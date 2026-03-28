from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Dict, List, Optional


class BaseZoneType(str, Enum):
    CORE = "core_zone"
    STORAGE = "storage_zone"
    WORK = "work_zone"
    ENTRY = "entry_zone"
    BUILD = "build_zone"
    DEFENSE = "defense_zone"


@dataclass(frozen=True)
class BaseBounds:
    min_x: float
    min_y: float
    min_z: float
    max_x: float
    max_y: float
    max_z: float


@dataclass(frozen=True)
class BaseZone:
    zone_type: BaseZoneType
    name: str
    bounds: BaseBounds
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass(frozen=True)
class BaseReturnCondition:
    kind: str
    threshold: Any


@dataclass(frozen=True)
class BaseDefinition:
    """Base is an explicit internal model, not an implied concept."""

    base_id: str
    base_name: str
    anchor_position: Any
    bounds: BaseBounds
    safety_score: float
    access_points: List[Any] = field(default_factory=list)
    storage_points: List[Any] = field(default_factory=list)
    crafting_points: List[Any] = field(default_factory=list)
    rest_points: List[Any] = field(default_factory=list)
    build_area: Optional[BaseBounds] = None
    defense_area: Optional[BaseBounds] = None
    home_marker_priority: int = 100
    return_conditions: List[BaseReturnCondition] = field(default_factory=list)
    build_plan_id: Optional[str] = None
    zones: List[BaseZone] = field(default_factory=list)
