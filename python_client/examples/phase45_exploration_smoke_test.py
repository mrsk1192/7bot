from __future__ import annotations

import math
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, List, Optional

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
if str(SRC) not in sys.path:
    sys.path.insert(0, str(SRC))

from sevendays_bridge.exploration.frontier_selector import FrontierSelector
from sevendays_bridge.exploration.search_grid import SearchGrid
from sevendays_bridge.exploration.search_state import SearchState
from sevendays_bridge.exploration.sector_scan import SectorScan
from sevendays_bridge.movement.heading_controller import HeadingController
from sevendays_bridge.movement.jump_decision import JumpDecision
from sevendays_bridge.movement.locomotion_controller import LocomotionController
from sevendays_bridge.movement.navigation_config import NavigationConfig
from sevendays_bridge.movement.obstacle_recovery import ObstacleRecovery
from sevendays_bridge.movement.target_approach import TargetApproach


@dataclass
class Vector3:
    x: float
    y: float
    z: float


@dataclass
class Rotation:
    yaw: float
    pitch: float = 0.0


@dataclass
class PlayerState:
    position: Vector3
    rotation: Rotation


@dataclass
class State:
    player: PlayerState


@dataclass
class LookTarget:
    has_target: bool
    target_kind: str
    target_name: str
    distance: float
    position: Vector3
    interaction_prompt_text: str
    can_interact: bool = False
    source: str = "focus_context"
    candidate_confidence: float = 0.0


@dataclass
class InteractionContext:
    has_focus_target: bool
    can_interact_now: bool
    suggested_action_kind: str
    prompt_text: str
    target_kind: str
    target_name: str
    distance: float
    source: str


@dataclass
class ResourceCandidate:
    name: str
    distance: float
    position: Vector3
    likely_resource_type: str = "stone"
    candidate_confidence: float = 0.0


@dataclass
class Interactable:
    kind: str
    name: str
    id: Any
    distance: float
    position: Vector3
    can_interact: bool = True
    interaction_prompt_text: str = "Search"
    interaction_action_kind: str = "search"


@dataclass
class Entity:
    entity_id: int
    entity_name: str
    entity_class: str
    kind: str
    distance: float
    position: Vector3
    alive: bool = True
    hostile: bool = True
    can_interact: bool = False


@dataclass
class QueryResult:
    count: int
    max_results: int
    note: str
    candidates: Optional[List[Any]] = None
    interactables: Optional[List[Any]] = None
    entities: Optional[List[Any]] = None


@dataclass
class EnvironmentSummary:
    current_biome: str = "pine_forest"
    foot_block_name: str = "terrDirt"
    foot_block_id: int = 0
    indoors_hint: Optional[bool] = False
    water_nearby_hint: Optional[bool] = False
    fall_hazard_ahead_hint: Optional[bool] = False
    local_height_span: Optional[float] = 0.8
    note: str = "ok"


@dataclass
class TerrainSummary:
    sample_center: Vector3
    sample_radius: float
    min_ground_y: float
    max_ground_y: float
    height_span: float
    foot_block_name: str = "terrDirt"
    foot_block_id: int = 0
    water_nearby_hint: Optional[bool] = False
    fall_hazard_ahead_hint: Optional[bool] = False
    indoors_hint: Optional[bool] = False
    note: str = "ok"


class FakeClient:
    def __init__(self):
        self.position = Vector3(0.0, 0.0, 0.0)
        self.yaw = 0.0
        self.pitch = 0.0
        self.forward_active = False
        self.move_log: List[str] = []
        self.look_log: List[tuple[float, float]] = []
        self.jump_count = 0
        self.scan_call_log: List[str] = []
        self.look_target_queue: List[LookTarget] = []
        self.interaction_queue: List[InteractionContext] = []
        self.look_target_factory: Optional[Callable[["FakeClient"], LookTarget]] = None
        self.interaction_factory: Optional[Callable[["FakeClient"], InteractionContext]] = None
        self.resource_query_factory: Optional[Callable[["FakeClient", dict], QueryResult]] = None
        self.interactable_query_factory: Optional[Callable[["FakeClient", dict], QueryResult]] = None
        self.entity_query_factory: Optional[Callable[["FakeClient", dict], QueryResult]] = None
        self.resource_query_result = QueryResult(0, 10, "ok", candidates=[])
        self.interactable_query_result = QueryResult(0, 10, "ok", interactables=[])
        self.entity_query_result = QueryResult(0, 10, "ok", entities=[])
        self.environment_summary = EnvironmentSummary()
        self.terrain_summary = TerrainSummary(Vector3(0.0, 0.0, 0.0), 2.0, 0.0, 0.8, 0.8)

    def get_state(self) -> State:
        if self.forward_active:
            yaw_rad = math.radians(self.yaw)
            self.position.x += math.sin(yaw_rad) * 0.18
            self.position.z += math.cos(yaw_rad) * 0.18
        return State(player=PlayerState(position=self.position, rotation=Rotation(self.yaw, self.pitch)))

    def look_delta(self, dx: float, dy: float):
        self.yaw = HeadingController.normalize_angle(self.yaw + dx)
        self.pitch += dy
        self.look_log.append((dx, dy))

    def move_forward(self, active: bool = True):
        self.forward_active = active
        self.move_log.append(f"move_forward:{active}")

    def press(self, name: str):
        self.move_log.append(f"press:{name}")
        if name == "move_forward":
            self.forward_active = True

    def release(self, name: str):
        self.move_log.append(f"release:{name}")
        if name == "move_forward":
            self.forward_active = False

    def stop_all(self):
        self.forward_active = False
        self.move_log.append("stop_all")

    def jump(self):
        self.jump_count += 1
        self.move_log.append("jump")

    def get_look_target(self):
        if self.look_target_factory is not None:
            return self.look_target_factory(self)
        if self.look_target_queue:
            return self.look_target_queue.pop(0)
        return LookTarget(False, "none", "Unknown", 999.0, self.position, "Unknown")

    def get_interaction_context(self):
        if self.interaction_factory is not None:
            return self.interaction_factory(self)
        if self.interaction_queue:
            return self.interaction_queue.pop(0)
        return InteractionContext(False, False, "none", "Unknown", "none", "Unknown", 999.0, "unknown")

    def query_resource_candidates(self, **arguments):
        self.scan_call_log.append(f"resource:{arguments.get('radius')}")
        if self.resource_query_factory is not None:
            return self.resource_query_factory(self, arguments)
        return self.resource_query_result

    def query_interactables_in_radius(self, **arguments):
        self.scan_call_log.append(f"interactable:{arguments.get('radius')}")
        if self.interactable_query_factory is not None:
            return self.interactable_query_factory(self, arguments)
        return self.interactable_query_result

    def query_entities_in_radius(self, **arguments):
        self.scan_call_log.append(f"entity:{arguments.get('radius')}")
        if self.entity_query_factory is not None:
            return self.entity_query_factory(self, arguments)
        return self.entity_query_result

    def get_environment_summary(self):
        return self.environment_summary

    def get_terrain_summary(self):
        return self.terrain_summary


def ensure(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def pairs_from_result(result) -> List[tuple[int, int]]:
    return [(entry.offset_deg, entry.pitch_deg) for entry in result.angle_results]


def test_heading_rules() -> None:
    client = FakeClient()
    controller = HeadingController(client, NavigationConfig())
    current = Vector3(0.0, 0.0, 0.0)
    large_target = Vector3(10.0, 0.0, 1.0)
    diff = controller.compute_yaw_diff(current, 0.0, large_target)
    correction = controller.apply_heading_correction(diff)
    ensure(correction.mode == "turn_in_place", "large yaw diff should turn in place")


def test_jump_rules() -> None:
    decision = JumpDecision(NavigationConfig())
    allowed = decision.evaluate(
        terrain_summary=TerrainSummary(Vector3(0.0, 0.0, 0.0), 2.0, 0.0, 0.8, 0.8),
        environment_summary=EnvironmentSummary(local_height_span=0.8, fall_hazard_ahead_hint=False),
        distance_to_target=2.0,
        recovery_attempt_index=3,
        jump_attempts_for_target=0,
        left_and_right_failed=True,
    )
    denied = decision.evaluate(
        terrain_summary=TerrainSummary(Vector3(0.0, 0.0, 0.0), 2.0, 0.0, 0.8, 0.8),
        environment_summary=EnvironmentSummary(local_height_span=0.8, fall_hazard_ahead_hint=False),
        distance_to_target=2.0,
        recovery_attempt_index=1,
        jump_attempts_for_target=0,
        left_and_right_failed=False,
    )
    ensure(allowed.should_jump, "jump should be allowed only when all conditions are satisfied")
    ensure(not denied.should_jump, "jump should be denied in normal cases")


def test_recovery_sequence() -> None:
    client = FakeClient()
    locomotion = LocomotionController(client, NavigationConfig())
    recovery = ObstacleRecovery(
        client,
        locomotion=locomotion,
        heading=HeadingController(client, NavigationConfig()),
        jump=JumpDecision(NavigationConfig()),
        config=NavigationConfig(),
    )
    target = Vector3(3.0, 0.0, 3.0)
    result = recovery.execute_recovery_full(
        attempt=1,
        target_pos=target,
        terrain_summary=client.get_terrain_summary(),
        env_summary=client.get_environment_summary(),
        distance=4.0,
        jump_attempts_for_target=1,
    )
    ensure(client.move_log[0] == "stop_all", "recovery must start with stop_all")
    ensure(result.status == "retry", "first recovery attempt should remain retryable")


def test_scan_angle_configuration() -> None:
    config = NavigationConfig()
    yaw_angles = config.generate_yaw_scan_angles()
    pitch_angles = config.generate_pitch_scan_angles()
    ensure(yaw_angles[0] == -90, "first yaw must be -90")
    ensure(yaw_angles[-1] == 90, "last yaw must be +90")
    ensure(len(yaw_angles) == 37, "yaw angle count must be 37")
    ensure(pitch_angles == [0, -15, -30, -45], "pitch order must match the required downward sweep")
    ensure(list(range(-90, 95, 5)) == yaw_angles, "yaw angles must be 5-degree steps from -90 to +90")
    ensure(yaw_angles[:5] == [-90, -85, -80, -75, -70], "yaw order must be one-way, not alternating")


def test_sector_scan_full_order() -> None:
    client = FakeClient()
    config = NavigationConfig()
    result = SectorScan(client, config).run_scan(stop_on_near_hostile=False)
    yaw_angles = config.generate_yaw_scan_angles()
    pitch_angles = config.generate_pitch_scan_angles()
    expected_steps = len(yaw_angles) * len(pitch_angles)
    ensure(len(result.angle_results) == expected_steps, "scan should cover every yaw/pitch combination")
    pairs = pairs_from_result(result)
    ensure(pairs[:4] == [(-90, 0), (-90, -15), (-90, -30), (-90, -45)], "first four views must cover the first yaw with all pitch values")
    ensure(pairs[4:8] == [(-85, 0), (-85, -15), (-85, -30), (-85, -45)], "second yaw must start only after the first yaw pitch group completes")
    ensure(pairs[-1] == (90, -45), "final scan view must be (+90, -45)")
    ensure(client.scan_call_log.count("resource:10.0") == expected_steps, "resource query must run at every scan view")
    ensure(client.scan_call_log.count("interactable:10.0") == expected_steps, "interactable query must run at every scan view")
    ensure(client.scan_call_log.count("entity:10.0") == expected_steps, "entity query must run at every scan view")
    for yaw in (-90, -85, -80, 0, 90):
        yaw_pitches = [pitch for offset, pitch in pairs if offset == yaw]
        ensure(yaw_pitches == [0, -15, -30, -45], f"pitch must reset for every yaw; failed at yaw={yaw}")


def test_near_ground_interactable_scan() -> None:
    client = FakeClient()
    config = NavigationConfig()

    def interactable_factory(fake: FakeClient, _arguments: dict) -> QueryResult:
        interactable = Interactable("loot", "birdnest", 1, 3.0, Vector3(2.0, 0.0, 2.0))
        return QueryResult(1, 10, "ok", interactables=[interactable])

    def look_factory(fake: FakeClient) -> LookTarget:
        if fake.pitch <= -25.0:
            return LookTarget(True, "loot", "birdnest", 3.0, Vector3(2.0, 0.0, 2.0), "Search", True, candidate_confidence=1.0)
        return LookTarget(False, "none", "Unknown", 999.0, fake.position, "Unknown")

    def interaction_factory(fake: FakeClient) -> InteractionContext:
        if fake.pitch <= -25.0:
            return InteractionContext(True, True, "search", "Search", "loot", "birdnest", 3.0, "focus_context")
        return InteractionContext(False, False, "none", "Unknown", "none", "Unknown", 999.0, "unknown")

    client.interactable_query_factory = interactable_factory
    client.look_target_factory = look_factory
    client.interaction_factory = interaction_factory

    result = SectorScan(client, config).run_scan(stop_on_near_hostile=False)
    pairs = pairs_from_result(result)
    ensure(pairs[:4] == [(-90, 0), (-90, -15), (-90, -30), (-90, -45)], "near-ground interactable scan must not stop at pitch 0")
    ensure(len(result.angle_results) == 4, "scan may stop after the first yaw group once a can-interact loot target is confirmed")
    ensure(result.stop_reason == "can_interact_loot_detected", "interactable stop reason must reflect can-interact loot")
    ensure(any(target.target_name == "birdnest" for target in result.look_targets), "downward pitch should reveal the near-ground loot target")
    ensure(len(result.priority_interactables) >= 1, "near-ground interactables should be prioritized when found below the horizon")


def test_near_ground_resource_scan() -> None:
    client = FakeClient()
    config = NavigationConfig()

    def resource_factory(fake: FakeClient, _arguments: dict) -> QueryResult:
        if fake.pitch <= -25.0:
            resource = ResourceCandidate("surfaceStone", 4.0, Vector3(1.0, 0.0, 2.0), likely_resource_type="stone", candidate_confidence=0.9)
            return QueryResult(1, 10, "ok", candidates=[resource])
        return QueryResult(0, 10, "ok", candidates=[])

    client.resource_query_factory = resource_factory
    result = SectorScan(client, config).run_scan(stop_on_near_hostile=False)
    pairs = pairs_from_result(result)
    ensure(pairs[:4] == [(-90, 0), (-90, -15), (-90, -30), (-90, -45)], "resource scan must keep checking downward pitch values")
    ensure(result.stop_reason == "high_confidence_resource_detected", "resource stop reason must reflect a high-confidence resource")
    ensure(len(result.priority_resources) >= 1, "near-ground resources should be promoted for follow-up exploration")
    ensure(any(resource.name == "surfaceStone" for resource in result.resources), "resource candidate should be recorded once a downward pitch sees it")


def test_frontier_priority() -> None:
    grid = SearchGrid(cell_size=10.0)
    selector = FrontierSelector(grid, NavigationConfig())
    grid.ensure_adjacent_cells(0, 0)
    grid.set_state(0, 1, SearchState.SCANNED)
    next_cell = selector.get_next_cell(0.0, 0.0, 0.0)
    ensure(next_cell == (-1, 1), "left-forward should be selected after forward is no longer unknown")


def test_target_approach() -> None:
    client = FakeClient()
    approach = TargetApproach(client, NavigationConfig())
    target = Vector3(0.0, 0.0, 2.6)
    client.look_target_queue = [
        LookTarget(True, "loot", "birdnest", 2.4, target, "Search", True),
        LookTarget(True, "loot", "birdnest", 2.4, target, "Search", True),
    ]
    result = approach.approach_with_details(target, "birdnest", "loot")
    ensure(result.status == "success", "approach should finish successfully for a straight target")
    ensure(any(entry in {"move_forward:True", "press:move_forward"} for entry in client.move_log), "approach must use walking input")


def main() -> int:
    test_heading_rules()
    test_jump_rules()
    test_recovery_sequence()
    test_scan_angle_configuration()
    test_sector_scan_full_order()
    test_near_ground_interactable_scan()
    test_near_ground_resource_scan()
    test_frontier_priority()
    test_target_approach()
    print("Phase 4.5 downward scan smoke test passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
