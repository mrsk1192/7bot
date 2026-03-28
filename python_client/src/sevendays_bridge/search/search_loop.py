from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Optional

from sevendays_bridge.decision.action_selector import ActionPlan, ActionSelector
from sevendays_bridge.decision.target_classifier import DecisionTarget, TargetClassifier
from sevendays_bridge.decision.target_priority import ScoredTarget, TargetPriority
from sevendays_bridge.exploration.systematic_explorer import SystematicExplorer
from sevendays_bridge.movement.navigation_config import NavigationConfig
from sevendays_bridge.movement.target_approach import TargetApproach

from .result_memory import ResultMemory
from .search_context import SearchContext
from .state_machine import SearchStateMachine, SearchStateMachineState


@dataclass(frozen=True)
class SearchLoopResult:
    final_state: str
    selected_target_name: Optional[str]
    selected_target_category: Optional[str]
    action_name: str
    verification_ok: bool
    failure_reason: str
    transitions: list[str]


class SearchLoop:
    """Phase 5 rule-based search loop that integrates observation, selection, movement, and action."""

    def __init__(
        self,
        client,
        config: Optional[NavigationConfig] = None,
        result_memory: Optional[ResultMemory] = None,
    ) -> None:
        self._client = client
        self.config = config or NavigationConfig()
        self.classifier = TargetClassifier()
        self.priority = TargetPriority()
        self.action_selector = ActionSelector(
            loot_stop_distance=self.config.approach_stop_distance_loot,
            resource_stop_distance=self.config.approach_stop_distance_resource,
            entity_stop_distance=self.config.approach_stop_distance_entity,
        )
        self.result_memory = result_memory or ResultMemory()
        self.state_machine = SearchStateMachine()
        self.approach = TargetApproach(client, self.config)
        self.explorer = SystematicExplorer(client, self.config)

    def run_cycle(self) -> SearchLoopResult:
        context = SearchContext()
        self._transition(context, SearchStateMachineState.OBSERVE)
        self._observe(context)

        self._transition(context, SearchStateMachineState.EVALUATE_LOOK_TARGET)
        immediate_target = self._evaluate_look_target(context)
        if immediate_target is None:
            self._transition(context, SearchStateMachineState.QUERY_CANDIDATES)
            self._query_candidates(context)

        self._transition(context, SearchStateMachineState.SELECT_TARGET)
        selected = self._select_target(context)
        if selected is None:
            self._transition(context, SearchStateMachineState.EXPLORE)
            discovered = self._explore(context)
            if discovered is None:
                self._transition(context, SearchStateMachineState.FAILED)
                self._stop_all()
                context.last_failure_reason = "no_target_after_explore"
                return self._result_from_context(context)
            context.classified_targets = [discovered]
            selected = discovered
            self._transition(context, SearchStateMachineState.SELECT_TARGET)
            context.selected_target = selected

        if selected.category == "hostile_candidate":
            self._transition(context, SearchStateMachineState.AVOID_HOSTILE)
            self._avoid_hostile(context)
            return self._result_from_context(context)

        self._transition(context, SearchStateMachineState.MOVE_TO_TARGET)
        if not self._move_to_target(context):
            self._transition(context, SearchStateMachineState.RECOVER)
            self._recover_or_fail(context)
            return self._result_from_context(context)

        self._transition(context, SearchStateMachineState.ALIGN_VIEW)
        if not self._align_view(context):
            self._transition(context, SearchStateMachineState.RECOVER)
            self._recover_or_fail(context)
            return self._result_from_context(context)

        self._transition(context, SearchStateMachineState.ACT)
        self._act(context)

        self._transition(context, SearchStateMachineState.VERIFY_RESULT)
        self._verify_result(context)

        if not context.verification_ok:
            self._transition(context, SearchStateMachineState.RECOVER)
            self._recover_or_fail(context)

        return self._result_from_context(context)

    def _transition(self, context: SearchContext, next_state: SearchStateMachineState) -> None:
        self.state_machine.transition(next_state)
        context.add_transition(next_state.value)

    def _observe(self, context: SearchContext) -> None:
        context.state = self._client.get_state()
        context.look_target = self._client.get_look_target()
        context.interaction_context = self._client.get_interaction_context()
        context.hostile_nearby = context.state.nearby_entities_summary.hostile_count > 0
        context.environment_summary = self._client.get_environment_summary()
        context.terrain_summary = self._client.get_terrain_summary()
        context.biome_info = self._client.get_biome_info()

    def _evaluate_look_target(self, context: SearchContext) -> Optional[DecisionTarget]:
        immediate = self.classifier.classify_look_target(context.look_target, context.interaction_context)
        if immediate is None:
            return None
        if immediate.category in {"loot_candidate", "resource_candidate"}:
            context.classified_targets = [immediate]
            context.selected_target = immediate
            return immediate
        if immediate.category == "interactable_candidate" and immediate.can_interact:
            context.classified_targets = [immediate]
            context.selected_target = immediate
            return immediate
        if immediate.category == "hostile_candidate" and immediate.distance <= self.config.approach_stop_distance_entity + 1.0:
            context.classified_targets = [immediate]
            context.selected_target = immediate
            return immediate
        return None

    def _query_candidates(self, context: SearchContext) -> None:
        context.resource_query = self._client.query_resource_candidates(
            radius=24,
            max_results=12,
            min_confidence=0.2,
            sort_by="distance",
        )
        context.interactable_query = self._client.query_interactables_in_radius(
            radius=18,
            max_results=12,
            include_blocks=True,
            include_entities=True,
            include_loot=True,
            include_doors=True,
            include_vehicles=True,
            include_npcs=True,
            include_traders=True,
            include_locked=True,
        )
        context.entity_query = self._client.query_entities_in_radius(
            radius=24,
            max_results=12,
            include_hostile=True,
            include_npc=True,
            include_animals=True,
            include_neutral=True,
            include_dead=False,
        )
        context.classified_targets = self.classifier.classify_all(
            context.look_target,
            context.interaction_context,
            context.resource_query.candidates,
            context.interactable_query.interactables,
            context.entity_query.entities,
        )

    def _select_target(self, context: SearchContext) -> Optional[DecisionTarget]:
        best = self.priority.choose_best(
            [target for target in context.classified_targets if not self.result_memory.should_skip_target(target.target_key)],
            self.result_memory,
            hostile_nearby=context.hostile_nearby,
        )
        context.selected_target = None if best is None else best.target
        return context.selected_target

    def _move_to_target(self, context: SearchContext) -> bool:
        target = context.selected_target
        if target is None:
            context.last_failure_reason = "selected_target_missing"
            return False

        context.action_plan = self.action_selector.select(target)
        if not context.action_plan.requires_approach:
            return True

        result = self.approach.approach_with_details(target.position, target.name, target.target_kind)
        context.action_result = result
        if result.status == "unreachable":
            self.result_memory.mark_unreachable(target.target_key)
            context.last_failure_reason = result.reason
            return False
        return True

    def _align_view(self, context: SearchContext) -> bool:
        target = context.selected_target
        if target is None or target.position is None:
            context.last_failure_reason = "align_target_missing"
            return False

        aligned = self.approach.heading.align_view_to_position(
            target.position,
            max_iterations=self.config.alignment_max_attempts,
        )
        if not aligned:
            context.last_failure_reason = "align_view_failed"
            return False

        look_target = self._client.get_look_target()
        context.look_target = look_target
        if look_target.has_target and (
            (look_target.target_name or "").lower() == target.name.lower()
            or look_target.target_kind == target.target_kind
        ):
            return True

        context.last_failure_reason = "look_target_mismatch_after_align"
        return False

    def _act(self, context: SearchContext) -> None:
        target = context.selected_target
        plan = context.action_plan or (None if target is None else self.action_selector.select(target))
        context.action_plan = plan
        if target is None or plan is None:
            context.last_failure_reason = "act_without_target"
            return

        if plan.plan_kind == "interact":
            context.action_result = self._client.interact()
            self.result_memory.mark_processed(target.target_key)
            return

        if plan.plan_kind == "harvest":
            context.action_result = self._client.attack_primary(duration_ms=plan.duration_ms)
            self._stop_all()
            self.result_memory.mark_processed(target.target_key)
            return

        if plan.plan_kind == "avoid_hostile":
            self._avoid_hostile(context)
            return

    def _verify_result(self, context: SearchContext) -> None:
        target = context.selected_target
        if target is None:
            context.verification_ok = False
            context.last_failure_reason = "verify_without_target"
            return

        post_look = self._client.get_look_target()
        post_context = self._client.get_interaction_context()
        context.look_target = post_look
        context.interaction_context = post_context

        if target.category == "loot_candidate":
            changed = (post_look.target_name or "").lower() != target.name.lower() or not post_context.can_interact_now
            context.verification_ok = bool(changed)
            if not context.verification_ok:
                self.result_memory.mark_empty_loot(target.target_key)
                context.last_failure_reason = "loot_context_did_not_change"
            return

        if target.category == "resource_candidate":
            same_target = post_look.has_target and (post_look.target_name or "").lower() == target.name.lower()
            durability_changed = post_look.durability != getattr(target.payload, "durability", None)
            context.verification_ok = bool(same_target or durability_changed)
            if not context.verification_ok:
                self.result_memory.mark_failed_mine(target.target_key)
                context.last_failure_reason = "resource_state_did_not_change"
            return

        if target.category == "interactable_candidate":
            changed = (post_context.prompt_text or "Unknown") != (context.interaction_context.prompt_text or "Unknown")
            context.verification_ok = bool(changed or not post_context.can_interact_now)
            if not context.verification_ok:
                self.result_memory.mark_failed_interact(target.target_key)
                context.last_failure_reason = "interactable_context_did_not_change"
            return

        context.verification_ok = True

    def _explore(self, context: SearchContext) -> Optional[DecisionTarget]:
        payload = self.explorer.start_exploration()
        if payload is None:
            return None

        if hasattr(payload, "kind") and hasattr(payload, "interaction_action_kind"):
            targets = self.classifier.classify_interactables([payload])
        elif hasattr(payload, "entity_name"):
            targets = self.classifier.classify_entities([payload])
        else:
            targets = self.classifier.classify_resource_candidates([payload])

        return targets[0] if targets else None

    def _recover_or_fail(self, context: SearchContext) -> None:
        target = context.selected_target
        if target is None or target.position is None:
            self._transition(context, SearchStateMachineState.FAILED)
            self._stop_all()
            return

        recovery = self.approach.recovery.execute_recovery_full(
            attempt=1,
            target_pos=target.position,
            terrain_summary=context.terrain_summary or self._client.get_terrain_summary(),
            env_summary=context.environment_summary or self._client.get_environment_summary(),
            distance=target.distance,
            jump_attempts_for_target=0,
        )
        context.action_result = recovery
        if recovery.status == "unreachable":
            self.result_memory.mark_unreachable(target.target_key)
            self._transition(context, SearchStateMachineState.FAILED)
            self._stop_all()

    def _avoid_hostile(self, context: SearchContext) -> None:
        self._stop_all()
        self.approach.loco.move_backward_pulse(self.config.recovery_backward_seconds)
        self._stop_all()
        context.verification_ok = True
        context.action_plan = ActionPlan(
            plan_kind="avoid_hostile",
            command_name="avoid_hostile",
            duration_ms=0,
            requires_alignment=False,
            requires_approach=False,
            stop_distance=self.config.approach_stop_distance_entity,
            reason="hostile caused exploration pause",
        )

    def _stop_all(self) -> None:
        self._client.stop_all()

    def _result_from_context(self, context: SearchContext) -> SearchLoopResult:
        return SearchLoopResult(
            final_state=self.state_machine.state.value,
            selected_target_name=None if context.selected_target is None else context.selected_target.name,
            selected_target_category=None if context.selected_target is None else context.selected_target.category,
            action_name="none" if context.action_plan is None else context.action_plan.command_name,
            verification_ok=bool(context.verification_ok),
            failure_reason=context.last_failure_reason,
            transitions=list(context.transitions),
        )
