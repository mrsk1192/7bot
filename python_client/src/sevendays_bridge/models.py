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
            alive=payload.get("Alive"),
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

    @classmethod
    def from_dict(cls, payload: Dict[str, Any]) -> "BridgeState":
        return cls(
            player=PlayerState.from_dict(payload.get("Player")),
            game=GameState.from_dict(payload.get("Game")),
            ui=UiState.from_dict(payload.get("Ui")),
            input_state=InputState.from_dict(payload.get("InputState")),
            availability=Availability.from_dict(payload.get("Availability")),
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
            position=Vector3State.from_dict(payload.get("position") or payload.get("Position")),
            available=bool(payload.get("available", payload.get("Available", False))),
        )


@dataclass
class PlayerRotationResult:
    rotation: Optional[RotationState]
    available: bool

    @classmethod
    def from_dict(cls, payload: Dict[str, Any]) -> "PlayerRotationResult":
        return cls(
            rotation=RotationState.from_dict(payload.get("rotation") or payload.get("Rotation")),
            available=bool(payload.get("available", payload.get("Available", False))),
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
