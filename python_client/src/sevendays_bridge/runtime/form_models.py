from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class CommandFormData:
    action_type: str
    target_x: str = ""
    target_y: str = ""
    target_z: str = ""
    target_resource_type: str = ""
    target_entity: str = ""
    target_area: str = ""
    base_id: str = ""
    priority: str = "50"
    interruptible: str = "interruptible"
    timeout_seconds: str = "60"
    retry_max_attempts: str = "2"
    metadata_json: str = "{}"


@dataclass(frozen=True)
class BaseFormData:
    base_id: str
    base_name: str
    anchor_x: str = ""
    anchor_y: str = ""
    anchor_z: str = ""
    min_x: str = ""
    min_y: str = ""
    min_z: str = ""
    max_x: str = ""
    max_y: str = ""
    max_z: str = ""
    safety_score: str = "0.5"
    build_plan_id: str = ""


@dataclass(frozen=True)
class ScanSettingsFormData:
    yaw_scan_min_deg: str = "-90"
    yaw_scan_max_deg: str = "90"
    yaw_scan_step_deg: str = "5"
    pitch_scan_min_deg: str = "-45"
    pitch_scan_max_deg: str = "0"
    pitch_scan_step_deg: str = "15"
    scan_settle_delay_ms: str = "60"
    near_ground_priority_enabled: bool = True
    near_ground_distance_threshold: str = "5.0"
