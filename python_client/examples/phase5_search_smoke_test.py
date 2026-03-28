from __future__ import annotations

import sys
from dataclasses import dataclass
from pathlib import Path
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
if str(SRC) not in sys.path:
    sys.path.insert(0, str(SRC))

from sevendays_bridge.decision.target_classifier import DecisionTarget
from sevendays_bridge.search.search_loop import SearchLoop


@dataclass
class Vec3:
    x: float
    y: float
    z: float


@dataclass
class Rot:
    yaw: float
    pitch: float = 0.0


class FakeClient:
    def __init__(self, mode: str):
        self.mode = mode
        self.stop_count = 0
        self.interact_count = 0
        self.attack_count = 0
        self.backward_count = 0
        self.state = SimpleNamespace(
            player=SimpleNamespace(position=Vec3(0.0, 0.0, 0.0), rotation=Rot(0.0)),
            nearby_entities_summary=SimpleNamespace(hostile_count=1 if mode == "hostile" else 0),
        )
        self.look_default = SimpleNamespace(
            has_target=False,
            target_kind="none",
            target_name="Unknown",
            target_class="Unknown",
            target_id="Unknown",
            entity_id=None,
            block_id=None,
            distance=0.0,
            position=Vec3(0.0, 0.0, 0.0),
            can_interact=False,
            interaction_prompt_text="Unknown",
            interaction_action_kind="none",
            hostile=False,
            alive=False,
            locked="unknown",
            powered="unknown",
            active="unknown",
            line_of_sight_clear=True,
            is_resource_candidate=False,
            candidate_category="unknown",
            candidate_confidence=0.0,
            likely_resource_type="unknown",
            durability="unknown",
            max_durability="unknown",
            note="",
        )
        self.look_after_action = self.look_default
        self.interaction_default = SimpleNamespace(
            has_focus_target=False,
            can_interact_now=False,
            suggested_action_kind="none",
            prompt_text="Unknown",
            target_kind="none",
            target_name="Unknown",
            distance=0.0,
            source="unknown",
            requires_precise_alignment=False,
            recommended_interact_distance_min="unknown",
            recommended_interact_distance_max="unknown",
            note="",
        )

    def get_state(self):
        return self.state

    def get_environment_summary(self):
        return SimpleNamespace(fall_hazard_ahead_hint=False, local_height_span=0.4)

    def get_terrain_summary(self):
        return SimpleNamespace(height_span=0.4)

    def get_biome_info(self):
        return SimpleNamespace(current_biome="pine_forest", hazard_hint="none")

    def get_look_target(self):
        if self.interact_count > 0 and self.mode in {"loot", "query"}:
            return self.look_default
        if self.mode == "loot":
            payload = dict(self.look_default.__dict__)
            payload.update(
                has_target=True,
                target_kind="loot",
                target_name="cntBirdnest",
                distance=2.0,
                position=Vec3(1.0, 0.0, 2.0),
                can_interact=True,
                interaction_prompt_text="Search",
                interaction_action_kind="search",
                candidate_confidence=1.0,
                likely_resource_type="loot",
            )
            return SimpleNamespace(**payload)
        if self.mode == "resource":
            payload = dict(self.look_default.__dict__)
            payload.update(
                has_target=True,
                target_kind="resource",
                target_name="treeDeadPineLeaf",
                distance=2.4,
                position=Vec3(1.0, 0.0, 3.0),
                can_interact=False,
                interaction_prompt_text="Harvest",
                interaction_action_kind="harvest",
                is_resource_candidate=True,
                candidate_category="surface_resource_node",
                candidate_confidence=0.9,
                likely_resource_type="wood",
                durability=50,
                max_durability=100,
            )
            return SimpleNamespace(**payload)
        if self.mode == "hostile":
            payload = dict(self.look_default.__dict__)
            payload.update(
                has_target=True,
                target_kind="enemy",
                target_name="zombieArlene",
                distance=2.0,
                position=Vec3(1.0, 0.0, 2.0),
                hostile=True,
                alive=True,
                interaction_prompt_text="Attack",
                interaction_action_kind="attack",
                candidate_confidence=1.0,
                likely_resource_type="enemy",
            )
            return SimpleNamespace(**payload)
        if self.mode == "query":
            return self.look_default
        return self.look_after_action

    def get_interaction_context(self):
        if self.interact_count > 0 and self.mode in {"loot", "query"}:
            return self.interaction_default
        if self.mode == "loot":
            payload = dict(self.interaction_default.__dict__)
            payload.update(
                has_focus_target=True,
                can_interact_now=True,
                suggested_action_kind="search",
                prompt_text="Search",
                target_kind="loot",
                target_name="cntBirdnest",
                distance=2.0,
                source="focus_context",
            )
            return SimpleNamespace(**payload)
        if self.mode == "resource":
            payload = dict(self.interaction_default.__dict__)
            payload.update(
                has_focus_target=True,
                can_interact_now=False,
                suggested_action_kind="harvest",
                prompt_text="Harvest",
                target_kind="resource",
                target_name="treeDeadPineLeaf",
                distance=2.4,
                source="focus_context",
            )
            return SimpleNamespace(**payload)
        if self.mode == "hostile":
            payload = dict(self.interaction_default.__dict__)
            payload.update(
                has_focus_target=True,
                can_interact_now=False,
                suggested_action_kind="none",
                prompt_text="Attack",
                target_kind="enemy",
                target_name="zombieArlene",
                distance=2.0,
                source="focus_context",
            )
            return SimpleNamespace(**payload)
        return self.interaction_default

    def query_resource_candidates(self, **kwargs):
        if self.mode == "query":
            resource = SimpleNamespace(
                name="treeDeadPineLeaf",
                block_id=101,
                position=Vec3(5.0, 0.0, 5.0),
                distance=5.0,
                candidate_category="surface_resource_node",
                candidate_confidence=0.8,
                likely_resource_type="wood",
                line_of_sight_clear=True,
                durability=100,
            )
            return SimpleNamespace(candidates=[resource], count=1)
        return SimpleNamespace(candidates=[], count=0)

    def query_interactables_in_radius(self, **kwargs):
        if self.mode == "query":
            loot = SimpleNamespace(
                kind="loot",
                id=1,
                name="cntBirdnest",
                position=Vec3(3.0, 0.0, 3.0),
                distance=3.0,
                can_interact=True,
                interaction_prompt_text="Search",
                interaction_action_kind="search",
                line_of_sight_clear=True,
            )
            return SimpleNamespace(interactables=[loot], count=1)
        return SimpleNamespace(interactables=[], count=0)

    def query_entities_in_radius(self, **kwargs):
        return SimpleNamespace(entities=[], count=0)

    def interact(self):
        self.interact_count += 1
        self.look_after_action = self.look_default
        return SimpleNamespace(accepted=True)

    def attack_primary(self, duration_ms=700):
        self.attack_count += 1
        return SimpleNamespace(accepted=True)

    def stop_all(self):
        self.stop_count += 1
        return SimpleNamespace(accepted=True)


class FakeApproach:
    def __init__(self, client):
        self.client = client
        self.heading = SimpleNamespace(align_view_to_position=lambda *args, **kwargs: True)
        self.recovery = SimpleNamespace(
            execute_recovery_full=lambda **kwargs: SimpleNamespace(status="unreachable", reason="blocked", jump_attempted=False)
        )
        self.loco = SimpleNamespace(move_backward_pulse=self._backward)

    def _backward(self, _seconds):
        self.client.backward_count += 1

    def approach_with_details(self, target_pos, target_name, target_kind):
        return SimpleNamespace(status="success", reason="ok", recovery_attempts=0, alignment_attempts=0, final_distance=2.0)


class FakeExplorer:
    def __init__(self, payload):
        self.payload = payload

    def start_exploration(self):
        return self.payload


def ensure(condition, message):
    if not condition:
        raise AssertionError(message)


def test_immediate_loot():
    client = FakeClient("loot")
    loop = SearchLoop(client)
    loop.approach = FakeApproach(client)
    result = loop.run_cycle()
    ensure(result.final_state == "VERIFY_RESULT", "loot flow should reach VERIFY_RESULT")
    ensure(result.selected_target_category == "loot_candidate", "look target loot should be selected")
    ensure(result.action_name == "use_interact", "loot should use interact")
    ensure(client.interact_count == 1, "loot test should execute one interact")


def test_immediate_resource():
    client = FakeClient("resource")
    loop = SearchLoop(client)
    loop.approach = FakeApproach(client)
    result = loop.run_cycle()
    ensure(result.selected_target_category == "resource_candidate", "resource look target should be selected")
    ensure(result.action_name == "primary_action", "resource should use primary action")
    ensure(client.attack_count == 1, "resource test should execute one primary action")


def test_hostile_avoid():
    client = FakeClient("hostile")
    loop = SearchLoop(client)
    loop.approach = FakeApproach(client)
    result = loop.run_cycle()
    ensure(result.final_state == "AVOID_HOSTILE", "hostile should move to avoid state")
    ensure(client.stop_count >= 1, "avoid hostile should stop input")
    ensure(client.backward_count == 1, "avoid hostile should perform one backward pulse")


def test_query_priority_prefers_interactable_loot():
    client = FakeClient("query")
    loop = SearchLoop(client)
    loop.approach = FakeApproach(client)
    result = loop.run_cycle()
    ensure(result.selected_target_name == "cntBirdnest", "query selection should prefer can_interact loot")
    ensure(result.action_name == "use_interact", "query-selected loot should interact")


def test_explore_when_no_candidates():
    client = FakeClient("none")
    loop = SearchLoop(client)
    loop.approach = FakeApproach(client)
    payload = SimpleNamespace(
        name="treeDeadPineLeaf",
        block_id=200,
        position=Vec3(6.0, 0.0, 6.0),
        distance=6.0,
        candidate_category="surface_resource_node",
        candidate_confidence=0.9,
        likely_resource_type="wood",
        line_of_sight_clear=True,
        durability=100,
    )
    loop.explorer = FakeExplorer(payload)
    result = loop.run_cycle()
    ensure("EXPLORE" in result.transitions, "no-candidate flow should enter EXPLORE")
    ensure(result.selected_target_category == "resource_candidate", "explore result should be classified")


def main():
    test_immediate_loot()
    test_immediate_resource()
    test_hostile_avoid()
    test_query_priority_prefers_interactable_loot()
    test_explore_when_no_candidates()
    print("Phase 5 smoke test passed.")


if __name__ == "__main__":
    main()
