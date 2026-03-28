from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Iterable, List, Optional


@dataclass(frozen=True)
class DecisionTarget:
    source: str
    category: str
    target_kind: str
    name: str
    action_kind: str
    distance: float
    position: Any
    can_interact: bool
    hostile: bool
    line_of_sight_clear: Any
    candidate_confidence: float
    likely_resource_type: str
    target_id: Any
    payload: Any

    @property
    def target_key(self) -> str:
        position_key = "none"
        if self.position is not None:
            position_key = f"{getattr(self.position, 'x', 0.0):.3f}:{getattr(self.position, 'y', 0.0):.3f}:{getattr(self.position, 'z', 0.0):.3f}"
        return f"{self.category}|{self.target_kind}|{self.name}|{self.target_id}|{position_key}"


class TargetClassifier:
    """Normalizes look/query payloads into deterministic decision targets."""

    IMMEDIATE_LOOK_KINDS = {"loot", "container", "interactable", "resource", "enemy", "entity", "npc", "trader", "vehicle"}

    def classify_look_target(self, look_target, interaction_context) -> Optional[DecisionTarget]:
        if look_target is None or not look_target.has_target:
            return None

        target_kind = (look_target.target_kind or "unknown").lower()
        if target_kind == "none":
            return None

        action_kind = (look_target.interaction_action_kind or "unknown").lower()
        category = self._category_from_look_target(target_kind, look_target)
        can_interact = bool(look_target.can_interact or getattr(interaction_context, "can_interact_now", False))
        if action_kind in {"none", "unknown"} and interaction_context is not None:
            action_kind = (interaction_context.suggested_action_kind or action_kind).lower()

        return DecisionTarget(
            source="look_target",
            category=category,
            target_kind=target_kind,
            name=look_target.target_name or "Unknown",
            action_kind=action_kind,
            distance=float(look_target.distance or 0.0),
            position=look_target.position,
            can_interact=can_interact,
            hostile=bool(look_target.hostile),
            line_of_sight_clear=look_target.line_of_sight_clear,
            candidate_confidence=float(look_target.candidate_confidence or 0.0),
            likely_resource_type=(look_target.likely_resource_type or "unknown").lower(),
            target_id=look_target.target_id,
            payload=look_target,
        )

    def classify_resource_candidates(self, resources: Iterable[Any]) -> List[DecisionTarget]:
        result: List[DecisionTarget] = []
        for resource in resources or []:
            result.append(
                DecisionTarget(
                    source="resource_query",
                    category="resource_candidate",
                    target_kind="resource",
                    name=resource.name or "Unknown",
                    action_kind=self._resource_action_kind(resource),
                    distance=float(resource.distance or 0.0),
                    position=resource.position,
                    can_interact=False,
                    hostile=False,
                    line_of_sight_clear=resource.line_of_sight_clear,
                    candidate_confidence=float(resource.candidate_confidence or 0.0),
                    likely_resource_type=(resource.likely_resource_type or "unknown").lower(),
                    target_id=resource.block_id,
                    payload=resource,
                )
            )
        return result

    def classify_interactables(self, interactables: Iterable[Any]) -> List[DecisionTarget]:
        result: List[DecisionTarget] = []
        for interactable in interactables or []:
            kind = (interactable.kind or "unknown").lower()
            category = self._category_from_interactable_kind(kind)
            result.append(
                DecisionTarget(
                    source="interactable_query",
                    category=category,
                    target_kind=kind,
                    name=interactable.name or "Unknown",
                    action_kind=(interactable.interaction_action_kind or "unknown").lower(),
                    distance=float(interactable.distance or 0.0),
                    position=interactable.position,
                    can_interact=bool(interactable.can_interact),
                    hostile=False,
                    line_of_sight_clear=interactable.line_of_sight_clear,
                    candidate_confidence=1.0 if interactable.can_interact else 0.6,
                    likely_resource_type="loot" if category == "loot_candidate" else "unknown",
                    target_id=interactable.id,
                    payload=interactable,
                )
            )
        return result

    def classify_entities(self, entities: Iterable[Any]) -> List[DecisionTarget]:
        result: List[DecisionTarget] = []
        for entity in entities or []:
            kind = (entity.kind or "unknown").lower()
            category = self._category_from_entity(entity)
            result.append(
                DecisionTarget(
                    source="entity_query",
                    category=category,
                    target_kind=kind,
                    name=entity.entity_name or "Unknown",
                    action_kind="attack" if bool(entity.hostile) else "observe",
                    distance=float(entity.distance or 0.0),
                    position=entity.position,
                    can_interact=bool(entity.can_interact),
                    hostile=bool(entity.hostile),
                    line_of_sight_clear=entity.line_of_sight_clear,
                    candidate_confidence=1.0 if entity.alive else 0.2,
                    likely_resource_type="enemy" if bool(entity.hostile) else ("npc" if kind in {"npc", "trader"} else "unknown"),
                    target_id=entity.entity_id,
                    payload=entity,
                )
            )
        return result

    def classify_all(
        self,
        look_target,
        interaction_context,
        resource_candidates: Iterable[Any],
        interactables: Iterable[Any],
        entities: Iterable[Any],
    ) -> List[DecisionTarget]:
        targets: List[DecisionTarget] = []
        immediate = self.classify_look_target(look_target, interaction_context)
        if immediate is not None:
            targets.append(immediate)
        targets.extend(self.classify_resource_candidates(resource_candidates))
        targets.extend(self.classify_interactables(interactables))
        targets.extend(self.classify_entities(entities))
        return targets

    def _resource_action_kind(self, resource: Any) -> str:
        likely = (getattr(resource, "likely_resource_type", "unknown") or "unknown").lower()
        if likely in {"wood"}:
            return "harvest"
        return "mine"

    def _category_from_look_target(self, target_kind: str, look_target: Any) -> str:
        if target_kind in {"loot", "container"}:
            return "loot_candidate"
        if target_kind == "resource" or bool(getattr(look_target, "is_resource_candidate", False)):
            return "resource_candidate"
        if target_kind in {"interactable", "vehicle"}:
            return "interactable_candidate"
        if target_kind in {"enemy"} or bool(getattr(look_target, "hostile", False)):
            return "hostile_candidate"
        if target_kind in {"npc", "trader"}:
            return "npc_candidate"
        return "ignore_candidate"

    def _category_from_interactable_kind(self, kind: str) -> str:
        if kind in {"loot", "container"}:
            return "loot_candidate"
        if kind in {"npc", "trader"}:
            return "npc_candidate"
        if kind in {"interactable", "vehicle", "door"}:
            return "interactable_candidate"
        return "ignore_candidate"

    def _category_from_entity(self, entity: Any) -> str:
        if bool(entity.hostile):
            return "hostile_candidate"
        kind = (entity.kind or "unknown").lower()
        if kind in {"npc", "trader"}:
            return "npc_candidate"
        return "ignore_candidate"
