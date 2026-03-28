from __future__ import annotations

from dataclasses import dataclass
from typing import Optional

from sevendays_bridge.movement.target_approach import TargetApproach

from .build_plan_models import BuildPlan, BuildProgress


@dataclass(frozen=True)
class BuildExecutionResult:
    status: str
    step_id: Optional[str]
    reason: str


class BuildExecutor:
    """Executes build plans as explicit step sequences without hiding progress."""

    def __init__(self, client, approach: Optional[TargetApproach] = None) -> None:
        self._client = client
        self.approach = approach or TargetApproach(client)

    def execute_next_step(self, plan: BuildPlan, progress: BuildProgress) -> BuildExecutionResult:
        step = progress.current_step(plan)
        if step is None:
            return BuildExecutionResult("completed", None, "build_plan_completed")

        approach_result = self.approach.approach_with_details(step.target_position, step.step_id, "interactable")
        if approach_result.status == "unreachable":
            progress.failed_step_ids.append(step.step_id)
            return BuildExecutionResult("failed", step.step_id, approach_result.reason)

        if step.preferred_hotbar_slot is not None:
            self._client.select_hotbar_slot(step.preferred_hotbar_slot)

        # Placement/repair remains client-side and deterministic. We use primary action
        # only after approach has converged, so step execution stays resumable.
        self._client.attack_primary(duration_ms=int(step.metadata.get("hold_ms", 250)))
        self._client.stop_all()
        progress.completed_step_ids.append(step.step_id)
        progress.current_step_index += 1
        return BuildExecutionResult("step_completed", step.step_id, step.verification_hint)
