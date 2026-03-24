from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional


@dataclass
class Vector3State:
    x: float
    y: float
    z: float

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> Optional["Vector3State"]:
        if payload is None:
            return None
        return cls(x=float(payload["X"]), y=float(payload["Y"]), z=float(payload["Z"]))


@dataclass
class RotationState:
    yaw: Optional[float]
    pitch: Optional[float]

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> Optional["RotationState"]:
        if payload is None:
            return None
        yaw = payload.get("Yaw")
        pitch = payload.get("Pitch")
        return cls(
            yaw=None if yaw is None else float(yaw),
            pitch=None if pitch is None else float(pitch),
        )


@dataclass
class LookTargetObservation:
    has_target: bool
    source: str
    target_kind: str
    target_name: str
    target_class: str
    target_id: Any
    entity_id: Any
    block_id: Any
    distance: float
    position: Optional[Vector3State]
    can_interact: bool
    interaction_prompt_text: str
    interaction_action_kind: str
    hostile: bool
    alive: bool
    locked: Any
    powered: Any
    active: Any
    line_of_sight_clear: Any
    is_resource_candidate: bool
    candidate_category: str
    candidate_confidence: float
    likely_resource_type: str
    durability: Any
    max_durability: Any
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "LookTargetObservation":
        payload = payload or {}
        return cls(
            has_target=bool(payload.get("HasTarget", False)),
            source=str(payload.get("Source", "unknown")),
            target_kind=str(payload.get("TargetKind", "none")),
            target_name=str(payload.get("TargetName", "Unknown")),
            target_class=str(payload.get("TargetClass", "Unknown")),
            target_id=payload.get("TargetId"),
            entity_id=payload.get("EntityId"),
            block_id=payload.get("BlockId"),
            distance=float(payload.get("Distance", 0.0)),
            position=Vector3State.from_dict(payload.get("Position")),
            can_interact=bool(payload.get("CanInteract", False)),
            interaction_prompt_text=str(payload.get("InteractionPromptText", "Unknown")),
            interaction_action_kind=str(payload.get("InteractionActionKind", "none")),
            hostile=bool(payload.get("Hostile", False)),
            alive=bool(payload.get("Alive", False)),
            locked=payload.get("Locked"),
            powered=payload.get("Powered"),
            active=payload.get("Active"),
            line_of_sight_clear=payload.get("LineOfSightClear"),
            is_resource_candidate=bool(payload.get("IsResourceCandidate", False)),
            candidate_category=str(payload.get("CandidateCategory", "unknown")),
            candidate_confidence=float(payload.get("CandidateConfidence", 0.0)),
            likely_resource_type=str(payload.get("LikelyResourceType", "unknown")),
            durability=payload.get("Durability"),
            max_durability=payload.get("MaxDurability"),
            note=str(payload.get("Note", "")),
        )


@dataclass
class InteractionContextObservation:
    has_focus_target: bool
    can_interact_now: bool
    suggested_action_kind: str
    prompt_text: str
    target_kind: str
    target_name: str
    distance: float
    source: str
    requires_precise_alignment: bool
    recommended_interact_distance_min: Any
    recommended_interact_distance_max: Any
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "InteractionContextObservation":
        payload = payload or {}
        return cls(
            has_focus_target=bool(payload.get("HasFocusTarget", False)),
            can_interact_now=bool(payload.get("CanInteractNow", False)),
            suggested_action_kind=str(payload.get("SuggestedActionKind", "unknown")),
            prompt_text=str(payload.get("PromptText", "Unknown")),
            target_kind=str(payload.get("TargetKind", "none")),
            target_name=str(payload.get("TargetName", "Unknown")),
            distance=float(payload.get("Distance", 0.0)),
            source=str(payload.get("Source", "unknown")),
            requires_precise_alignment=bool(payload.get("RequiresPreciseAlignment", False)),
            recommended_interact_distance_min=payload.get("RecommendedInteractDistanceMin"),
            recommended_interact_distance_max=payload.get("RecommendedInteractDistanceMax"),
            note=str(payload.get("Note", "")),
        )


@dataclass
class ObservationAvailability:
    look_target_available: bool
    interaction_context_available: bool
    resource_query_available: bool
    interactable_query_available: bool
    entity_query_available: bool
    environment_summary_available: bool
    biome_info_available: bool
    terrain_summary_available: bool
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "ObservationAvailability":
        payload = payload or {}
        return cls(
            look_target_available=bool(payload.get("LookTargetAvailable", False)),
            interaction_context_available=bool(payload.get("InteractionContextAvailable", False)),
            resource_query_available=bool(payload.get("ResourceQueryAvailable", False)),
            interactable_query_available=bool(payload.get("InteractableQueryAvailable", False)),
            entity_query_available=bool(payload.get("EntityQueryAvailable", False)),
            environment_summary_available=bool(payload.get("EnvironmentSummaryAvailable", False)),
            biome_info_available=bool(payload.get("BiomeInfoAvailable", False)),
            terrain_summary_available=bool(payload.get("TerrainSummaryAvailable", False)),
            note=str(payload.get("Note", "")),
        )


@dataclass
class ResourceObservation:
    player_position: Optional[Vector3State]
    player_rotation: Optional[RotationState]
    biome: str
    look_target: LookTargetObservation
    interaction_context: InteractionContextObservation
    availability: ObservationAvailability

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "ResourceObservation":
        payload = payload or {}
        return cls(
            player_position=Vector3State.from_dict(payload.get("PlayerPosition")),
            player_rotation=RotationState.from_dict(payload.get("PlayerRotation")),
            biome=str(payload.get("Biome", "Unknown")),
            look_target=LookTargetObservation.from_dict(payload.get("LookTarget")),
            interaction_context=InteractionContextObservation.from_dict(payload.get("InteractionContext")),
            availability=ObservationAvailability.from_dict(payload.get("Availability")),
        )


@dataclass
class ResourceCandidateObservation:
    name: str
    block_id: Any
    position: Optional[Vector3State]
    distance: float
    candidate_category: str
    candidate_confidence: float
    likely_resource_type: str
    is_exposed: bool
    biome: str
    line_of_sight_clear: Any
    reachable_hint: Any
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "ResourceCandidateObservation":
        payload = payload or {}
        return cls(
            name=str(payload.get("Name", "Unknown")),
            block_id=payload.get("BlockId"),
            position=Vector3State.from_dict(payload.get("Position")),
            distance=float(payload.get("Distance", 0.0)),
            candidate_category=str(payload.get("CandidateCategory", "unknown")),
            candidate_confidence=float(payload.get("CandidateConfidence", 0.0)),
            likely_resource_type=str(payload.get("LikelyResourceType", "unknown")),
            is_exposed=bool(payload.get("IsExposed", False)),
            biome=str(payload.get("Biome", "Unknown")),
            line_of_sight_clear=payload.get("LineOfSightClear"),
            reachable_hint=payload.get("ReachableHint"),
            note=str(payload.get("Note", "")),
        )


@dataclass
class InteractableObservation:
    kind: str
    id: Any
    name: str
    position: Optional[Vector3State]
    distance: float
    can_interact: bool
    interaction_prompt_text: str
    interaction_action_kind: str
    locked: Any
    powered: Any
    active: Any
    line_of_sight_clear: Any
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "InteractableObservation":
        payload = payload or {}
        return cls(
            kind=str(payload.get("Kind", "unknown")),
            id=payload.get("Id"),
            name=str(payload.get("Name", "Unknown")),
            position=Vector3State.from_dict(payload.get("Position")),
            distance=float(payload.get("Distance", 0.0)),
            can_interact=bool(payload.get("CanInteract", False)),
            interaction_prompt_text=str(payload.get("InteractionPromptText", "Unknown")),
            interaction_action_kind=str(payload.get("InteractionActionKind", "unknown")),
            locked=payload.get("Locked"),
            powered=payload.get("Powered"),
            active=payload.get("Active"),
            line_of_sight_clear=payload.get("LineOfSightClear"),
            note=str(payload.get("Note", "")),
        )


@dataclass
class EntityObservation:
    entity_id: Any
    entity_name: str
    entity_class: str
    kind: str
    position: Optional[Vector3State]
    distance: float
    alive: bool
    hostile: bool
    can_interact: bool
    current_targeting_player: Any
    line_of_sight_clear: Any
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "EntityObservation":
        payload = payload or {}
        return cls(
            entity_id=payload.get("EntityId"),
            entity_name=str(payload.get("EntityName", "Unknown")),
            entity_class=str(payload.get("EntityClass", "Unknown")),
            kind=str(payload.get("Kind", "unknown")),
            position=Vector3State.from_dict(payload.get("Position")),
            distance=float(payload.get("Distance", 0.0)),
            alive=bool(payload.get("Alive", False)),
            hostile=bool(payload.get("Hostile", False)),
            can_interact=bool(payload.get("CanInteract", False)),
            current_targeting_player=payload.get("CurrentTargetingPlayer"),
            line_of_sight_clear=payload.get("LineOfSightClear"),
            note=str(payload.get("Note", "")),
        )


@dataclass
class EnvironmentSummary:
    current_biome: str
    foot_block_name: str
    foot_block_id: Any
    indoors_hint: Optional[bool]
    water_nearby_hint: Optional[bool]
    fall_hazard_ahead_hint: Optional[bool]
    local_height_span: Optional[float]
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "EnvironmentSummary":
        payload = payload or {}
        return cls(
            current_biome=str(payload.get("CurrentBiome", "Unknown")),
            foot_block_name=str(payload.get("FootBlockName", "Unknown")),
            foot_block_id=payload.get("FootBlockId"),
            indoors_hint=_maybe_bool(payload.get("IndoorsHint")),
            water_nearby_hint=_maybe_bool(payload.get("WaterNearbyHint")),
            fall_hazard_ahead_hint=_maybe_bool(payload.get("FallHazardAheadHint")),
            local_height_span=_maybe_float(payload.get("LocalHeightSpan")),
            note=str(payload.get("Note", "")),
        )


@dataclass
class BiomeInfo:
    current_biome: str
    biome_intensity: Any
    indoors_hint: Optional[bool]
    hazard_hint: str
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "BiomeInfo":
        payload = payload or {}
        return cls(
            current_biome=str(payload.get("CurrentBiome", "Unknown")),
            biome_intensity=payload.get("BiomeIntensity"),
            indoors_hint=_maybe_bool(payload.get("IndoorsHint")),
            hazard_hint=str(payload.get("HazardHint", "Unknown")),
            note=str(payload.get("Note", "")),
        )


@dataclass
class TerrainSummary:
    sample_center: Optional[Vector3State]
    sample_radius: float
    min_ground_y: float
    max_ground_y: float
    height_span: float
    foot_block_name: str
    foot_block_id: Any
    water_nearby_hint: Optional[bool]
    fall_hazard_ahead_hint: Optional[bool]
    indoors_hint: Optional[bool]
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "TerrainSummary":
        payload = payload or {}
        return cls(
            sample_center=Vector3State.from_dict(payload.get("SampleCenter")),
            sample_radius=float(payload.get("SampleRadius", 0.0)),
            min_ground_y=float(payload.get("MinGroundY", 0.0)),
            max_ground_y=float(payload.get("MaxGroundY", 0.0)),
            height_span=float(payload.get("HeightSpan", 0.0)),
            foot_block_name=str(payload.get("FootBlockName", "Unknown")),
            foot_block_id=payload.get("FootBlockId"),
            water_nearby_hint=_maybe_bool(payload.get("WaterNearbyHint")),
            fall_hazard_ahead_hint=_maybe_bool(payload.get("FallHazardAheadHint")),
            indoors_hint=_maybe_bool(payload.get("IndoorsHint")),
            note=str(payload.get("Note", "")),
        )


@dataclass
class ResourceCandidateQueryResult:
    center: Optional[Vector3State]
    radius: float
    max_results: int
    sort_by: str
    count: int
    ignored_filters: List[str]
    candidates: List[ResourceCandidateObservation]
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "ResourceCandidateQueryResult":
        payload = payload or {}
        return cls(
            center=Vector3State.from_dict(payload.get("Center")),
            radius=float(payload.get("Radius", 0.0)),
            max_results=int(payload.get("MaxResults", 0)),
            sort_by=str(payload.get("SortBy", "distance")),
            count=int(payload.get("Count", 0)),
            ignored_filters=[str(item) for item in payload.get("IgnoredFilters", [])],
            candidates=[ResourceCandidateObservation.from_dict(item) for item in payload.get("Candidates", [])],
            note=str(payload.get("Note", "")),
        )


@dataclass
class InteractableQueryResult:
    center: Optional[Vector3State]
    radius: float
    max_results: int
    count: int
    ignored_filters: List[str]
    interactables: List[InteractableObservation]
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "InteractableQueryResult":
        payload = payload or {}
        return cls(
            center=Vector3State.from_dict(payload.get("Center")),
            radius=float(payload.get("Radius", 0.0)),
            max_results=int(payload.get("MaxResults", 0)),
            count=int(payload.get("Count", 0)),
            ignored_filters=[str(item) for item in payload.get("IgnoredFilters", [])],
            interactables=[InteractableObservation.from_dict(item) for item in payload.get("Interactables", [])],
            note=str(payload.get("Note", "")),
        )


@dataclass
class EntityQueryResult:
    center: Optional[Vector3State]
    radius: float
    max_results: int
    count: int
    ignored_filters: List[str]
    entities: List[EntityObservation]
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "EntityQueryResult":
        payload = payload or {}
        return cls(
            center=Vector3State.from_dict(payload.get("Center")),
            radius=float(payload.get("Radius", 0.0)),
            max_results=int(payload.get("MaxResults", 0)),
            count=int(payload.get("Count", 0)),
            ignored_filters=[str(item) for item in payload.get("IgnoredFilters", [])],
            entities=[EntityObservation.from_dict(item) for item in payload.get("Entities", [])],
            note=str(payload.get("Note", "")),
        )


@dataclass
class NearbyResourceCandidatesSummary:
    count: int
    top_candidates: List[ResourceCandidateObservation]

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "NearbyResourceCandidatesSummary":
        payload = payload or {}
        return cls(
            count=int(payload.get("Count", 0)),
            top_candidates=[ResourceCandidateObservation.from_dict(item) for item in payload.get("TopCandidates", [])],
        )


@dataclass
class NearbyInteractablesSummary:
    count: int
    top_interactables: List[InteractableObservation]

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "NearbyInteractablesSummary":
        payload = payload or {}
        return cls(
            count=int(payload.get("Count", 0)),
            top_interactables=[InteractableObservation.from_dict(item) for item in payload.get("TopInteractables", [])],
        )


@dataclass
class NearbyEntitiesSummary:
    hostile_count: int
    npc_count: int
    nearest_hostile_distance: Optional[float]
    top_entities: List[EntityObservation]

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "NearbyEntitiesSummary":
        payload = payload or {}
        return cls(
            hostile_count=int(payload.get("HostileCount", 0)),
            npc_count=int(payload.get("NpcCount", 0)),
            nearest_hostile_distance=_maybe_float(payload.get("NearestHostileDistance")),
            top_entities=[EntityObservation.from_dict(item) for item in payload.get("TopEntities", [])],
        )


@dataclass
class PlayerState:
    position: Optional[Vector3State]
    rotation: Optional[RotationState]
    alive: Optional[bool]
    is_dead: Optional[bool]
    death_screen_visible: Optional[bool]
    respawn_available: Optional[bool]
    respawn_cooldown_seconds: Optional[float]
    last_death_time: Optional[str]
    last_death_position: Optional[Vector3State]
    bedroll_spawn_available: Optional[bool]
    nearest_spawn_option_summary: Optional[str]
    respawn_in_progress: Optional[bool]
    just_respawned: Optional[bool]
    hp: Optional[float]
    max_hp: Optional[float]
    stamina: Optional[float]
    max_stamina: Optional[float]
    food: Optional[float]
    water: Optional[float]
    selected_hotbar_slot: Optional[int]
    holding_light: Optional[bool]

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> Optional["PlayerState"]:
        if payload is None:
            return None
        return cls(
            position=Vector3State.from_dict(payload.get("Position")),
            rotation=RotationState.from_dict(payload.get("Rotation")),
            alive=_maybe_bool(payload.get("Alive")),
            is_dead=_maybe_bool(payload.get("IsDead")),
            death_screen_visible=_maybe_bool(payload.get("DeathScreenVisible")),
            respawn_available=_maybe_bool(payload.get("RespawnAvailable")),
            respawn_cooldown_seconds=_maybe_float(payload.get("RespawnCooldownSeconds")),
            last_death_time=payload.get("LastDeathTime"),
            last_death_position=Vector3State.from_dict(payload.get("LastDeathPosition")),
            bedroll_spawn_available=_maybe_bool(payload.get("BedrollSpawnAvailable")),
            nearest_spawn_option_summary=payload.get("NearestSpawnOptionSummary"),
            respawn_in_progress=_maybe_bool(payload.get("RespawnInProgress")),
            just_respawned=_maybe_bool(payload.get("JustRespawned")),
            hp=_maybe_float(payload.get("Hp")),
            max_hp=_maybe_float(payload.get("MaxHp")),
            stamina=_maybe_float(payload.get("Stamina")),
            max_stamina=_maybe_float(payload.get("MaxStamina")),
            food=_maybe_float(payload.get("Food")),
            water=_maybe_float(payload.get("Water")),
            selected_hotbar_slot=_maybe_int(payload.get("SelectedHotbarSlot")),
            holding_light=_maybe_bool(payload.get("HoldingLight")),
        )


@dataclass
class GameState:
    time_of_day: Optional[float]
    day: Optional[int]
    biome: Optional[str]

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> Optional["GameState"]:
        if payload is None:
            return None
        return cls(
            time_of_day=_maybe_float(payload.get("TimeOfDay")),
            day=_maybe_int(payload.get("Day")),
            biome=payload.get("Biome"),
        )


@dataclass
class UiState:
    hud_available: bool
    menu_open: bool
    inventory_open: Optional[bool]
    map_open: Optional[bool]
    quest_log_open: Optional[bool]
    console_open: Optional[bool]
    pause_menu_open: Optional[bool]
    death_screen_open: Optional[bool]
    respawn_screen_open: Optional[bool]
    respawn_confirmation_open: Optional[bool]
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "UiState":
        payload = payload or {}
        return cls(
            hud_available=bool(payload.get("HudAvailable", False)),
            menu_open=bool(payload.get("MenuOpen", False)),
            inventory_open=_maybe_bool(payload.get("InventoryOpen")),
            map_open=_maybe_bool(payload.get("MapOpen")),
            quest_log_open=_maybe_bool(payload.get("QuestLogOpen")),
            console_open=_maybe_bool(payload.get("ConsoleOpen")),
            pause_menu_open=_maybe_bool(payload.get("PauseMenuOpen")),
            death_screen_open=_maybe_bool(payload.get("DeathScreenOpen")),
            respawn_screen_open=_maybe_bool(payload.get("RespawnScreenOpen")),
            respawn_confirmation_open=_maybe_bool(payload.get("RespawnConfirmationOpen")),
            note=str(payload.get("Note", "")),
        )


@dataclass
class InputState:
    input_readable: bool
    movement_locked: bool
    move_forward: bool
    move_back: bool
    move_left: bool
    move_right: bool
    sprint: bool
    crouch: bool
    primary_action: bool
    secondary_action: bool
    hold_interact: bool
    auto_run: bool
    note: str

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "InputState":
        payload = payload or {}
        return cls(
            input_readable=bool(payload.get("InputReadable", False)),
            movement_locked=bool(payload.get("MovementLocked", False)),
            move_forward=bool(payload.get("MoveForward", False)),
            move_back=bool(payload.get("MoveBack", False)),
            move_left=bool(payload.get("MoveLeft", False)),
            move_right=bool(payload.get("MoveRight", False)),
            sprint=bool(payload.get("Sprint", False)),
            crouch=bool(payload.get("Crouch", False)),
            primary_action=bool(payload.get("PrimaryAction", False)),
            secondary_action=bool(payload.get("SecondaryAction", False)),
            hold_interact=bool(payload.get("HoldInteract", False)),
            auto_run=bool(payload.get("AutoRun", False)),
            note=str(payload.get("Note", "")),
        )


@dataclass
class Availability:
    player_available: bool
    position_available: bool
    rotation_available: bool
    vitals_available: bool
    time_available: bool
    biome_available: bool
    input_backend_available: bool
    ui_state_available: bool
    respawn_state_available: bool

    @classmethod
    def from_dict(cls, payload: Optional[Dict[str, Any]]) -> "Availability":
        payload = payload or {}
        return cls(
            player_available=bool(payload.get("PlayerAvailable", False)),
            position_available=bool(payload.get("PositionAvailable", False)),
            rotation_available=bool(payload.get("RotationAvailable", False)),
            vitals_available=bool(payload.get("VitalsAvailable", False)),
            time_available=bool(payload.get("TimeAvailable", False)),
            biome_available=bool(payload.get("BiomeAvailable", False)),
            input_backend_available=bool(payload.get("InputBackendAvailable", False)),
            ui_state_available=bool(payload.get("UiStateAvailable", False)),
            respawn_state_available=bool(payload.get("RespawnStateAvailable", False)),
        )


@dataclass
class BridgeState:
    player: Optional[PlayerState]
    game: Optional[GameState]
    ui: UiState
    input_state: InputState
    availability: Availability
    resource_observation: ResourceObservation
    nearby_resource_candidates_summary: NearbyResourceCandidatesSummary
    nearby_interactables_summary: NearbyInteractablesSummary
    nearby_entities_summary: NearbyEntitiesSummary

    @classmethod
    def from_dict(cls, payload: Dict[str, Any]) -> "BridgeState":
        return cls(
            player=PlayerState.from_dict(payload.get("Player")),
            game=GameState.from_dict(payload.get("Game")),
            ui=UiState.from_dict(payload.get("Ui")),
            input_state=InputState.from_dict(payload.get("InputState")),
            availability=Availability.from_dict(payload.get("Availability")),
            resource_observation=ResourceObservation.from_dict(payload.get("ResourceObservation")),
            nearby_resource_candidates_summary=NearbyResourceCandidatesSummary.from_dict(payload.get("NearbyResourceCandidatesSummary")),
            nearby_interactables_summary=NearbyInteractablesSummary.from_dict(payload.get("NearbyInteractablesSummary")),
            nearby_entities_summary=NearbyEntitiesSummary.from_dict(payload.get("NearbyEntitiesSummary")),
        )


@dataclass
class VersionInfo:
    bridge_version: str
    communication_mode: str
    host: str
    port: int
    mod_api_entry_point: str
    runtime: str
    active_backend: Optional[str]

    @classmethod
    def from_dict(cls, payload: Dict[str, Any]) -> "VersionInfo":
        return cls(
            bridge_version=str(payload["BridgeVersion"]),
            communication_mode=str(payload["CommunicationMode"]),
            host=str(payload["Host"]),
            port=int(payload["Port"]),
            mod_api_entry_point=str(payload["ModApiEntryPoint"]),
            runtime=str(payload["Runtime"]),
            active_backend=payload.get("ActiveBackend"),
        )


@dataclass
class CapabilityInfo:
    supported: bool
    note: str

    @classmethod
    def from_dict(cls, payload: Dict[str, Any]) -> "CapabilityInfo":
        return cls(supported=bool(payload.get("Supported", False)), note=str(payload.get("Note", "")))


@dataclass
class ActionCapabilityInfo(CapabilityInfo):
    category: str = ""
    backend: str = ""
    idempotent: bool = False

    @classmethod
    def from_dict(cls, payload: Dict[str, Any]) -> "ActionCapabilityInfo":
        return cls(
            supported=bool(payload.get("Supported", False)),
            note=str(payload.get("Note", "")),
            category=str(payload.get("Category", "")),
            backend=str(payload.get("Backend", "")),
            idempotent=bool(payload.get("Idempotent", False)),
        )


@dataclass
class CapabilitySet:
    phase: str
    active_backend: str
    available_backends: List[str] = field(default_factory=list)
    commands: Dict[str, CapabilityInfo] = field(default_factory=dict)
    actions: Dict[str, ActionCapabilityInfo] = field(default_factory=dict)
    respawn: Dict[str, CapabilityInfo] = field(default_factory=dict)
    features: Dict[str, CapabilityInfo] = field(default_factory=dict)

    @classmethod
    def from_dict(cls, payload: Dict[str, Any]) -> "CapabilitySet":
        return cls(
            phase=str(payload.get("Phase", "")),
            active_backend=str(payload.get("ActiveBackend", "")),
            available_backends=[str(item) for item in payload.get("AvailableBackends", [])],
            commands={k: CapabilityInfo.from_dict(v) for k, v in (payload.get("Commands") or {}).items()},
            actions={k: ActionCapabilityInfo.from_dict(v) for k, v in (payload.get("Actions") or {}).items()},
            respawn={k: CapabilityInfo.from_dict(v) for k, v in (payload.get("Respawn") or {}).items()},
            features={k: CapabilityInfo.from_dict(v) for k, v in (payload.get("Features") or {}).items()},
        )


@dataclass
class LogsTailResult:
    requested_lines: int
    returned_lines: int
    lines: List[str]

    @classmethod
    def from_dict(cls, payload: Dict[str, Any]) -> "LogsTailResult":
        return cls(
            requested_lines=int(payload.get("RequestedLines", 0)),
            returned_lines=int(payload.get("ReturnedLines", 0)),
            lines=[str(line) for line in payload.get("Lines", [])],
        )


@dataclass
class PlayerPositionResult:
    position: Optional[Vector3State]
    available: bool

    @classmethod
    def from_dict(cls, payload: Dict[str, Any]) -> "PlayerPositionResult":
        return cls(
            position=Vector3State.from_dict(payload.get("Position")),
            available=bool(payload.get("Available", False)),
        )


@dataclass
class PlayerRotationResult:
    rotation: Optional[RotationState]
    available: bool

    @classmethod
    def from_dict(cls, payload: Dict[str, Any]) -> "PlayerRotationResult":
        return cls(
            rotation=RotationState.from_dict(payload.get("Rotation")),
            available=bool(payload.get("Available", False)),
        )


@dataclass
class InputActionResult:
    action: str
    accepted: bool
    state_changed: bool
    backend: str
    note: str
    input_state: InputState

    @classmethod
    def from_dict(cls, payload: Dict[str, Any]) -> "InputActionResult":
        return cls(
            action=str(payload.get("Action", "")),
            accepted=bool(payload.get("Accepted", False)),
            state_changed=bool(payload.get("StateChanged", False)),
            backend=str(payload.get("Backend", "")),
            note=str(payload.get("Note", "")),
            input_state=InputState.from_dict(payload.get("InputState")),
        )


@dataclass
class BridgeEnvelope:
    ok: bool
    command: Optional[str]
    timestamp_utc: str
    data: Any
    error: Optional[Dict[str, Any]]

    @classmethod
    def from_dict(cls, payload: Dict[str, Any]) -> "BridgeEnvelope":
        return cls(
            ok=bool(payload.get("Ok", False)),
            command=payload.get("Command"),
            timestamp_utc=str(payload.get("TimestampUtc", "")),
            data=payload.get("Data"),
            error=payload.get("Error"),
        )


def _maybe_float(value: Any) -> Optional[float]:
    if value is None:
        return None
    return float(value)


def _maybe_int(value: Any) -> Optional[int]:
    if value is None:
        return None
    return int(value)


def _maybe_bool(value: Any) -> Optional[bool]:
    if value is None:
        return None
    return bool(value)
