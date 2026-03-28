from __future__ import annotations

import math
import time
from dataclasses import dataclass
from typing import Optional

from .navigation_config import NavigationConfig


@dataclass(frozen=True)
class RecoveryResult:
    status: str
    attempts_used: int
    jump_attempted: bool
    reason: str


class ObstacleRecovery:
    """Fixed-sequence obstacle recovery with deterministic left/right/jump order."""

    def __init__(self, client, locomotion, heading, jump, config: Optional[NavigationConfig] = None):
        self._client = client
        self.loco = locomotion
        self.heading = heading
        self.jump_decision = jump
        self.config = config or NavigationConfig()

    @staticmethod
    def _distance_2d(a, b) -> float:
        return math.sqrt(((a.x - b.x) ** 2) + ((a.z - b.z) ** 2))

    def _sample_distance(self, target_pos) -> Optional[float]:
        state = self._client.get_state()
        player = state.player
        if player is None or player.position is None:
            return None
        return self._distance_2d(player.position, target_pos)

    def _turn_and_walk(self, turn_deg: float, duration_sec: float) -> None:
        self.heading.turn_relative(turn_deg)
        time.sleep(0.1)
        self.loco.pulse_walk(duration_sec)

    def _realign(self, target_pos) -> None:
        self.loco.emergency_stop()
        time.sleep(0.05)
        self.heading.align_view_to_position(target_pos, max_iterations=8)

    def execute_recovery_full(
        self,
        attempt: int,
        target_pos,
        terrain_summary,
        env_summary,
        distance: float,
        jump_attempts_for_target: int = 0,
    ) -> RecoveryResult:
        if attempt > self.config.max_recovery_attempts:
            return RecoveryResult("unreachable", attempt - 1, False, "max_recovery_attempts_exceeded")

        baseline_distance = self._sample_distance(target_pos)

        self.loco.emergency_stop()
        time.sleep(0.05)
        self.loco.move_backward_pulse(self.config.recovery_backward_seconds)

        self._turn_and_walk(-self.config.recovery_turn_degrees, self.config.recovery_forward_seconds)
        self._realign(target_pos)
        left_distance = self._sample_distance(target_pos)
        if baseline_distance is not None and left_distance is not None and left_distance < baseline_distance - 0.35:
            return RecoveryResult("retry", attempt, False, "left_recovery_improved_distance")

        self._turn_and_walk(self.config.recovery_turn_degrees, self.config.recovery_forward_seconds)
        self._realign(target_pos)
        right_distance = self._sample_distance(target_pos)
        if baseline_distance is not None and right_distance is not None and right_distance < baseline_distance - 0.35:
            return RecoveryResult("retry", attempt, False, "right_recovery_improved_distance")

        decision = self.jump_decision.evaluate(
            terrain_summary=terrain_summary,
            environment_summary=env_summary,
            distance_to_target=distance,
            recovery_attempt_index=attempt,
            jump_attempts_for_target=jump_attempts_for_target,
            left_and_right_failed=True,
        )
        if decision.should_jump:
            self.loco.forward_jump()
            self._realign(target_pos)
            return RecoveryResult("retry", attempt, True, decision.reason)

        if attempt >= self.config.max_recovery_attempts:
            return RecoveryResult("unreachable", attempt, False, decision.reason)
        return RecoveryResult("retry", attempt, False, decision.reason)
