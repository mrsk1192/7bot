from __future__ import annotations

from dataclasses import dataclass
from typing import Optional

from .target_classifier import DecisionTarget


@dataclass(frozen=True)
class ActionPlan:
    plan_kind: str
    command_name: str
    duration_ms: int
    requires_alignment: bool
    requires_approach: bool
    stop_distance: float
    reason: str


class ActionSelector:
    """Maps a classified target to a deterministic action plan."""

    def __init__(
        self,
        loot_stop_distance: float = 2.4,
        resource_stop_distance: float = 2.7,
        entity_stop_distance: float = 2.5,
        primary_action_duration_ms: int = 700,
    ) -> None:
        self.loot_stop_distance = loot_stop_distance
        self.resource_stop_distance = resource_stop_distance
        self.entity_stop_distance = entity_stop_distance
        self.primary_action_duration_ms = primary_action_duration_ms

    def select(self, target: DecisionTarget) -> ActionPlan:
        if target.category == "hostile_candidate":
            return ActionPlan(
                plan_kind="avoid_hostile",
                command_name="avoid_hostile",
                duration_ms=0,
                requires_alignment=False,
                requires_approach=False,
                stop_distance=self.entity_stop_distance,
                reason="hostile targets suspend gathering/search actions",
            )

        if target.category == "loot_candidate":
            return ActionPlan(
                plan_kind="interact",
                command_name="use_interact",
                duration_ms=0,
                requires_alignment=True,
                requires_approach=target.distance > self.loot_stop_distance or not target.can_interact,
                stop_distance=self.loot_stop_distance,
                reason="loot/container targets use interact once aligned and in range",
            )

        if target.category == "resource_candidate":
            return ActionPlan(
                plan_kind="harvest",
                command_name="primary_action",
                duration_ms=self.primary_action_duration_ms,
                requires_alignment=True,
                requires_approach=target.distance > self.resource_stop_distance,
                stop_distance=self.resource_stop_distance,
                reason="resource targets use primary action in harvesting range",
            )

        if target.category == "interactable_candidate":
            return ActionPlan(
                plan_kind="interact",
                command_name="use_interact",
                duration_ms=0,
                requires_alignment=True,
                requires_approach=target.distance > self.loot_stop_distance or not target.can_interact,
                stop_distance=self.loot_stop_distance,
                reason="interactables follow the use/open/activate path via interact",
            )

        if target.category == "npc_candidate":
            return ActionPlan(
                plan_kind="observe",
                command_name="none",
                duration_ms=0,
                requires_alignment=False,
                requires_approach=False,
                stop_distance=self.loot_stop_distance,
                reason="npc/trader targets are not repeatedly interacted with in Phase 5",
            )

        return ActionPlan(
            plan_kind="ignore",
            command_name="none",
            duration_ms=0,
            requires_alignment=False,
            requires_approach=False,
            stop_distance=self.loot_stop_distance,
            reason="target is ignored by Phase 5 rules",
        )
