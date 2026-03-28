from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Dict, List, Optional


class BuildStepType(str, Enum):
    FLOOR_FOUNDATION = "floor_foundation"
    OUTER_WALL = "outer_wall"
    DOOR_FRAME = "door_frame"
    STORAGE_CORNER = "storage_corner"
    WORK_AREA = "work_area"
    PERIMETER_EXPAND = "perimeter_expand"
    REPAIR_DAMAGED_BLOCKS = "repair_damaged_blocks"


@dataclass(frozen=True)
class BuildStep:
    step_id: str
    step_type: BuildStepType
    target_position: Any
    required_items: Dict[str, int] = field(default_factory=dict)
    required_tool: Optional[str] = None
    preferred_hotbar_slot: Optional[int] = None
    verification_hint: str = "position_reached_and_action_sent"
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass(frozen=True)
class BuildPlan:
    build_plan_id: str
    name: str
    description: str
    steps: List[BuildStep]


@dataclass
class BuildProgress:
    build_plan_id: str
    current_step_index: int = 0
    completed_step_ids: List[str] = field(default_factory=list)
    failed_step_ids: List[str] = field(default_factory=list)
    placed_blocks: List[str] = field(default_factory=list)
    remaining_materials: Dict[str, int] = field(default_factory=dict)
    interrupted: bool = False

    def current_step(self, plan: BuildPlan) -> Optional[BuildStep]:
        if self.current_step_index >= len(plan.steps):
            return None
        return plan.steps[self.current_step_index]
