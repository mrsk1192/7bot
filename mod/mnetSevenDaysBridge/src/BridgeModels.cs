using System.Collections.Generic;

namespace mnetSevenDaysBridge
{
    public sealed class BridgeResponse
    {
        public bool Ok { get; set; }
        public string Command { get; set; }
        public string TimestampUtc { get; set; }
        public object Data { get; set; }
        public BridgeError Error { get; set; }
    }

    public sealed class BridgeError
    {
        public string Type { get; set; }
        public string Message { get; set; }
    }

    public sealed class CommandRequest
    {
        public string Command { get; set; }
        public Dictionary<string, object> Arguments { get; set; }
    }

    public sealed class BridgeCommand
    {
        public string Name { get; set; }
        public Dictionary<string, object> Arguments { get; set; }
    }

    public sealed class BridgeState
    {
        public PlayerState Player { get; set; }
        public GameState Game { get; set; }
        public UiState Ui { get; set; }
        public InputState InputState { get; set; }
        public Availability Availability { get; set; }
    }

    public sealed class PlayerState
    {
        public Vector3State Position { get; set; }
        public RotationState Rotation { get; set; }
        public bool? Alive { get; set; }
        public bool? IsDead { get; set; }
        public bool? DeathScreenVisible { get; set; }
        public bool? RespawnAvailable { get; set; }
        public float? RespawnCooldownSeconds { get; set; }
        public string LastDeathTime { get; set; }
        public Vector3State LastDeathPosition { get; set; }
        public bool? BedrollSpawnAvailable { get; set; }
        public string NearestSpawnOptionSummary { get; set; }
        public bool? RespawnInProgress { get; set; }
        public bool? JustRespawned { get; set; }
        public float? Hp { get; set; }
        public float? MaxHp { get; set; }
        public float? Stamina { get; set; }
        public float? MaxStamina { get; set; }
        public float? Food { get; set; }
        public float? Water { get; set; }
        public int? SelectedHotbarSlot { get; set; }
        public bool? HoldingLight { get; set; }
    }

    public sealed class GameState
    {
        public float? TimeOfDay { get; set; }
        public int? Day { get; set; }
        public string Biome { get; set; }
    }

    public sealed class UiState
    {
        public bool HudAvailable { get; set; }
        public bool MenuOpen { get; set; }
        public bool? InventoryOpen { get; set; }
        public bool? MapOpen { get; set; }
        public bool? QuestLogOpen { get; set; }
        public bool? ConsoleOpen { get; set; }
        public bool? PauseMenuOpen { get; set; }
        public bool? DeathScreenOpen { get; set; }
        public bool? RespawnScreenOpen { get; set; }
        public bool? RespawnConfirmationOpen { get; set; }
        public string Note { get; set; }
    }

    public sealed class InputState
    {
        public bool InputReadable { get; set; }
        public bool MovementLocked { get; set; }
        public bool MoveForward { get; set; }
        public bool MoveBack { get; set; }
        public bool MoveLeft { get; set; }
        public bool MoveRight { get; set; }
        public bool Sprint { get; set; }
        public bool Crouch { get; set; }
        public bool PrimaryAction { get; set; }
        public bool SecondaryAction { get; set; }
        public bool HoldInteract { get; set; }
        public bool AutoRun { get; set; }
        public string Note { get; set; }
    }

    public sealed class Availability
    {
        public bool PlayerAvailable { get; set; }
        public bool PositionAvailable { get; set; }
        public bool RotationAvailable { get; set; }
        public bool VitalsAvailable { get; set; }
        public bool TimeAvailable { get; set; }
        public bool BiomeAvailable { get; set; }
        public bool InputBackendAvailable { get; set; }
        public bool UiStateAvailable { get; set; }
        public bool RespawnStateAvailable { get; set; }
    }

    public sealed class Vector3State
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public sealed class RotationState
    {
        public float? Yaw { get; set; }
        public float? Pitch { get; set; }
    }

    public sealed class VersionInfo
    {
        public string BridgeVersion { get; set; }
        public string CommunicationMode { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string ModApiEntryPoint { get; set; }
        public string Runtime { get; set; }
        public string ActiveBackend { get; set; }
    }

    public sealed class CapabilitySet
    {
        public string Phase { get; set; }
        public string ActiveBackend { get; set; }
        public IList<string> AvailableBackends { get; set; }
        public Dictionary<string, CapabilityInfo> Commands { get; set; }
        public Dictionary<string, ActionCapabilityInfo> Actions { get; set; }
        public Dictionary<string, CapabilityInfo> Respawn { get; set; }
        public Dictionary<string, CapabilityInfo> Features { get; set; }
    }

    public class CapabilityInfo
    {
        public bool Supported { get; set; }
        public string Note { get; set; }
    }

    public sealed class ActionCapabilityInfo : CapabilityInfo
    {
        public string Category { get; set; }
        public string Backend { get; set; }
        public bool Idempotent { get; set; }
    }

    public sealed class LogsTailResult
    {
        public int RequestedLines { get; set; }
        public int ReturnedLines { get; set; }
        public IList<string> Lines { get; set; }
    }

    public sealed class InputCommandResult
    {
        public string Action { get; set; }
        public bool Accepted { get; set; }
        public bool StateChanged { get; set; }
        public string Backend { get; set; }
        public string Note { get; set; }
        public InputState InputState { get; set; }
    }
}
