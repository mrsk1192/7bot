from __future__ import annotations

from typing import Dict, List, Optional

from .build_plan_models import BuildPlan


class BuildPlanRegistry:
    """Stores deterministic build plans that GUI and runtime can reference by id."""

    def __init__(self) -> None:
        self._plans: Dict[str, BuildPlan] = {}

    def add_or_update(self, plan: BuildPlan) -> BuildPlan:
        self._plans[plan.build_plan_id] = plan
        return plan

    def get(self, build_plan_id: str) -> Optional[BuildPlan]:
        return self._plans.get(build_plan_id)

    def list_plans(self) -> List[BuildPlan]:
        return list(self._plans.values())
