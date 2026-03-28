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
        public InventorySummary InventorySummary { get; set; }
        public ResourceObservation ResourceObservation { get; set; }
        public NearbyResourceCandidatesSummary NearbyResourceCandidatesSummary { get; set; }
        public NearbyInteractablesSummary NearbyInteractablesSummary { get; set; }
        public NearbyEntitiesSummary NearbyEntitiesSummary { get; set; }
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

    public sealed class InventorySummary
    {
        public int SlotCount { get; set; }
        public IList<InventoryItemObservation> Items { get; set; }
        public int? ResourceWoodCount { get; set; }
        public string Note { get; set; }
    }

    public sealed class InventoryItemObservation
    {
        public int SlotIndex { get; set; }
        public string ItemName { get; set; }
        public string ItemClass { get; set; }
        public int Count { get; set; }
        public bool IsHoldingSlot { get; set; }
        public string Note { get; set; }
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

    public sealed class ResourceObservation
    {
        public Vector3State PlayerPosition { get; set; }
        public RotationState PlayerRotation { get; set; }
        public string Biome { get; set; }
        public LookTargetObservation LookTarget { get; set; }
        public InteractionContextObservation InteractionContext { get; set; }
        public ObservationAvailability Availability { get; set; }
    }

    public sealed class ObservationAvailability
    {
        public bool LookTargetAvailable { get; set; }
        public bool InteractionContextAvailable { get; set; }
        public bool ResourceQueryAvailable { get; set; }
        public bool InteractableQueryAvailable { get; set; }
        public bool EntityQueryAvailable { get; set; }
        public bool EnvironmentSummaryAvailable { get; set; }
        public bool BiomeInfoAvailable { get; set; }
        public bool TerrainSummaryAvailable { get; set; }
        public string Note { get; set; }
    }

    public sealed class LookTargetObservation
    {
        public bool HasTarget { get; set; }
        public string Source { get; set; }
        public string TargetKind { get; set; }
        public string TargetName { get; set; }
        public string TargetClass { get; set; }
        public object TargetId { get; set; }
        public object EntityId { get; set; }
        public object BlockId { get; set; }
        public float Distance { get; set; }
        public Vector3State Position { get; set; }
        public bool CanInteract { get; set; }
        public string InteractionPromptText { get; set; }
        public string InteractionActionKind { get; set; }
        public bool Hostile { get; set; }
        public bool Alive { get; set; }
        public object Locked { get; set; }
        public object Powered { get; set; }
        public object Active { get; set; }
        public object LineOfSightClear { get; set; }
        public bool IsResourceCandidate { get; set; }
        public string CandidateCategory { get; set; }
        public float CandidateConfidence { get; set; }
        public string LikelyResourceType { get; set; }
        public object Durability { get; set; }
        public object MaxDurability { get; set; }
        public string Note { get; set; }
    }

    public sealed class InteractionContextObservation
    {
        public bool HasFocusTarget { get; set; }
        public bool CanInteractNow { get; set; }
        public string SuggestedActionKind { get; set; }
        public string PromptText { get; set; }
        public string TargetKind { get; set; }
        public string TargetName { get; set; }
        public float Distance { get; set; }
        public string Source { get; set; }
        public bool RequiresPreciseAlignment { get; set; }
        public object RecommendedInteractDistanceMin { get; set; }
        public object RecommendedInteractDistanceMax { get; set; }
        public string Note { get; set; }
    }

    public sealed class ResourceCandidateObservation
    {
        public string Name { get; set; }
        public object BlockId { get; set; }
        public Vector3State Position { get; set; }
        public float Distance { get; set; }
        public string CandidateCategory { get; set; }
        public float CandidateConfidence { get; set; }
        public string LikelyResourceType { get; set; }
        public bool IsExposed { get; set; }
        public string Biome { get; set; }
        public object LineOfSightClear { get; set; }
        public object ReachableHint { get; set; }
        public string Note { get; set; }
    }

    public sealed class InteractableObservation
    {
        public string Kind { get; set; }
        public object Id { get; set; }
        public string Name { get; set; }
        public Vector3State Position { get; set; }
        public float Distance { get; set; }
        public bool CanInteract { get; set; }
        public string InteractionPromptText { get; set; }
        public string InteractionActionKind { get; set; }
        public object Locked { get; set; }
        public object Powered { get; set; }
        public object Active { get; set; }
        public object LineOfSightClear { get; set; }
        public string Note { get; set; }
    }

    public sealed class EntityObservation
    {
        public object EntityId { get; set; }
        public string EntityName { get; set; }
        public string EntityClass { get; set; }
        public string Kind { get; set; }
        public Vector3State Position { get; set; }
        public float Distance { get; set; }
        public bool Alive { get; set; }
        public bool Hostile { get; set; }
        public bool CanInteract { get; set; }
        public object CurrentTargetingPlayer { get; set; }
        public object LineOfSightClear { get; set; }
        public string Note { get; set; }
    }

    public sealed class EnvironmentSummary
    {
        public string CurrentBiome { get; set; }
        public string FootBlockName { get; set; }
        public object FootBlockId { get; set; }
        public bool? IndoorsHint { get; set; }
        public bool? WaterNearbyHint { get; set; }
        public bool? FallHazardAheadHint { get; set; }
        public float? LocalHeightSpan { get; set; }
        public string Note { get; set; }
    }

    public sealed class BiomeInfo
    {
        public string CurrentBiome { get; set; }
        public object BiomeIntensity { get; set; }
        public bool? IndoorsHint { get; set; }
        public string HazardHint { get; set; }
        public string Note { get; set; }
    }

    public sealed class TerrainSummary
    {
        public Vector3State SampleCenter { get; set; }
        public float SampleRadius { get; set; }
        public float MinGroundY { get; set; }
        public float MaxGroundY { get; set; }
        public float HeightSpan { get; set; }
        public string FootBlockName { get; set; }
        public object FootBlockId { get; set; }
        public bool? WaterNearbyHint { get; set; }
        public bool? FallHazardAheadHint { get; set; }
        public bool? IndoorsHint { get; set; }
        public string Note { get; set; }
    }

    public sealed class ResourceCandidateQueryResult
    {
        public Vector3State Center { get; set; }
        public float Radius { get; set; }
        public int MaxResults { get; set; }
        public string SortBy { get; set; }
        public int Count { get; set; }
        public IList<string> IgnoredFilters { get; set; }
        public IList<ResourceCandidateObservation> Candidates { get; set; }
        public string Note { get; set; }
    }

    public sealed class InteractableQueryResult
    {
        public Vector3State Center { get; set; }
        public float Radius { get; set; }
        public int MaxResults { get; set; }
        public int Count { get; set; }
        public IList<string> IgnoredFilters { get; set; }
        public IList<InteractableObservation> Interactables { get; set; }
        public string Note { get; set; }
    }

    public sealed class EntityQueryResult
    {
        public Vector3State Center { get; set; }
        public float Radius { get; set; }
        public int MaxResults { get; set; }
        public int Count { get; set; }
        public IList<string> IgnoredFilters { get; set; }
        public IList<EntityObservation> Entities { get; set; }
        public string Note { get; set; }
    }

    public sealed class NearbyResourceCandidatesSummary
    {
        public int Count { get; set; }
        public IList<ResourceCandidateObservation> TopCandidates { get; set; }
    }

    public sealed class NearbyInteractablesSummary
    {
        public int Count { get; set; }
        public IList<InteractableObservation> TopInteractables { get; set; }
    }

    public sealed class NearbyEntitiesSummary
    {
        public int HostileCount { get; set; }
        public int NpcCount { get; set; }
        public float? NearestHostileDistance { get; set; }
        public IList<EntityObservation> TopEntities { get; set; }
    }
}
