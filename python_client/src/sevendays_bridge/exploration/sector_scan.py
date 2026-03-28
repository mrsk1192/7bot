from __future__ import annotations

import time
from dataclasses import dataclass, field
from typing import List, Optional

from sevendays_bridge.movement.heading_controller import HeadingController
from sevendays_bridge.movement.navigation_config import NavigationConfig


@dataclass
class ScanAngleObservation:
    offset_deg: int
    pitch_deg: int
    yaw: float
    pitch: float
    look_target: object
    interaction_context: object
    resource_query: object
    interactable_query: object
    entity_query: object


@dataclass
class SectorScanResult:
    angle_results: List[ScanAngleObservation] = field(default_factory=list)
    look_targets: List[object] = field(default_factory=list)
    interactables: List[object] = field(default_factory=list)
    resources: List[object] = field(default_factory=list)
    entities: List[object] = field(default_factory=list)
    priority_interactables: List[object] = field(default_factory=list)
    priority_resources: List[object] = field(default_factory=list)
    stop_reason: Optional[str] = None


class SectorScan:
    """Fixed-order yaw and downward-pitch scan used for deterministic exploration."""

    def __init__(self, client, config: Optional[NavigationConfig] = None):
        self._client = client
        self.config = config or NavigationConfig()
        self.heading = HeadingController(client, self.config)

    @staticmethod
    def _dedupe_key_from_position(position) -> str:
        if position is None:
            return "none"
        return f"{position.x:.3f}:{position.y:.3f}:{position.z:.3f}"

    @staticmethod
    def _kind_is_loot(kind: str) -> bool:
        return (kind or "").lower() in {"loot", "container"}

    @staticmethod
    def _kind_is_resource(kind: str) -> bool:
        return (kind or "").lower() == "resource"

    def _scan_settle_seconds(self) -> float:
        return max(0.03, min(0.1, self.config.scan_settle_delay_ms / 1000.0))

    def _query_all_sources(self):
        look_target = self._client.get_look_target()
        interaction_context = self._client.get_interaction_context()
        resource_query = self._client.query_resource_candidates(
            radius=self.config.sector_scan_radius,
            max_results=10,
            sort_by="distance",
        )
        interactable_query = self._client.query_interactables_in_radius(
            radius=self.config.sector_scan_radius,
            max_results=10,
            include_blocks=True,
            include_entities=True,
            include_loot=True,
            include_doors=True,
            include_vehicles=True,
            include_npcs=True,
            include_traders=True,
            include_locked=True,
        )
        entity_query = self._client.query_entities_in_radius(
            radius=self.config.sector_scan_radius,
            max_results=10,
            include_hostile=True,
            include_npc=True,
            include_animals=True,
            include_neutral=True,
            include_dead=False,
        )
        return look_target, interaction_context, resource_query, interactable_query, entity_query

    def _append_unique(self, collection: List[object], seen_keys: set[str], key: str, item: object) -> None:
        if key not in seen_keys:
            seen_keys.add(key)
            collection.append(item)

    def _record_observation(
        self,
        result: SectorScanResult,
        seen_look: set[str],
        seen_resources: set[str],
        seen_interactables: set[str],
        seen_entities: set[str],
        seen_priority_resources: set[str],
        seen_priority_interactables: set[str],
        observation: ScanAngleObservation,
    ) -> None:
        look_target = observation.look_target
        if getattr(look_target, "has_target", False):
            key = (
                f"{getattr(look_target, 'target_kind', 'unknown')}:"
                f"{getattr(look_target, 'target_name', 'Unknown')}:"
                f"{self._dedupe_key_from_position(getattr(look_target, 'position', None))}"
            )
            self._append_unique(result.look_targets, seen_look, key, look_target)

        for candidate in getattr(observation.resource_query, "candidates", []):
            key = f"{getattr(candidate, 'name', 'Unknown')}:{self._dedupe_key_from_position(getattr(candidate, 'position', None))}"
            self._append_unique(result.resources, seen_resources, key, candidate)
            if (
                self.config.near_ground_priority_enabled
                and observation.pitch_deg < 0
                and float(getattr(candidate, "distance", 999.0) or 999.0) <= self.config.near_ground_distance_threshold
            ):
                self._append_unique(result.priority_resources, seen_priority_resources, key, candidate)

        for interactable in getattr(observation.interactable_query, "interactables", []):
            key = (
                f"{getattr(interactable, 'id', 'unknown')}:"
                f"{getattr(interactable, 'name', 'Unknown')}:"
                f"{self._dedupe_key_from_position(getattr(interactable, 'position', None))}"
            )
            self._append_unique(result.interactables, seen_interactables, key, interactable)
            if (
                self.config.near_ground_priority_enabled
                and observation.pitch_deg < 0
                and float(getattr(interactable, "distance", 999.0) or 999.0) <= self.config.near_ground_distance_threshold
            ):
                self._append_unique(result.priority_interactables, seen_priority_interactables, key, interactable)

        for entity in getattr(observation.entity_query, "entities", []):
            key = (
                f"{getattr(entity, 'entity_id', 'unknown')}:"
                f"{getattr(entity, 'entity_name', 'Unknown')}:"
                f"{self._dedupe_key_from_position(getattr(entity, 'position', None))}"
            )
            self._append_unique(result.entities, seen_entities, key, entity)

    def _should_stop_after_yaw(self, observations: List[ScanAngleObservation], stop_on_near_hostile: bool) -> Optional[str]:
        for observation in observations:
            look_target = observation.look_target
            interaction_context = observation.interaction_context
            look_kind = (getattr(look_target, "target_kind", "none") or "none").lower()
            can_interact_now = bool(
                getattr(look_target, "can_interact", False)
                or getattr(interaction_context, "can_interact_now", False)
            )
            if self._kind_is_loot(look_kind) and can_interact_now:
                return "can_interact_loot_detected"

        for observation in observations:
            look_target = observation.look_target
            look_kind = (getattr(look_target, "target_kind", "none") or "none").lower()
            look_confidence = float(getattr(look_target, "candidate_confidence", 0.0) or 0.0)
            if self._kind_is_resource(look_kind) and look_confidence >= 0.8:
                return "high_confidence_resource_detected"
            for candidate in getattr(observation.resource_query, "candidates", []):
                confidence = float(getattr(candidate, "candidate_confidence", 0.0) or 0.0)
                if confidence >= 0.8:
                    return "high_confidence_resource_detected"

        if stop_on_near_hostile:
            for observation in observations:
                for entity in getattr(observation.entity_query, "entities", []):
                    if bool(getattr(entity, "hostile", False)) and float(getattr(entity, "distance", 999.0) or 999.0) <= self.config.near_ground_distance_threshold:
                        return "near_hostile_detected"

        return None

    def run_scan(self, stop_on_near_hostile: bool = True) -> SectorScanResult:
        state = self._client.get_state()
        player = state.player
        if player is None or player.position is None or player.rotation is None or player.rotation.yaw is None:
            return SectorScanResult()

        base_yaw = player.rotation.yaw
        base_pitch = 0.0 if player.rotation.pitch is None else player.rotation.pitch
        yaw_angles = self.config.generate_yaw_scan_angles()
        pitch_angles = self.config.generate_pitch_scan_angles()
        settle_seconds = self._scan_settle_seconds()
        seen_look: set[str] = set()
        seen_resources: set[str] = set()
        seen_interactables: set[str] = set()
        seen_entities: set[str] = set()
        seen_priority_resources: set[str] = set()
        seen_priority_interactables: set[str] = set()
        result = SectorScanResult()

        for offset in yaw_angles:
            target_yaw = self.heading.normalize_angle(base_yaw + offset)
            yaw_observations: List[ScanAngleObservation] = []

            for pitch_offset in pitch_angles:
                target_pitch = base_pitch + pitch_offset
                self.heading.turn_to_view(
                    target_yaw_deg=target_yaw,
                    target_pitch_deg=target_pitch,
                    tolerance_yaw_deg=2.0,
                    tolerance_pitch_deg=2.0,
                    max_iterations=12,
                )
                time.sleep(settle_seconds)

                look_target, interaction_context, resource_query, interactable_query, entity_query = self._query_all_sources()
                observation = ScanAngleObservation(
                    offset_deg=offset,
                    pitch_deg=pitch_offset,
                    yaw=target_yaw,
                    pitch=target_pitch,
                    look_target=look_target,
                    interaction_context=interaction_context,
                    resource_query=resource_query,
                    interactable_query=interactable_query,
                    entity_query=entity_query,
                )
                result.angle_results.append(observation)
                yaw_observations.append(observation)
                self._record_observation(
                    result,
                    seen_look,
                    seen_resources,
                    seen_interactables,
                    seen_entities,
                    seen_priority_resources,
                    seen_priority_interactables,
                    observation,
                )

            stop_reason = self._should_stop_after_yaw(yaw_observations, stop_on_near_hostile=stop_on_near_hostile)
            if stop_reason is not None:
                result.stop_reason = stop_reason
                break

        self.heading.turn_to_view(base_yaw, base_pitch, tolerance_yaw_deg=2.0, tolerance_pitch_deg=2.0, max_iterations=12)
        return result
