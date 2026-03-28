from __future__ import annotations

from dataclasses import dataclass, field
from typing import List, Optional

from .priority_models import EquipmentStatus, InventoryStatus, PriorityActionKind, PriorityDecision, PrioritySeverity


@dataclass
class PrioritySnapshot:
    """Runtime-only facts not yet supplied by the mod can be fed here."""

    debuffs: List[str] = field(default_factory=list)
    continuous_damage: bool = False
    close_hostile_distance: Optional[float] = None
    stuck_detected: bool = False
    action_blocked: bool = False
    inventory_status: InventoryStatus = field(default_factory=InventoryStatus)
    equipment_status: EquipmentStatus = field(default_factory=EquipmentStatus)


class PriorityMonitor:
    """Evaluates survival and action-blocker conditions above instructed commands."""

    def evaluate(self, state, environment_summary=None, snapshot: Optional[PrioritySnapshot] = None) -> List[PriorityDecision]:
        snapshot = snapshot or PrioritySnapshot()
        decisions: List[PriorityDecision] = []
        player = getattr(state, "player", None)
        nearby_entities = getattr(state, "nearby_entities_summary", None)

        hp = getattr(player, "hp", None)
        max_hp = getattr(player, "max_hp", None)
        stamina = getattr(player, "stamina", None)
        max_stamina = getattr(player, "max_stamina", None)
        food = getattr(player, "food", None)
        water = getattr(player, "water", None)
        is_dead = bool(getattr(player, "is_dead", False))

        if is_dead:
            decisions.append(
                PriorityDecision(
                    action_kind=PriorityActionKind.AVOID_DEATH,
                    severity=PrioritySeverity.CRITICAL,
                    should_interrupt_now=True,
                    reason="player_is_dead_or_respawning",
                    recovery_hint="run respawn and keep command paused",
                )
            )
            return decisions

        if snapshot.continuous_damage or snapshot.debuffs:
            decisions.append(
                PriorityDecision(
                    action_kind=PriorityActionKind.CLEAR_CONTINUOUS_DAMAGE,
                    severity=PrioritySeverity.CRITICAL,
                    should_interrupt_now=True,
                    reason="continuous_damage_or_severe_debuff_detected",
                    recovery_hint="consume remedy or disengage before resuming work",
                )
            )

        close_hostile_distance = snapshot.close_hostile_distance
        if close_hostile_distance is None and nearby_entities is not None:
            close_hostile_distance = getattr(nearby_entities, "nearest_hostile_distance", None)
        if close_hostile_distance is not None and close_hostile_distance <= 4.0:
            decisions.append(
                PriorityDecision(
                    action_kind=PriorityActionKind.LEAVE_CLOSE_THREAT,
                    severity=PrioritySeverity.CRITICAL,
                    should_interrupt_now=True,
                    reason="hostile_within_close_range",
                    recovery_hint="back off and re-evaluate targeting",
                    distance_hint=close_hostile_distance,
                )
            )

        if hp is not None and max_hp:
            hp_ratio = hp / max_hp if max_hp > 0 else 0.0
            if hp_ratio <= 0.25:
                decisions.append(
                    PriorityDecision(
                        action_kind=PriorityActionKind.RECOVER_HEALTH,
                        severity=PrioritySeverity.WARNING,
                        should_interrupt_now=True,
                        reason="health_critically_low",
                        recovery_hint="consume healing and return to safe position",
                    )
                )

        if water is not None and water <= 15:
            decisions.append(
                PriorityDecision(
                    action_kind=PriorityActionKind.RECOVER_WATER,
                    severity=PrioritySeverity.WARNING,
                    should_interrupt_now=False,
                    reason="water_low",
                    recovery_hint="switch to water recovery after current subtask",
                    blocking=False,
                )
            )

        if food is not None and food <= 15:
            decisions.append(
                PriorityDecision(
                    action_kind=PriorityActionKind.RECOVER_HUNGER,
                    severity=PrioritySeverity.MONITOR,
                    should_interrupt_now=False,
                    reason="food_low",
                    recovery_hint="schedule food consumption soon",
                    blocking=False,
                )
            )

        if stamina is not None and max_stamina:
            stamina_ratio = stamina / max_stamina if max_stamina > 0 else 0.0
            if stamina_ratio <= 0.1:
                decisions.append(
                    PriorityDecision(
                        action_kind=PriorityActionKind.AVOID_STAMINA_EXHAUSTION,
                        severity=PrioritySeverity.MONITOR,
                        should_interrupt_now=False,
                        reason="stamina_nearly_exhausted",
                        recovery_hint="pause aggressive movement and allow recovery",
                        blocking=False,
                    )
                )

        if snapshot.action_blocked or (environment_summary is not None and getattr(environment_summary, "fall_hazard_ahead_hint", False) is True):
            decisions.append(
                PriorityDecision(
                    action_kind=PriorityActionKind.CLEAR_ACTION_BLOCKER,
                    severity=PrioritySeverity.WARNING,
                    should_interrupt_now=True,
                    reason="action_blocked_or_fall_hazard_detected",
                    recovery_hint="stop current task and reroute",
                )
            )

        equipment = snapshot.equipment_status
        if not equipment.has_required_tool or equipment.selected_tool_broken or equipment.missing_required_armor:
            decisions.append(
                PriorityDecision(
                    action_kind=PriorityActionKind.RESTORE_REQUIRED_EQUIPMENT,
                    severity=PrioritySeverity.WARNING,
                    should_interrupt_now=False,
                    reason="required_equipment_missing_or_broken",
                    recovery_hint="return to base or craft replacements",
                    blocking=False,
                )
            )

        if snapshot.stuck_detected:
            decisions.append(
                PriorityDecision(
                    action_kind=PriorityActionKind.ESCAPE_STUCK,
                    severity=PrioritySeverity.WARNING,
                    should_interrupt_now=True,
                    reason="stuck_detected",
                    recovery_hint="run obstacle recovery then resume or replan",
                )
            )

        decisions.sort(key=self._sort_key)
        return decisions

    def highest_priority(self, state, environment_summary=None, snapshot: Optional[PrioritySnapshot] = None) -> Optional[PriorityDecision]:
        decisions = self.evaluate(state, environment_summary=environment_summary, snapshot=snapshot)
        return None if not decisions else decisions[0]

    @staticmethod
    def _sort_key(decision: PriorityDecision):
        severity_rank = {
            PrioritySeverity.CRITICAL: 0,
            PrioritySeverity.WARNING: 1,
            PrioritySeverity.MONITOR: 2,
        }
        action_rank = {
            PriorityActionKind.AVOID_DEATH: 0,
            PriorityActionKind.CLEAR_CONTINUOUS_DAMAGE: 1,
            PriorityActionKind.LEAVE_CLOSE_THREAT: 2,
            PriorityActionKind.RECOVER_HEALTH: 3,
            PriorityActionKind.RECOVER_WATER: 4,
            PriorityActionKind.RECOVER_HUNGER: 5,
            PriorityActionKind.AVOID_STAMINA_EXHAUSTION: 6,
            PriorityActionKind.CLEAR_ACTION_BLOCKER: 7,
            PriorityActionKind.RESTORE_REQUIRED_EQUIPMENT: 8,
            PriorityActionKind.ESCAPE_STUCK: 9,
        }
        return (severity_rank[decision.severity], action_rank[decision.action_kind])
