from __future__ import annotations

import math
import time
from dataclasses import dataclass
from typing import Optional

from sevendays_bridge.movement.heading_controller import HeadingController
from sevendays_bridge.movement.jump_decision import JumpDecision
from sevendays_bridge.movement.locomotion_controller import LocomotionController
from sevendays_bridge.movement.navigation_config import NavigationConfig
from sevendays_bridge.movement.obstacle_recovery import ObstacleRecovery


@dataclass(frozen=True)
class ApproachResult:
    status: str
    reason: str
    recovery_attempts: int
    alignment_attempts: int
    final_distance: float


class TargetApproach:
    """Rule-based target approach for Phase 4.5."""

    def __init__(self, client, config: Optional[NavigationConfig] = None):
        self._client = client
        self.config = config or NavigationConfig()
        self.heading = HeadingController(client, self.config)
        self.loco = LocomotionController(client, self.config)
        self.jump = JumpDecision(self.config)
        self.recovery = ObstacleRecovery(client, self.loco, self.heading, self.jump, self.config)

    @staticmethod
    def _distance_2d(a, b) -> float:
        return math.sqrt(((a.x - b.x) ** 2) + ((a.z - b.z) ** 2))

    def _stop_distance_for_kind(self, target_kind: str) -> float:
        if target_kind in {"loot", "container", "interactable", "vehicle", "npc", "trader"}:
            return self.config.approach_stop_distance_loot
        if target_kind in {"resource", "block", "terrain"}:
            return self.config.approach_stop_distance_resource
        return self.config.approach_stop_distance_entity

    def _is_look_match(self, look_target, expected_name: str, expected_kind: str) -> bool:
        if not look_target.has_target:
            return False
        name_match = bool(expected_name) and look_target.target_name.lower() == expected_name.lower()
        kind_match = bool(expected_kind) and look_target.target_kind == expected_kind
        if expected_kind == "loot" and look_target.target_kind == "container":
            kind_match = True
        if expected_kind == "container" and look_target.target_kind == "loot":
            kind_match = True
        return bool(name_match or kind_match)

    def _current_player_state(self):
        state = self._client.get_state()
        player = state.player
        if player is None or player.position is None:
            return None, None
        return state, player

    def _align_sight(self, target_pos, expected_name: str, expected_kind: str) -> ApproachResult:
        for attempt in range(1, self.config.alignment_max_attempts + 1):
            look_target = self._client.get_look_target()
            if self._is_look_match(look_target, expected_name, expected_kind):
                return ApproachResult("success", "target_focused", 0, attempt, look_target.distance)

            self.heading.align_view_to_position(target_pos, max_iterations=3)
            time.sleep(self.config.alignment_settle_seconds)

            look_target = self._client.get_look_target()
            if self._is_look_match(look_target, expected_name, expected_kind):
                return ApproachResult("success", "target_focused_after_alignment", 0, attempt, look_target.distance)

            if attempt in {5, 10, 15}:
                if look_target.distance <= self.config.approach_stop_distance_resource + 0.4:
                    self.loco.move_backward_pulse(self.config.micro_adjust_backward_seconds)
                else:
                    self.loco.pulse_walk(self.config.micro_adjust_forward_seconds)
                time.sleep(self.config.alignment_settle_seconds)

        return ApproachResult("alignment_failed", "look_target_did_not_converge", 0, self.config.alignment_max_attempts, 0.0)

    def approach(self, target_pos, target_name: str, target_kind: str) -> str:
        return self.approach_with_details(target_pos, target_name, target_kind).status

    def approach_with_details(self, target_pos, target_name: str, target_kind: str) -> ApproachResult:
        stop_distance = self._stop_distance_for_kind(target_kind)
        recovery_attempts = 0
        last_progress_checkpoint_time = time.time()
        last_progress_checkpoint_pos = None
        last_progress_checkpoint_distance = None
        last_turn_progress_time = None
        previous_abs_yaw_diff = None
        jump_attempts_for_target = 0

        while True:
            state, player = self._current_player_state()
            if player is None:
                self.loco.emergency_stop()
                return ApproachResult("unreachable", "player_state_unavailable", recovery_attempts, 0, float("inf"))

            current_pos = player.position
            current_distance = self._distance_2d(current_pos, target_pos)
            if last_progress_checkpoint_pos is None:
                last_progress_checkpoint_pos = current_pos
                last_progress_checkpoint_distance = current_distance

            if current_distance <= stop_distance:
                self.loco.emergency_stop()
                alignment = self._align_sight(target_pos, target_name, target_kind)
                return ApproachResult(
                    alignment.status,
                    alignment.reason,
                    recovery_attempts,
                    alignment.alignment_attempts,
                    current_distance,
                )

            rotation = player.rotation
            current_yaw = 0.0 if rotation is None or rotation.yaw is None else rotation.yaw
            yaw_diff = self.heading.compute_yaw_diff(current_pos, current_yaw, target_pos)
            correction = self.heading.apply_heading_correction(yaw_diff)
            now = time.time()

            if correction.mode == "turn_in_place":
                self.loco.stop()
                if previous_abs_yaw_diff is None or abs(yaw_diff) < previous_abs_yaw_diff - 1.0:
                    last_turn_progress_time = now
                elif last_turn_progress_time is None:
                    last_turn_progress_time = now
                elif now - last_turn_progress_time >= self.config.turn_stagnation_seconds:
                    self.loco.emergency_stop()
                    recovery_attempts += 1
                    recovery = self.recovery.execute_recovery_full(
                        attempt=recovery_attempts,
                        target_pos=target_pos,
                        terrain_summary=self._client.get_terrain_summary(),
                        env_summary=self._client.get_environment_summary(),
                        distance=current_distance,
                        jump_attempts_for_target=jump_attempts_for_target,
                    )
                    if recovery.jump_attempted:
                        jump_attempts_for_target += 1
                    if recovery.status == "unreachable":
                        return ApproachResult("unreachable", recovery.reason, recovery_attempts, 0, current_distance)
                    last_turn_progress_time = now
                previous_abs_yaw_diff = abs(yaw_diff)
                time.sleep(self.config.movement_loop_sleep_seconds)
            else:
                last_turn_progress_time = None
                previous_abs_yaw_diff = abs(yaw_diff)
                if correction.mode == "turn_and_move":
                    self.loco.pulse_walk(self.config.movement_pulse_turn_and_move_seconds)
                elif correction.mode == "walk_with_adjustment":
                    self.loco.start_walk()
                    time.sleep(self.config.movement_loop_sleep_seconds)
                else:
                    self.loco.start_walk()
                    time.sleep(self.config.movement_loop_sleep_seconds)

            if now - last_progress_checkpoint_time >= self.config.stagnation_window_seconds:
                progress_distance = self._distance_2d(last_progress_checkpoint_pos, current_pos)
                distance_reduction = (last_progress_checkpoint_distance or current_distance) - current_distance
                if (
                    progress_distance < self.config.stagnation_min_progress_meters
                    and distance_reduction < self.config.stagnation_min_distance_reduction_meters
                ):
                    self.loco.emergency_stop()
                    recovery_attempts += 1
                    recovery = self.recovery.execute_recovery_full(
                        attempt=recovery_attempts,
                        target_pos=target_pos,
                        terrain_summary=self._client.get_terrain_summary(),
                        env_summary=self._client.get_environment_summary(),
                        distance=current_distance,
                        jump_attempts_for_target=jump_attempts_for_target,
                    )
                    if recovery.jump_attempted:
                        jump_attempts_for_target += 1
                    if recovery.status == "unreachable":
                        return ApproachResult("unreachable", recovery.reason, recovery_attempts, 0, current_distance)
                last_progress_checkpoint_time = now
                last_progress_checkpoint_pos = current_pos
                last_progress_checkpoint_distance = current_distance

