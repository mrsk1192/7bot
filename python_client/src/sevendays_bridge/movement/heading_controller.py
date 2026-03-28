from __future__ import annotations

import math
import time
from dataclasses import dataclass
from typing import Optional

from .navigation_config import NavigationConfig


@dataclass(frozen=True)
class HeadingCorrection:
    mode: str
    yaw_diff: float
    applied_delta: float


class HeadingController:
    """Deterministic yaw and pitch controller for Phase 4.5."""

    def __init__(self, client, config: Optional[NavigationConfig] = None):
        self._client = client
        self.config = config or NavigationConfig()

    @staticmethod
    def normalize_angle(angle_deg: float) -> float:
        while angle_deg > 180.0:
            angle_deg -= 360.0
        while angle_deg < -180.0:
            angle_deg += 360.0
        return angle_deg

    @staticmethod
    def clamp(value: float, minimum: float, maximum: float) -> float:
        return max(minimum, min(maximum, value))

    def compute_target_yaw(self, current_pos, target_pos) -> float:
        dx = target_pos.x - current_pos.x
        dz = target_pos.z - current_pos.z
        return math.degrees(math.atan2(dx, dz))

    def compute_target_pitch(self, current_pos, target_pos) -> float:
        dx = target_pos.x - current_pos.x
        dy = target_pos.y - current_pos.y
        dz = target_pos.z - current_pos.z
        horizontal = math.sqrt((dx * dx) + (dz * dz))
        if horizontal <= 1e-6:
            return 0.0
        return math.degrees(math.atan2(dy, horizontal))

    def compute_yaw_diff(self, current_pos, current_yaw_deg: float, target_pos) -> float:
        target_yaw_deg = self.compute_target_yaw(current_pos, target_pos)
        return self.normalize_angle(target_yaw_deg - float(current_yaw_deg))

    def compute_pitch_diff(self, current_pos, current_pitch_deg: Optional[float], target_pos) -> float:
        current_pitch_deg = 0.0 if current_pitch_deg is None else float(current_pitch_deg)
        target_pitch_deg = self.compute_target_pitch(current_pos, target_pos)
        return self.normalize_angle(target_pitch_deg - current_pitch_deg)

    def classify_yaw_diff(self, yaw_diff: float) -> str:
        abs_diff = abs(yaw_diff)
        if abs_diff > self.config.approach_turn_in_place_threshold_deg:
            return "turn_in_place"
        if abs_diff >= self.config.approach_turn_and_move_threshold_deg:
            return "turn_and_move"
        if abs_diff >= self.config.approach_walk_priority_threshold_deg:
            return "walk_with_adjustment"
        return "walk_priority"

    def _yaw_step_for_mode(self, mode: str) -> float:
        if mode == "turn_in_place":
            return self.config.approach_turn_only_step_deg
        if mode == "turn_and_move":
            return self.config.approach_turn_and_move_step_deg
        if mode == "walk_with_adjustment":
            return self.config.approach_walk_adjust_step_deg
        return min(2.0, self.config.approach_walk_adjust_step_deg)

    def apply_heading_correction(self, yaw_diff: float) -> HeadingCorrection:
        mode = self.classify_yaw_diff(yaw_diff)
        step = self._yaw_step_for_mode(mode)
        applied_delta = self.clamp(yaw_diff, -step, step)
        if abs(applied_delta) > 0.01:
            self._client.look_delta(dx=applied_delta, dy=0.0)
        return HeadingCorrection(mode=mode, yaw_diff=yaw_diff, applied_delta=applied_delta)

    def apply_pitch_correction(self, pitch_diff: float) -> float:
        applied_delta = self.clamp(pitch_diff, -4.0, 4.0)
        if abs(applied_delta) > 0.01:
            self._client.look_delta(dx=0.0, dy=applied_delta)
        return applied_delta

    def turn_relative(self, delta_yaw_deg: float) -> None:
        if abs(delta_yaw_deg) > 0.01:
            self._client.look_delta(dx=delta_yaw_deg, dy=0.0)

    def turn_to_yaw(self, target_yaw_deg: float, tolerance_deg: float = 2.0, max_iterations: int = 24) -> bool:
        for _ in range(max_iterations):
            state = self._client.get_state()
            player = state.player
            rotation = None if player is None else player.rotation
            current_yaw = 0.0 if rotation is None or rotation.yaw is None else rotation.yaw
            yaw_diff = self.normalize_angle(target_yaw_deg - current_yaw)
            if abs(yaw_diff) <= tolerance_deg:
                return True
            self.apply_heading_correction(yaw_diff)
            time.sleep(self.config.alignment_settle_seconds)
        return False

    def turn_to_view(
        self,
        target_yaw_deg: float,
        target_pitch_deg: float,
        tolerance_yaw_deg: float = 2.0,
        tolerance_pitch_deg: float = 2.0,
        max_iterations: int = 24,
    ) -> bool:
        for _ in range(max_iterations):
            state = self._client.get_state()
            player = state.player
            rotation = None if player is None else player.rotation
            current_yaw = 0.0 if rotation is None or rotation.yaw is None else rotation.yaw
            current_pitch = 0.0 if rotation is None or rotation.pitch is None else rotation.pitch
            yaw_diff = self.normalize_angle(target_yaw_deg - current_yaw)
            pitch_diff = target_pitch_deg - current_pitch
            if abs(yaw_diff) <= tolerance_yaw_deg and abs(pitch_diff) <= tolerance_pitch_deg:
                return True
            if abs(yaw_diff) > tolerance_yaw_deg:
                self.apply_heading_correction(yaw_diff)
            if abs(pitch_diff) > tolerance_pitch_deg:
                self.apply_pitch_correction(pitch_diff)
            time.sleep(self.config.alignment_settle_seconds)
        return False

    def align_view_to_position(
        self,
        target_pos,
        tolerance_yaw_deg: float = 2.0,
        tolerance_pitch_deg: float = 3.0,
        max_iterations: int = 20,
    ) -> bool:
        for _ in range(max_iterations):
            state = self._client.get_state()
            player = state.player
            if player is None or player.position is None:
                return False
            rotation = player.rotation
            current_yaw = 0.0 if rotation is None or rotation.yaw is None else rotation.yaw
            current_pitch = 0.0 if rotation is None or rotation.pitch is None else rotation.pitch
            yaw_diff = self.compute_yaw_diff(player.position, current_yaw, target_pos)
            pitch_diff = self.compute_pitch_diff(player.position, current_pitch, target_pos)
            if abs(yaw_diff) <= tolerance_yaw_deg and abs(pitch_diff) <= tolerance_pitch_deg:
                return True
            self.apply_heading_correction(yaw_diff)
            if abs(yaw_diff) <= self.config.approach_turn_and_move_threshold_deg:
                self.apply_pitch_correction(pitch_diff)
            time.sleep(self.config.alignment_settle_seconds)
        return False
