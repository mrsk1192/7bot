using System;
using System.Collections;
using System.Collections.Generic;
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
        private readonly ObservationService observationService;

        public GameStateCollector(
            BridgeLogger logger,
            Func<InputState> inputStateProvider,
            Func<UiState> uiStateProvider,
            Func<bool> inputBackendAvailableProvider,
            Func<DeathRespawnStateSnapshot> deathRespawnStateProvider,
            ObservationService observationService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.inputStateProvider = inputStateProvider ?? throw new ArgumentNullException(nameof(inputStateProvider));
            this.uiStateProvider = uiStateProvider ?? throw new ArgumentNullException(nameof(uiStateProvider));
            this.inputBackendAvailableProvider = inputBackendAvailableProvider ?? throw new ArgumentNullException(nameof(inputBackendAvailableProvider));
            this.deathRespawnStateProvider = deathRespawnStateProvider ?? throw new ArgumentNullException(nameof(deathRespawnStateProvider));
            this.observationService = observationService ?? throw new ArgumentNullException(nameof(observationService));
        }

        public BridgeState CollectState(bool includeObservation = true)
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
                },
                InventorySummary = includeObservation ? TryCollectInventorySummary(player) : null,
                ResourceObservation = includeObservation ? observationService.GetResourceObservation() : null,
                NearbyResourceCandidatesSummary = includeObservation ? observationService.GetNearbyResourceCandidatesSummary() : null,
                NearbyInteractablesSummary = includeObservation ? observationService.GetNearbyInteractablesSummary() : null,
                NearbyEntitiesSummary = includeObservation ? observationService.GetNearbyEntitiesSummary() : null
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
            var biomeName = NormalizeBiomeName(biome);
            if ((biome == null || string.IsNullOrWhiteSpace(biomeName)) && player is EntityPlayerLocal localPlayer)
            {
                var gameManager = GameManager.Instance;
                var world = gameManager == null ? null : gameManager.World;
                if (world != null)
                {
                    biome = InvokeMember(world, "GetBiomeInWorld", Mathf.FloorToInt(localPlayer.position.x), Mathf.FloorToInt(localPlayer.position.z))
                        ?? InvokeMember(world, "GetBiome", Mathf.FloorToInt(localPlayer.position.x), Mathf.FloorToInt(localPlayer.position.z));
                    biomeName = NormalizeBiomeName(biome);
                }
            }

            return string.IsNullOrWhiteSpace(biomeName) ? null : biomeName;
        }

        private string NormalizeBiomeName(object biome)
        {
            if (biome == null)
            {
                return null;
            }

            var biomeName = ReadMember(biome, "m_sBiomeName")
                ?? ReadMember(biome, "LocalizedName")
                ?? ReadMember(biome, "BiomeName")
                ?? ReadMember(biome, "Name");
            if (biomeName != null)
            {
                var text = biomeName.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            var fallback = biome.ToString();
            return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
        }

        private bool? TryReadHoldingLight(object player)
        {
            return TryReadBool(player, "IsHoldingLight");
        }

        private InventorySummary TryCollectInventorySummary(object player)
        {
            var empty = new InventorySummary
            {
                SlotCount = 0,
                Items = new List<InventoryItemObservation>(),
                Note = "Inventory summary was unavailable."
            };

            if (player == null)
            {
                return empty;
            }

            try
            {
                var inventory = ReadMember(player, "inventory")
                    ?? ReadMember(player, "Inventory");
                if (inventory == null)
                {
                    return empty;
                }

                var holdingIndex = TryReadInt(inventory, "holdingItemIdx", "HoldingItemIdx", "selectedHotbarSlot");
                var slots = ReadMember(inventory, "slots") as IEnumerable;
                var items = slots == null
                    ? EnumerateInventoryItems(inventory, holdingIndex)
                    : EnumerateInventorySlots(slots, holdingIndex);
                var resourceWoodCount = TryReadInventoryItemCount(inventory, "resourceWood")
                    ?? TryReadInventoryItemCount(inventory, "wood");
                return new InventorySummary
                {
                    SlotCount = items.Count,
                    Items = items,
                    ResourceWoodCount = resourceWoodCount,
                    Note = items.Count > 0
                        ? "Inventory summary was collected from reflected inventory slots."
                        : resourceWoodCount.HasValue
                            ? "Inventory slots were empty or opaque, but direct item counts were available."
                            : "Inventory was available but no populated slots were enumerated."
                };
            }
            catch (Exception exception)
            {
                logger.Error("Failed to collect the inventory summary.", exception);
                return empty;
            }
        }

        private List<InventoryItemObservation> EnumerateInventorySlots(IEnumerable slots, int? holdingIndex)
        {
            var items = new List<InventoryItemObservation>();
            if (slots == null)
            {
                return items;
            }

            foreach (var entry in slots)
            {
                if (entry == null)
                {
                    continue;
                }

                var slotIndex = TryReadInt(entry, "slotIdx", "SlotIdx", "index", "Index") ?? items.Count;
                var itemStack = ReadMember(entry, "itemStack")
                    ?? ReadMember(entry, "ItemStack");
                var count = TryReadInt(itemStack, "count", "Count", "itemCount");
                if (!count.HasValue || count.Value <= 0)
                {
                    continue;
                }

                var itemValue = ReadMember(entry, "itemValue")
                    ?? ReadMember(itemStack, "itemValue")
                    ?? ReadMember(itemStack, "ItemValue");
                var itemClass = ReadMember(entry, "item")
                    ?? ReadMember(entry, "Item")
                    ?? ReadMember(itemValue, "ItemClass")
                    ?? ReadMember(itemValue, "itemClass");
                var itemName = ResolveInventoryItemName(itemValue, itemClass);
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    continue;
                }

                items.Add(new InventoryItemObservation
                {
                    SlotIndex = slotIndex,
                    ItemName = itemName,
                    ItemClass = ResolveInventoryItemClassName(itemClass, itemValue),
                    Count = count.Value,
                    IsHoldingSlot = holdingIndex.HasValue && holdingIndex.Value == slotIndex,
                    Note = "Inventory item reflected from Inventory.slots."
                });
            }

            return items;
        }

        private int? TryReadInventoryItemCount(object inventory, string itemName)
        {
            if (inventory == null || string.IsNullOrWhiteSpace(itemName))
            {
                return null;
            }

            try
            {
                var itemValue = InvokeStaticMethod(typeof(ItemClass), "GetItem", itemName, true);
                if (itemValue == null)
                {
                    return null;
                }

                var raw = InvokeMember(inventory, "GetItemCount", itemValue, false, 0, 0, false);
                if (raw is int intValue)
                {
                    return intValue;
                }

                if (int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var parsed))
                {
                    return parsed;
                }
            }
            catch (Exception exception)
            {
                logger.Warn("Failed to read inventory count for item '" + itemName + "': " + exception.Message);
            }

            return null;
        }

        private List<InventoryItemObservation> EnumerateInventoryItems(object inventory, int? holdingIndex)
        {
            var items = new List<InventoryItemObservation>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var slotIndex = 0;

            foreach (var collection in EnumerateInventoryCollections(inventory))
            {
                foreach (var item in EnumerateInventoryItemsFromCollection(collection, slotIndex, holdingIndex))
                {
                    var key = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}|{1}|{2}|{3}",
                        item.SlotIndex,
                        item.ItemName ?? string.Empty,
                        item.ItemClass ?? string.Empty,
                        item.Count);
                    if (seen.Add(key))
                    {
                        items.Add(item);
                    }
                }

                if (collection is IEnumerable countedCollection)
                {
                    foreach (var _ in countedCollection)
                    {
                        slotIndex++;
                    }
                }
            }

            return items;
        }

        private IEnumerable<object> EnumerateInventoryCollections(object inventory)
        {
            if (inventory == null)
            {
                yield break;
            }

            var yielded = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var rootNames = new[]
            {
                "slots",
                "Slots",
                "itemStacks",
                "ItemStacks",
                "items",
                "Items",
                "backpack",
                "Backpack",
                "toolbelt",
                "Toolbelt",
                "bag",
                "Bag"
            };

            foreach (var name in rootNames)
            {
                var value = ReadMember(inventory, name) ?? InvokeMember(inventory, name);
                if (value == null)
                {
                    continue;
                }

                if (yielded.Add(value))
                {
                    yield return value;
                }

                var nestedNames = new[] { "slots", "Slots", "itemStacks", "ItemStacks", "items", "Items" };
                foreach (var nestedName in nestedNames)
                {
                    var nestedValue = ReadMember(value, nestedName) ?? InvokeMember(value, nestedName);
                    if (nestedValue != null && yielded.Add(nestedValue))
                    {
                        yield return nestedValue;
                    }
                }
            }
        }

        private IEnumerable<InventoryItemObservation> EnumerateInventoryItemsFromCollection(object collection, int startSlotIndex, int? holdingIndex)
        {
            if (!(collection is IEnumerable enumerable))
            {
                yield break;
            }

            var slotIndex = startSlotIndex;
            foreach (var entry in enumerable)
            {
                var currentSlot = slotIndex;
                slotIndex++;
                var item = TryBuildInventoryItem(entry, currentSlot, holdingIndex);
                if (item != null)
                {
                    yield return item;
                }
            }
        }

        private InventoryItemObservation TryBuildInventoryItem(object entry, int slotIndex, int? holdingIndex)
        {
            if (entry == null)
            {
                return null;
            }

            var count = TryReadInt(entry, "count", "Count", "itemCount");
            var itemValue = ReadMember(entry, "itemValue")
                ?? ReadMember(entry, "ItemValue")
                ?? entry;
            var itemClass = ReadMember(itemValue, "ItemClass")
                ?? ReadMember(itemValue, "itemClass")
                ?? ReadMember(entry, "ItemClass")
                ?? ReadMember(entry, "itemClass");

            if (!count.HasValue)
            {
                count = TryReadInt(itemValue, "count", "Count", "itemCount");
            }

            if (!count.HasValue || count.Value <= 0)
            {
                return null;
            }

            var itemName = ResolveInventoryItemName(itemValue, itemClass);
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return null;
            }

            return new InventoryItemObservation
            {
                SlotIndex = slotIndex,
                ItemName = itemName,
                ItemClass = ResolveInventoryItemClassName(itemClass, itemValue),
                Count = count.Value,
                IsHoldingSlot = holdingIndex.HasValue && holdingIndex.Value == slotIndex,
                Note = "Inventory item reflected from a populated slot."
            };
        }

        private string ResolveInventoryItemName(object itemValue, object itemClass)
        {
            var candidates = new[]
            {
                ReadMember(itemValue, "ItemName"),
                ReadMember(itemValue, "Name"),
                ReadMember(itemValue, "name"),
                InvokeMember(itemValue, "GetItemName"),
                ReadMember(itemClass, "ItemName"),
                ReadMember(itemClass, "LocalizedName"),
                ReadMember(itemClass, "Name"),
                ReadMember(itemClass, "name"),
                InvokeMember(itemClass, "GetItemName"),
                InvokeMember(itemClass, "GetLocalizedItemName"),
            };

            foreach (var candidate in candidates)
            {
                var text = Convert.ToString(candidate, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, "Air", StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            var fallback = itemValue == null ? null : itemValue.ToString();
            return string.IsNullOrWhiteSpace(fallback) || string.Equals(fallback, "Air", StringComparison.OrdinalIgnoreCase)
                ? null
                : fallback;
        }

        private string ResolveInventoryItemClassName(object itemClass, object itemValue)
        {
            var candidates = new[]
            {
                Convert.ToString(ReadMember(itemClass, "Name"), CultureInfo.InvariantCulture),
                Convert.ToString(ReadMember(itemClass, "name"), CultureInfo.InvariantCulture),
                Convert.ToString(ReadMember(itemValue, "Name"), CultureInfo.InvariantCulture),
                Convert.ToString(ReadMember(itemValue, "name"), CultureInfo.InvariantCulture),
            };

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return itemClass == null ? itemValue?.GetType().Name : itemClass.GetType().Name;
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

        private object InvokeStaticMethod(Type type, string methodName, params object[] args)
        {
            if (type == null || string.IsNullOrWhiteSpace(methodName))
            {
                return null;
            }

            foreach (var candidate in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
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

                return candidate.Invoke(null, args);
            }

            return null;
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

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
