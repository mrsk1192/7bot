from __future__ import annotations

from typing import Dict, List, Optional

from .base_models import BaseDefinition


class BaseRegistry:
    """Stores GUI-defined bases. Explicit human-defined bases are authoritative."""

    def __init__(self) -> None:
        self._bases: Dict[str, BaseDefinition] = {}

    def add_or_update(self, base: BaseDefinition) -> BaseDefinition:
        self._bases[base.base_id] = base
        return base

    def get(self, base_id: str) -> Optional[BaseDefinition]:
        return self._bases.get(base_id)

    def remove(self, base_id: str) -> Optional[BaseDefinition]:
        return self._bases.pop(base_id, None)

    def rename(self, base_id: str, new_name: str) -> BaseDefinition:
        base = self._bases[base_id]
        updated = BaseDefinition(
            base_id=base.base_id,
            base_name=new_name,
            anchor_position=base.anchor_position,
            bounds=base.bounds,
            safety_score=base.safety_score,
            access_points=list(base.access_points),
            storage_points=list(base.storage_points),
            crafting_points=list(base.crafting_points),
            rest_points=list(base.rest_points),
            build_area=base.build_area,
            defense_area=base.defense_area,
            home_marker_priority=base.home_marker_priority,
            return_conditions=list(base.return_conditions),
            build_plan_id=base.build_plan_id,
            zones=list(base.zones),
        )
        self._bases[base_id] = updated
        return updated

    def list_bases(self) -> List[BaseDefinition]:
        return list(self._bases.values())
