from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, List, Optional

from sevendays_bridge.decision.target_classifier import DecisionTarget


@dataclass
class SearchContext:
    state: Any = None
    look_target: Any = None
    interaction_context: Any = None
    resource_query: Any = None
    interactable_query: Any = None
    entity_query: Any = None
    environment_summary: Any = None
    terrain_summary: Any = None
    biome_info: Any = None
    classified_targets: List[DecisionTarget] = field(default_factory=list)
    selected_target: Optional[DecisionTarget] = None
    action_plan: Any = None
    action_result: Any = None
    verification_ok: bool = False
    last_failure_reason: str = ""
    hostile_nearby: bool = False
    transitions: List[str] = field(default_factory=list)

    def add_transition(self, state_name: str) -> None:
        self.transitions.append(state_name)
