using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace mnetSevenDaysBridge
{
    public sealed class GameStateCollector
    {
        private readonly BridgeLogger logger;
        private readonly Func<InputState> inputStateProvider;
        private readonly Func<UiState> uiStateProvider;
        private readonly Func<bool> inputBackendAvailableProvider;
        private readonly Func<DeathRespawnStateSnapshot> deathRespawnStateProvider;

        public GameStateCollector(
            BridgeLogger logger,
            Func<InputState> inputStateProvider,
            Func<UiState> uiStateProvider,
            Func<bool> inputBackendAvailableProvider,
            Func<DeathRespawnStateSnapshot> deathRespawnStateProvider)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.inputStateProvider = inputStateProvider ?? throw new ArgumentNullException(nameof(inputStateProvider));
            this.uiStateProvider = uiStateProvider ?? throw new ArgumentNullException(nameof(uiStateProvider));
            this.inputBackendAvailableProvider = inputBackendAvailableProvider ?? throw new ArgumentNullException(nameof(inputBackendAvailableProvider));
            this.deathRespawnStateProvider = deathRespawnStateProvider ?? throw new ArgumentNullException(nameof(deathRespawnStateProvider));
        }

        public BridgeState CollectState()
        {
            var world = GetWorld();
            var player = GetPrimaryPlayer(world);
            var position = TryGetPosition(player);
            var rotation = TryGetRotation(player);
            var hp = TryReadFloat(player, "Health", "health");
            var maxHp = TryReadStatMax(player, "Health", "health")
                ?? TryReadFloat(player, "GetMaxHealth", "MaxHealth", "maxHealth", "classMaxHealth");
            var stamina = TryReadNestedStat(player, "Stamina", "stamina");
            var maxStamina = TryReadStatMax(player, "Stamina", "stamina")
                ?? TryReadFloat(player, "GetMaxStamina", "MaxStamina", "maxStamina", "classMaxStamina");
            var food = TryReadNestedStat(player, "Food", "food");
            var water = TryReadNestedStat(player, "Water", "water");
            var timeOfDay = TryReadFloat(world, "worldTime", "WorldTime", "time", "Time");
            var day = TryReadInt(world, "day", "Day", "worldDay", "WorldDay");
            var biome = TryReadBiome(player);
            var deathRespawnState = deathRespawnStateProvider() ?? DeathRespawnStateSnapshot.CreateDefault();
            var uiState = uiStateProvider() ?? CreateFallbackUiState();
            uiState.DeathScreenOpen = deathRespawnState.DeathScreenOpen;
            uiState.RespawnScreenOpen = deathRespawnState.RespawnScreenOpen;
            uiState.RespawnConfirmationOpen = deathRespawnState.RespawnConfirmationOpen;
            uiState.MenuOpen = uiState.MenuOpen
                || deathRespawnState.DeathScreenOpen
                || deathRespawnState.RespawnScreenOpen
                || deathRespawnState.RespawnConfirmationOpen;

            var inputState = inputStateProvider() ?? CreateFallbackInputState(uiState);
            inputState.MovementLocked = uiState.MenuOpen || deathRespawnState.IsDead || deathRespawnState.RespawnInProgress;

            return new BridgeState
            {
                Player = new PlayerState
                {
                    Position = position,
                    Rotation = rotation,
                    Alive = deathRespawnState.Alive ?? TryReadAlive(player),
                    IsDead = deathRespawnState.IsDead,
                    DeathScreenVisible = deathRespawnState.DeathScreenVisible,
                    RespawnAvailable = deathRespawnState.RespawnAvailable,
                    RespawnCooldownSeconds = deathRespawnState.RespawnCooldownSeconds,
                    LastDeathTime = deathRespawnState.LastDeathTime,
                    LastDeathPosition = deathRespawnState.LastDeathPosition,
                    BedrollSpawnAvailable = deathRespawnState.BedrollSpawnAvailable,
                    NearestSpawnOptionSummary = deathRespawnState.NearestSpawnOptionSummary,
                    RespawnInProgress = deathRespawnState.RespawnInProgress,
                    JustRespawned = deathRespawnState.JustRespawned,
                    Hp = hp,
                    MaxHp = maxHp,
                    Stamina = stamina,
                    MaxStamina = maxStamina,
                    Food = food,
                    Water = water,
                    SelectedHotbarSlot = TryReadSelectedHotbarSlot(player),
                    HoldingLight = TryReadHoldingLight(player)
                },
                Game = new GameState
                {
                    TimeOfDay = timeOfDay,
                    Day = day,
                    Biome = biome
                },
                Ui = uiState,
                InputState = inputState,
                Availability = new Availability
                {
                    PlayerAvailable = player != null,
                    PositionAvailable = position != null,
                    RotationAvailable = rotation != null && (rotation.Yaw.HasValue || rotation.Pitch.HasValue),
                    VitalsAvailable = hp.HasValue || maxHp.HasValue || stamina.HasValue || maxStamina.HasValue,
                    TimeAvailable = timeOfDay.HasValue || day.HasValue,
                    BiomeAvailable = !string.IsNullOrWhiteSpace(biome),
                    InputBackendAvailable = inputBackendAvailableProvider(),
                    UiStateAvailable = uiState != null,
                    RespawnStateAvailable = true
                }
            };
        }

        public Vector3State CollectPosition()
        {
            return TryGetPosition(GetPrimaryPlayer(GetWorld()));
        }

        public RotationState CollectRotation()
        {
            return TryGetRotation(GetPrimaryPlayer(GetWorld()));
        }

        private UiState CreateFallbackUiState()
        {
            return new UiState
            {
                HudAvailable = false,
                MenuOpen = false,
                InventoryOpen = null,
                MapOpen = null,
                QuestLogOpen = null,
                ConsoleOpen = null,
                PauseMenuOpen = null,
                DeathScreenOpen = null,
                RespawnScreenOpen = null,
                RespawnConfirmationOpen = null,
                Note = "UI state fallback placeholder."
            };
        }

        private InputState CreateFallbackInputState(UiState uiState)
        {
            return new InputState
            {
                InputReadable = false,
                MovementLocked = uiState != null && uiState.MenuOpen,
                MoveForward = false,
                MoveBack = false,
                MoveLeft = false,
                MoveRight = false,
                Sprint = false,
                Crouch = false,
                PrimaryAction = false,
                SecondaryAction = false,
                HoldInteract = false,
                AutoRun = false,
                Note = "Input state fallback placeholder."
            };
        }

        private object GetWorld()
        {
            try
            {
                var gameManager = ReadStaticMember(typeof(GameManager), "Instance");
                return gameManager == null ? null : ReadMember(gameManager, "World");
            }
            catch (Exception exception)
            {
                logger.Error("Failed to resolve the world object.", exception);
                return null;
            }
        }

        private object GetPrimaryPlayer(object world)
        {
            if (world == null)
            {
                return null;
            }

            try
            {
                return InvokeMember(world, "GetPrimaryPlayer")
                    ?? InvokeMember(world, "GetLocalPlayer")
                    ?? ReadMember(world, "PrimaryPlayer")
                    ?? ReadMember(world, "primaryPlayer");
            }
            catch (Exception exception)
            {
                logger.Error("Failed to resolve the primary player.", exception);
                return null;
            }
        }

        private Vector3State TryGetPosition(object player)
        {
            if (player == null)
            {
                return null;
            }

            var raw = ReadMember(player, "position")
                ?? ReadMember(player, "Position")
                ?? ReadMember(ReadMember(player, "transform"), "position");

            if (raw is Vector3 position)
            {
                return new Vector3State { X = position.x, Y = position.y, Z = position.z };
            }

            return null;
        }

        private RotationState TryGetRotation(object player)
        {
            if (player == null)
            {
                return null;
            }

            var fpCamera = ReadMember(player, "vp_FPCamera");
            var yaw = TryReadFloat(fpCamera, "Yaw", "m_Yaw")
                ?? TryReadFloat(player, "rotationYaw", "yaw", "RotationYaw", "rotation");
            var pitch = TryReadFloat(fpCamera, "Pitch", "m_Pitch")
                ?? TryReadFloat(player, "rotationPitch", "pitch", "RotationPitch", "upDownRotation", "OverridePitch");

            if (!yaw.HasValue)
            {
                var transform = ReadMember(player, "transform");
                var eulerAngles = ReadMember(transform, "eulerAngles");
                if (eulerAngles is Vector3 angles)
                {
                    yaw = angles.y;
                    pitch = angles.x;
                }
            }

            return new RotationState
            {
                Yaw = yaw,
                Pitch = pitch
            };
        }

        private bool? TryReadAlive(object player)
        {
            if (player == null)
            {
                return null;
            }

            var alive = TryReadBool(player, "Alive", "alive", "isEntityAlive");
            if (alive.HasValue)
            {
                return alive.Value;
            }

            var isDead = TryReadBool(player, "IsDead", "isDead");
            return isDead.HasValue ? !isDead.Value : (bool?)null;
        }

        private int? TryReadSelectedHotbarSlot(object player)
        {
            if (player == null)
            {
                return null;
            }

            var inventory = ReadMember(player, "inventory")
                ?? ReadMember(player, "Inventory");
            return TryReadInt(inventory, "holdingItemIdx", "HoldingItemIdx", "selectedHotbarSlot");
        }

        private string TryReadBiome(object player)
        {
            if (player == null)
            {
                return null;
            }

            var biome = ReadMember(player, "biomeStandingOn")
                ?? ReadMember(player, "BiomeStandingOn")
                ?? ReadMember(player, "biome");
            return biome == null ? null : biome.ToString();
        }

        private bool? TryReadHoldingLight(object player)
        {
            return TryReadBool(player, "IsHoldingLight");
        }

        private float? TryReadNestedStat(object root, params string[] names)
        {
            if (root == null)
            {
                return null;
            }

            var stats = ReadMember(root, "Stats") ?? ReadMember(root, "stats");
            if (stats == null)
            {
                return TryReadFloat(root, names);
            }

            foreach (var name in names)
            {
                var direct = ReadMember(stats, name);
                var value = TryCoerceFloat(direct);
                if (value.HasValue)
                {
                    return value;
                }

                if (direct != null)
                {
                    value = TryReadFloat(direct, "Value", "Current", "BaseValue");
                    if (value.HasValue)
                    {
                        return value;
                    }
                }

                var viaMethod = InvokeMember(stats, "GetValue", name);
                value = TryCoerceFloat(viaMethod);
                if (value.HasValue)
                {
                    return value;
                }
            }

            return null;
        }

        private float? TryReadStatMax(object root, params string[] names)
        {
            if (root == null)
            {
                return null;
            }

            var stats = ReadMember(root, "Stats") ?? ReadMember(root, "stats");
            if (stats == null)
            {
                return null;
            }

            foreach (var name in names)
            {
                var stat = ReadMember(stats, name);
                if (stat == null)
                {
                    continue;
                }

                var max = TryReadFloat(stat, "Max", "ModifiedMax", "BaseMax", "OriginalMax");
                if (max.HasValue)
                {
                    return max;
                }
            }

            return null;
        }

        private float? TryReadFloat(object target, params string[] memberNames)
        {
            if (target == null)
            {
                return null;
            }

            foreach (var memberName in memberNames)
            {
                var raw = ReadMember(target, memberName) ?? InvokeMember(target, memberName);
                var value = TryCoerceFloat(raw);
                if (value.HasValue)
                {
                    return value;
                }
            }

            return null;
        }

        private int? TryReadInt(object target, params string[] memberNames)
        {
            if (target == null)
            {
                return null;
            }

            foreach (var memberName in memberNames)
            {
                var raw = ReadMember(target, memberName) ?? InvokeMember(target, memberName);
                if (raw == null)
                {
                    continue;
                }

                if (raw is int intValue)
                {
                    return intValue;
                }

                if (int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private bool? TryReadBool(object target, params string[] memberNames)
        {
            if (target == null)
            {
                return null;
            }

            foreach (var memberName in memberNames)
            {
                var raw = ReadMember(target, memberName) ?? InvokeMember(target, memberName);
                if (raw == null)
                {
                    continue;
                }

                if (raw is bool boolValue)
                {
                    return boolValue;
                }

                if (bool.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private float? TryCoerceFloat(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is float single)
            {
                return single;
            }

            if (value is double @double)
            {
                return (float)@double;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (float.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private object ReadStaticMember(Type type, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property != null)
            {
                return property.GetValue(null, null);
            }

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return field == null ? null : field.GetValue(null);
        }

        private object ReadMember(object target, string name)
        {
            if (target == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var type = target.GetType();
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return property.GetValue(target, null);
            }

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(target);
        }

        private object InvokeMember(object target, string methodName, params object[] args)
        {
            if (target == null || string.IsNullOrWhiteSpace(methodName))
            {
                return null;
            }

            foreach (var candidate in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = candidate.GetParameters();
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                return candidate.Invoke(target, args);
            }

            return null;
        }
    }
}
