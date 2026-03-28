from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Optional


class PrioritySeverity(str, Enum):
    MONITOR = "monitor"
    WARNING = "warning"
    CRITICAL = "critical"


class PriorityActionKind(str, Enum):
    AVOID_DEATH = "avoid_death"
    CLEAR_CONTINUOUS_DAMAGE = "clear_continuous_damage"
    LEAVE_CLOSE_THREAT = "leave_close_threat"
    RECOVER_HEALTH = "recover_health"
    RECOVER_WATER = "recover_water"
    RECOVER_HUNGER = "recover_hunger"
    AVOID_STAMINA_EXHAUSTION = "avoid_stamina_exhaustion"
    CLEAR_ACTION_BLOCKER = "clear_action_blocker"
    RESTORE_REQUIRED_EQUIPMENT = "restore_required_equipment"
    ESCAPE_STUCK = "escape_stuck"


@dataclass(frozen=True)
class InventoryStatus:
    is_full: bool = False
    carried_weight_ratio: float = 0.0


@dataclass(frozen=True)
class EquipmentStatus:
    has_required_tool: bool = True
    selected_tool_broken: bool = False
    missing_required_armor: bool = False


@dataclass(frozen=True)
class PriorityDecision:
    action_kind: PriorityActionKind
    severity: PrioritySeverity
    should_interrupt_now: bool
    reason: str
    recovery_hint: str
    blocking: bool = True
    distance_hint: Optional[float] = None
