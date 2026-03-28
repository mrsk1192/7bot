from __future__ import annotations

from dataclasses import asdict, dataclass
from typing import Dict, List


@dataclass(frozen=True)
class NavigationConfig:
    approach_stop_distance_loot: float = 2.4
    approach_stop_distance_resource: float = 2.7
    approach_stop_distance_entity: float = 2.5
    approach_turn_in_place_threshold_deg: float = 60.0
    approach_turn_and_move_threshold_deg: float = 20.0
    approach_walk_priority_threshold_deg: float = 5.0
    approach_turn_only_step_deg: float = 18.0
    approach_turn_and_move_step_deg: float = 10.0
    approach_walk_adjust_step_deg: float = 4.0
    alignment_max_attempts: int = 20
    alignment_settle_seconds: float = 0.08
    micro_adjust_forward_seconds: float = 0.15
    micro_adjust_backward_seconds: float = 0.15
    movement_pulse_turn_and_move_seconds: float = 0.2
    movement_loop_sleep_seconds: float = 0.12
    stagnation_window_seconds: float = 2.0
    stagnation_min_progress_meters: float = 0.5
    stagnation_min_distance_reduction_meters: float = 0.35
    turn_stagnation_seconds: float = 3.0
    recovery_turn_degrees: float = 20.0
    recovery_backward_seconds: float = 0.4
    recovery_forward_seconds: float = 0.8
    max_recovery_attempts: int = 3
    cell_size_meters: float = 10.0
    local_scan_radius: float = 24.0
    sector_scan_radius: float = 10.0
    yaw_scan_min_deg: int = -90
    yaw_scan_max_deg: int = 90
    yaw_scan_step_deg: int = 5
    pitch_scan_min_deg: int = -45
    pitch_scan_max_deg: int = 0
    pitch_scan_step_deg: int = 15
    scan_settle_delay_ms: int = 60
    near_ground_priority_enabled: bool = True
    near_ground_distance_threshold: float = 5.0
    exploration_revisit_cooldown_sec: float = 30.0
    movement_default_mode: str = "walk"
    sprint_enabled_for_navigation: bool = False
    jump_enabled_for_navigation: bool = False
    jump_only_for_bypass_when_capable: bool = True
    max_jumpable_obstacle_height: float = 1.2
    required_landing_clearance: float = 1.0
    required_forward_space_before_jump: float = 1.25
    jump_forward_press_delay_ms: int = 150
    jump_max_attempts_per_target: int = 1

    def generate_yaw_scan_angles(self) -> List[int]:
        values: List[int] = []
        current = self.yaw_scan_min_deg
        while current <= self.yaw_scan_max_deg:
            values.append(current)
            current += self.yaw_scan_step_deg
        return values

    def generate_pitch_scan_angles(self) -> List[int]:
        values: List[int] = []
        current = 0
        while current >= self.pitch_scan_min_deg:
            values.append(current)
            current -= self.pitch_scan_step_deg
        return values

    def validate_scan_settings(self) -> List[str]:
        errors: List[str] = []

        if self.yaw_scan_step_deg <= 0:
            errors.append("yaw_scan_step_deg must be greater than 0.")
        if self.yaw_scan_min_deg >= self.yaw_scan_max_deg:
            errors.append("yaw_scan_min_deg must be less than yaw_scan_max_deg.")
        if self.yaw_scan_min_deg < -180:
            errors.append("yaw_scan_min_deg must be at least -180.")
        if self.yaw_scan_max_deg > 180:
            errors.append("yaw_scan_max_deg must be at most 180.")

        if self.pitch_scan_step_deg <= 0:
            errors.append("pitch_scan_step_deg must be greater than 0.")
        if self.pitch_scan_min_deg > 0:
            errors.append("pitch_scan_min_deg must be 0 or below.")
        if self.pitch_scan_max_deg < 0:
            errors.append("pitch_scan_max_deg must be 0 or above.")
        if self.pitch_scan_min_deg < -89:
            errors.append("pitch_scan_min_deg must be at least -89.")
        if self.pitch_scan_max_deg > 89:
            errors.append("pitch_scan_max_deg must be at most 89.")
        if self.scan_settle_delay_ms <= 0:
            errors.append("scan_settle_delay_ms must be greater than 0.")
        if self.near_ground_distance_threshold <= 0:
            errors.append("near_ground_distance_threshold must be greater than 0.")

        if not errors and not self.generate_yaw_scan_angles():
            errors.append("yaw scan configuration produced no scan angles.")
        if not errors and not self.generate_pitch_scan_angles():
            errors.append("pitch scan configuration produced no scan angles.")

        return errors

    def to_dict(self) -> Dict[str, object]:
        return asdict(self)

    @classmethod
    def from_mapping(cls, payload: Dict[str, object] | None) -> "NavigationConfig":
        if payload is None:
            return cls()
        known = {field_name: payload[field_name] for field_name in cls.__dataclass_fields__ if field_name in payload}
        return cls(**known)
