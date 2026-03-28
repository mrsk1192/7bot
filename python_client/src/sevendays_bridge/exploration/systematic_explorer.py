from __future__ import annotations

from dataclasses import dataclass
from typing import Any, List, Optional

from sevendays_bridge.exploration.frontier_selector import FrontierSelector
from sevendays_bridge.exploration.search_grid import SearchGrid
from sevendays_bridge.exploration.search_state import SearchState
from sevendays_bridge.exploration.sector_scan import SectorScan
from sevendays_bridge.movement.navigation_config import NavigationConfig
from sevendays_bridge.movement.target_approach import TargetApproach


@dataclass(frozen=True)
class ExplorerCandidate:
    category: str
    action_kind: str
    distance: float
    payload: Any


class SystematicExplorer:
    """Deterministic local-scan-first explorer with forward-priority grid expansion."""

    def __init__(self, client, config: Optional[NavigationConfig] = None):
        self._client = client
        self.config = config or NavigationConfig()
        self.grid = SearchGrid(cell_size=self.config.cell_size_meters)
        self.frontier = FrontierSelector(self.grid, self.config)
        self.scanner = SectorScan(client, self.config)
        self.approach = TargetApproach(client, self.config)

    def _wrap_resource(self, resource) -> ExplorerCandidate:
        return ExplorerCandidate("resource", resource.likely_resource_type, resource.distance, resource)

    def _wrap_interactable(self, interactable) -> ExplorerCandidate:
        return ExplorerCandidate(interactable.kind, interactable.interaction_action_kind, interactable.distance, interactable)

    def _wrap_entity(self, entity) -> ExplorerCandidate:
        return ExplorerCandidate(entity.kind, "attack" if entity.hostile else "observe", entity.distance, entity)

    def _pick_best_candidate(self, candidates: List[ExplorerCandidate]) -> Optional[ExplorerCandidate]:
        if not candidates:
            return None

        def priority(candidate: ExplorerCandidate):
            payload = candidate.payload
            can_interact = getattr(payload, "can_interact", False)
            if candidate.category in {"loot", "container"} and can_interact:
                return (0, candidate.distance)
            if candidate.category == "resource" and candidate.distance <= 8.0:
                return (1, candidate.distance)
            if can_interact:
                return (2, candidate.distance)
            if candidate.category == "resource":
                return (3, candidate.distance)
            return (4, candidate.distance)

        return sorted(candidates, key=priority)[0]

    def _gather_local_candidates(self, radius: float) -> List[ExplorerCandidate]:
        results: List[ExplorerCandidate] = []
        for interactable in self._client.query_interactables_in_radius(
            radius=radius,
            max_results=12,
            include_blocks=True,
            include_entities=True,
            include_loot=True,
            include_doors=True,
            include_vehicles=True,
            include_npcs=True,
            include_traders=True,
            include_locked=True,
        ).interactables:
            results.append(self._wrap_interactable(interactable))

        for resource in self._client.query_resource_candidates(
            radius=radius,
            max_results=12,
            min_confidence=0.2,
            sort_by="distance",
        ).candidates:
            results.append(self._wrap_resource(resource))

        for entity in self._client.query_entities_in_radius(
            radius=radius,
            max_results=12,
            include_hostile=True,
            include_npc=True,
            include_animals=True,
            include_neutral=True,
            include_dead=False,
        ).entities:
            results.append(self._wrap_entity(entity))
        return results

    def _build_cell_target(self, x: float, y: float, z: float):
        class PositionStub:
            def __init__(self, px: float, py: float, pz: float):
                self.x = px
                self.y = py
                self.z = pz

        return PositionStub(x, y, z)

    def start_exploration(self) -> Any:
        while True:
            local_candidate = self._pick_best_candidate(self._gather_local_candidates(self.config.local_scan_radius))
            if local_candidate is not None:
                return local_candidate.payload

            scan_result = self.scanner.run_scan()
            scan_candidates: List[ExplorerCandidate] = []
            for interactable in scan_result.priority_interactables:
                scan_candidates.append(self._wrap_interactable(interactable))
            for resource in scan_result.priority_resources:
                scan_candidates.append(self._wrap_resource(resource))
            for interactable in scan_result.interactables:
                scan_candidates.append(self._wrap_interactable(interactable))
            for resource in scan_result.resources:
                scan_candidates.append(self._wrap_resource(resource))
            for entity in scan_result.entities:
                scan_candidates.append(self._wrap_entity(entity))

            best_scan_candidate = self._pick_best_candidate(scan_candidates)
            state = self._client.get_state()
            player = state.player
            if player is None or player.position is None:
                return None

            cx, cz = self.grid.get_cell_coords(player.position.x, player.position.z)
            self.grid.ensure_adjacent_cells(cx, cz)
            self.grid.set_state(cx, cz, SearchState.SCANNED)

            if best_scan_candidate is not None:
                self.grid.mark_candidate_found(cx, cz)
                return best_scan_candidate.payload

            yaw = 0.0 if player.rotation is None or player.rotation.yaw is None else player.rotation.yaw
            next_cell = self.frontier.get_next_cell(player.position.x, player.position.z, yaw)
            if next_cell is None:
                return None

            self.grid.set_state(next_cell[0], next_cell[1], SearchState.SCHEDULED)
            target_x, target_z = self.grid.get_cell_center(next_cell[0], next_cell[1])
            target_pos = self._build_cell_target(target_x, player.position.y, target_z)
            result = self.approach.approach_with_details(target_pos, "CellCenter", "terrain")
            if result.status == "unreachable":
                self.grid.set_state(next_cell[0], next_cell[1], SearchState.UNREACHABLE)
            else:
                self.grid.set_state(next_cell[0], next_cell[1], SearchState.VISITED)
