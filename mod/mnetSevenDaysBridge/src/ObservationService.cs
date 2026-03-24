using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace mnetSevenDaysBridge
{
    public sealed class ObservationService
    {
        private const string UnknownText = "Unknown";
        private readonly BridgeLogger logger;
        private readonly object cacheSyncRoot = new object();
        private ResourceObservation cachedResourceObservation;
        private DateTime cachedResourceObservationUtc = DateTime.MinValue;
        private EnvironmentSummary cachedEnvironmentSummary;
        private DateTime cachedEnvironmentSummaryUtc = DateTime.MinValue;
        private BiomeInfo cachedBiomeInfo;
        private DateTime cachedBiomeInfoUtc = DateTime.MinValue;
        private TerrainSummary cachedTerrainSummary;
        private DateTime cachedTerrainSummaryUtc = DateTime.MinValue;
        private NearbyResourceCandidatesSummary cachedNearbyResourceCandidatesSummary;
        private DateTime cachedNearbyResourceCandidatesSummaryUtc = DateTime.MinValue;
        private NearbyInteractablesSummary cachedNearbyInteractablesSummary;
        private DateTime cachedNearbyInteractablesSummaryUtc = DateTime.MinValue;
        private NearbyEntitiesSummary cachedNearbyEntitiesSummary;
        private DateTime cachedNearbyEntitiesSummaryUtc = DateTime.MinValue;

        public ObservationService(BridgeLogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ResourceObservation GetResourceObservation()
        {
            return GetCached(ref cachedResourceObservation, ref cachedResourceObservationUtc, 150, BuildResourceObservation);
        }

        private ResourceObservation BuildResourceObservation()
        {
            var player = ResolvePlayer();
            return new ResourceObservation
            {
                PlayerPosition = player == null ? null : ToVector3State(player.position),
                PlayerRotation = ReadRotation(player),
                Biome = ResolveBiome(player),
                LookTarget = GetLookTarget(),
                InteractionContext = GetInteractionContext(),
                Availability = new ObservationAvailability
                {
                    LookTargetAvailable = true,
                    InteractionContextAvailable = true,
                    ResourceQueryAvailable = true,
                    InteractableQueryAvailable = true,
                    EntityQueryAvailable = true,
                    EnvironmentSummaryAvailable = true,
                    BiomeInfoAvailable = true,
                    TerrainSummaryAvailable = true,
                    Note = "Phase 4 observation pipeline is active."
                }
            };
        }

        public LookTargetObservation GetLookTarget()
        {
            var player = ResolvePlayer();
            if (player == null)
            {
                return CreateEmptyLookTarget("unknown", Vector3.zero, "Primary local player was unavailable.");
            }

            var world = ResolveWorld();
            var ray = player.GetLookRay();
            if (world != null && TryBuildLookTargetFromHitInfo(world, player, out var hitInfoTarget))
            {
                return hitInfoTarget;
            }

            if (Physics.Raycast(ray, out var hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (world == null)
                {
                    return CreateEmptyLookTarget("raycast", hit.point, "World was unavailable while resolving the look target.");
                }

                var entity = FindNearestEntity(world, player, hit.point, 1.8f);
                if (entity != null)
                {
                    var target = BuildLookTargetFromEntity(player, entity, hit.point);
                    target.Source = "raycast";
                    return target;
                }

                var blockPos = ResolveBlockPosition(hit.point, hit.normal, ray.direction);
                var blockTarget = BuildLookTargetFromBlock(world, player, blockPos, world.GetBlock(blockPos), hit.point);
                blockTarget.Source = "raycast";
                if (TryPromoteAlignedInteractableTarget(world, player, blockTarget, out var promotedTarget))
                {
                    promotedTarget.Source = "raycast";
                    promotedTarget.Note = "Promoted an aligned interactable over a foreground block hit.";
                    return promotedTarget;
                }

                return blockTarget;
            }

            if (world != null && TryBuildLookTargetFromNearbyFocus(world, player, out var nearbyTarget))
            {
                return nearbyTarget;
            }

            return CreateEmptyLookTarget("raycast", ray.origin + ray.direction * 6f, "No focused target was hit by the look ray.");
        }

        public InteractionContextObservation GetInteractionContext()
        {
            return BuildInteractionContext(GetLookTarget());
        }

        public ResourceCandidateQueryResult QueryResourceCandidates(IDictionary<string, object> arguments)
        {
            var player = ResolvePlayer();
            var world = ResolveWorld();
            var center = ReadCenter(arguments, player == null ? Vector3.zero : player.position);
            var requestedRadius = ReadFloat(arguments, "radius", 10f);
            var radius = Mathf.Clamp(requestedRadius, 1f, 24f);
            var maxResults = Mathf.Clamp(ReadInt(arguments, "max_results", 10), 1, 64);
            var includeSurfaceOnly = ReadBool(arguments, "include_surface_only", false);
            var includeExposedOnly = ReadBool(arguments, "include_exposed_only", false);
            var minConfidence = Mathf.Clamp01(ReadFloat(arguments, "min_confidence", 0.35f));
            var sortBy = NormalizeSort(ReadString(arguments, "sort_by", "distance"));
            var categories = ReadStringSet(arguments, "candidate_categories");
            var resourceTypes = ReadStringSet(arguments, "likely_resource_types");
            var ignored = new List<string>();
            if (requestedRadius > 24f)
            {
                ignored.Add("radius_clamped_to_24");
            }
            var candidates = new List<ResourceCandidateObservation>();

            if (world != null)
            {
                var min = new Vector3i(Mathf.FloorToInt(center.x - radius), Mathf.FloorToInt(center.y - radius), Mathf.FloorToInt(center.z - radius));
                var max = new Vector3i(Mathf.CeilToInt(center.x + radius), Mathf.CeilToInt(center.y + radius), Mathf.CeilToInt(center.z + radius));
                var radiusSqr = radius * radius;
                for (var x = min.x; x <= max.x; x++)
                {
                    for (var y = min.y; y <= max.y; y++)
                    {
                        for (var z = min.z; z <= max.z; z++)
                        {
                            var position = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                            if ((position - center).sqrMagnitude > radiusSqr)
                            {
                                continue;
                            }

                            var blockPos = new Vector3i(x, y, z);
                            var blockValue = world.GetBlock(blockPos);
                            if (IsAirBlock(blockValue))
                            {
                                continue;
                            }

                            var candidate = BuildResourceCandidate(world, player, blockPos, blockValue);
                            if (candidate == null || candidate.CandidateConfidence < minConfidence)
                            {
                                continue;
                            }

                            if (categories.Count > 0 && !categories.Contains(candidate.CandidateCategory))
                            {
                                continue;
                            }

                            if (resourceTypes.Count > 0 && !resourceTypes.Contains(candidate.LikelyResourceType))
                            {
                                continue;
                            }

                            if (includeExposedOnly && !candidate.IsExposed)
                            {
                                continue;
                            }

                            if (includeSurfaceOnly && (!candidate.IsExposed || candidate.Position == null || candidate.Position.Y < center.y - 2f))
                            {
                                continue;
                            }

                            candidates.Add(candidate);
                        }
                    }
                }
            }

            var ordered = SortResourceCandidates(candidates, sortBy).Take(maxResults).ToList();
            return new ResourceCandidateQueryResult
            {
                Center = ToVector3State(center),
                Radius = radius,
                MaxResults = maxResults,
                SortBy = sortBy,
                Count = ordered.Count,
                IgnoredFilters = ignored,
                Candidates = ordered,
                Note = ordered.Count == 0 ? "No resource candidates matched the current filters." : "Resource candidates were derived from nearby block inspection."
            };
        }

        public InteractableQueryResult QueryInteractablesInRadius(IDictionary<string, object> arguments)
        {
            var player = ResolvePlayer();
            var world = ResolveWorld();
            var center = ReadCenter(arguments, player == null ? Vector3.zero : player.position);
            var requestedRadius = ReadFloat(arguments, "radius", 8f);
            var radius = Mathf.Clamp(requestedRadius, 1f, 24f);
            var maxResults = Mathf.Clamp(ReadInt(arguments, "max_results", 12), 1, 64);
            var includeBlocks = ReadBool(arguments, "include_blocks", true);
            var includeEntities = ReadBool(arguments, "include_entities", true);
            var includeLoot = ReadBool(arguments, "include_loot", true);
            var includeDoors = ReadBool(arguments, "include_doors", true);
            var includeVehicles = ReadBool(arguments, "include_vehicles", true);
            var includeNpcs = ReadBool(arguments, "include_npcs", true);
            var includeTraders = ReadBool(arguments, "include_traders", true);
            var includeLocked = ReadBool(arguments, "include_locked", true);
            var interactables = new List<InteractableObservation>();
            var ignored = new List<string>();
            if (requestedRadius > 24f)
            {
                ignored.Add("radius_clamped_to_24");
            }

            if (world != null && includeBlocks)
            {
                var min = new Vector3i(Mathf.FloorToInt(center.x - radius), Mathf.FloorToInt(center.y - radius), Mathf.FloorToInt(center.z - radius));
                var max = new Vector3i(Mathf.CeilToInt(center.x + radius), Mathf.CeilToInt(center.y + radius), Mathf.CeilToInt(center.z + radius));
                var radiusSqr = radius * radius;
                for (var x = min.x; x <= max.x; x++)
                {
                    for (var y = min.y; y <= max.y; y++)
                    {
                        for (var z = min.z; z <= max.z; z++)
                        {
                            var position = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                            if ((position - center).sqrMagnitude > radiusSqr)
                            {
                                continue;
                            }

                            var blockPos = new Vector3i(x, y, z);
                            var blockValue = world.GetBlock(blockPos);
                            if (IsAirBlock(blockValue))
                            {
                                continue;
                            }

                            var interactable = BuildInteractableFromBlock(world, player, blockPos, blockValue, includeLoot, includeDoors, includeLocked);
                            if (interactable != null)
                            {
                                interactables.Add(interactable);
                            }
                        }
                    }
                }
            }

            if (world != null && includeEntities)
            {
                foreach (var entity in EnumerateEntities(world, player, center, radius, true))
                {
                    if (entity.Kind == "vehicle" && !includeVehicles)
                    {
                        continue;
                    }

                    if (entity.Kind == "npc" && !includeNpcs && !includeTraders)
                    {
                        continue;
                    }

                    if (entity.Kind == "neutral" && !includeLoot)
                    {
                        continue;
                    }

                    interactables.Add(new InteractableObservation
                    {
                        Kind = entity.Kind,
                        Id = entity.EntityId,
                        Name = entity.EntityName,
                        Position = entity.Position,
                        Distance = entity.Distance,
                        CanInteract = entity.CanInteract,
                        InteractionPromptText = InferPromptText(entity.Kind, entity.EntityName, entity.Kind == "neutral", true),
                        InteractionActionKind = InferActionKind(entity.Kind, entity.EntityName, entity.Kind == "neutral", true),
                        Locked = "unknown",
                        Powered = "unknown",
                        Active = entity.Alive,
                        LineOfSightClear = entity.LineOfSightClear,
                        Note = entity.Note
                    });
                }
            }

            var ordered = interactables.OrderBy(item => item.Distance).ThenBy(item => item.Name ?? UnknownText, StringComparer.OrdinalIgnoreCase).Take(maxResults).ToList();
            return new InteractableQueryResult
            {
                Center = ToVector3State(center),
                Radius = radius,
                MaxResults = maxResults,
                Count = ordered.Count,
                IgnoredFilters = ignored,
                Interactables = ordered,
                Note = ordered.Count == 0 ? "No nearby interactables matched the current filters." : "Interactables were assembled from nearby block and entity inspection."
            };
        }

        public EntityQueryResult QueryEntitiesInRadius(IDictionary<string, object> arguments)
        {
            var player = ResolvePlayer();
            var world = ResolveWorld();
            var center = ReadCenter(arguments, player == null ? Vector3.zero : player.position);
            var requestedRadius = ReadFloat(arguments, "radius", 16f);
            var radius = Mathf.Clamp(requestedRadius, 1f, 64f);
            var maxResults = Mathf.Clamp(ReadInt(arguments, "max_results", 16), 1, 128);
            var includeHostile = ReadBool(arguments, "include_hostile", true);
            var includeNpc = ReadBool(arguments, "include_npc", true);
            var includeAnimals = ReadBool(arguments, "include_animals", true);
            var includeNeutral = ReadBool(arguments, "include_neutral", true);
            var includeDead = ReadBool(arguments, "include_dead", false);
            var entities = new List<EntityObservation>();
            var ignored = new List<string>();
            if (requestedRadius > 64f)
            {
                ignored.Add("radius_clamped_to_64");
            }

            if (world != null)
            {
                foreach (var entity in EnumerateEntities(world, player, center, radius, includeDead))
                {
                    if (entity.Kind == "enemy" && !includeHostile) continue;
                    if (entity.Kind == "npc" && !includeNpc) continue;
                    if (entity.Kind == "animal" && !includeAnimals) continue;
                    if ((entity.Kind == "neutral" || entity.Kind == "vehicle" || entity.Kind == "unknown") && !includeNeutral) continue;
                    entities.Add(entity);
                }
            }

            var ordered = entities.OrderBy(item => item.Distance).ThenBy(item => item.EntityName ?? UnknownText, StringComparer.OrdinalIgnoreCase).Take(maxResults).ToList();
            return new EntityQueryResult
            {
                Center = ToVector3State(center),
                Radius = radius,
                MaxResults = maxResults,
                Count = ordered.Count,
                IgnoredFilters = ignored,
                Entities = ordered,
                Note = ordered.Count == 0 ? "No nearby entities matched the current filters." : "Entities were gathered from the world bounds query."
            };
        }

        public EnvironmentSummary GetEnvironmentSummary()
        {
            return GetCached(ref cachedEnvironmentSummary, ref cachedEnvironmentSummaryUtc, 250, BuildEnvironmentSummary);
        }

        private EnvironmentSummary BuildEnvironmentSummary()
        {
            var player = ResolvePlayer();
            var world = ResolveWorld();
            if (player == null || world == null)
            {
                return new EnvironmentSummary { CurrentBiome = UnknownText, FootBlockName = UnknownText, FootBlockId = UnknownText, Note = "Player or world was unavailable." };
            }

            var terrain = GetTerrainSummary();
            return new EnvironmentSummary
            {
                CurrentBiome = ResolveBiome(player),
                FootBlockName = terrain.FootBlockName,
                FootBlockId = terrain.FootBlockId,
                IndoorsHint = terrain.IndoorsHint,
                WaterNearbyHint = terrain.WaterNearbyHint,
                FallHazardAheadHint = terrain.FallHazardAheadHint,
                LocalHeightSpan = terrain.HeightSpan,
                Note = "Environment summary was derived from nearby terrain and biome sampling."
            };
        }

        public BiomeInfo GetBiomeInfo()
        {
            return GetCached(ref cachedBiomeInfo, ref cachedBiomeInfoUtc, 250, BuildBiomeInfo);
        }

        private BiomeInfo BuildBiomeInfo()
        {
            var player = ResolvePlayer();
            var world = ResolveWorld();
            if (player == null || world == null)
            {
                return new BiomeInfo { CurrentBiome = UnknownText, BiomeIntensity = UnknownText, HazardHint = UnknownText, Note = "Player or world was unavailable." };
            }

            var intensity = InvokeMember(world, "GetBiomeIntensity", new Vector3i(player.position));
            return new BiomeInfo
            {
                CurrentBiome = ResolveBiome(player),
                BiomeIntensity = intensity == null ? UnknownText : intensity.ToString(),
                IndoorsHint = TryIsOpenSkyAbove(world, player.position),
                HazardHint = InferBiomeHazard(ResolveBiome(player)),
                Note = "Biome information was derived from the world biome state."
            };
        }

        public TerrainSummary GetTerrainSummary()
        {
            return GetCached(ref cachedTerrainSummary, ref cachedTerrainSummaryUtc, 200, BuildTerrainSummary);
        }

        private TerrainSummary BuildTerrainSummary()
        {
            var player = ResolvePlayer();
            var world = ResolveWorld();
            if (player == null || world == null)
            {
                return new TerrainSummary { FootBlockName = UnknownText, FootBlockId = UnknownText, Note = "Player or world was unavailable." };
            }

            return GetTerrainSummaryInternal(player, world);
        }

        public NearbyResourceCandidatesSummary GetNearbyResourceCandidatesSummary()
        {
            return GetCached(ref cachedNearbyResourceCandidatesSummary, ref cachedNearbyResourceCandidatesSummaryUtc, 350, BuildNearbyResourceCandidatesSummary);
        }

        private NearbyResourceCandidatesSummary BuildNearbyResourceCandidatesSummary()
        {
            var result = QueryResourceCandidates(new Dictionary<string, object>
            {
                { "radius", 10f },
                { "max_results", 5 },
                { "include_surface_only", true },
                { "include_exposed_only", true },
                { "sort_by", "distance" }
            });
            return new NearbyResourceCandidatesSummary
            {
                Count = result.Count,
                TopCandidates = result.Candidates
            };
        }

        public NearbyInteractablesSummary GetNearbyInteractablesSummary()
        {
            return GetCached(ref cachedNearbyInteractablesSummary, ref cachedNearbyInteractablesSummaryUtc, 350, BuildNearbyInteractablesSummary);
        }

        private NearbyInteractablesSummary BuildNearbyInteractablesSummary()
        {
            var player = ResolvePlayer();
            var world = ResolveWorld();
            if (player == null || world == null)
            {
                return new NearbyInteractablesSummary
                {
                    Count = 0,
                    TopInteractables = new List<InteractableObservation>()
                };
            }

            var result = QueryInteractablesInRadius(new Dictionary<string, object>
            {
                { "radius", 8f },
                { "max_results", 5 },
                { "include_blocks", true },
                { "include_entities", true },
                { "include_loot", true },
                { "include_doors", true },
                { "include_vehicles", true },
                { "include_npcs", true },
                { "include_traders", true },
                { "include_locked", true }
            });
            return new NearbyInteractablesSummary
            {
                Count = result.Count,
                TopInteractables = result.Interactables
            };
        }

        public NearbyEntitiesSummary GetNearbyEntitiesSummary()
        {
            return GetCached(ref cachedNearbyEntitiesSummary, ref cachedNearbyEntitiesSummaryUtc, 350, BuildNearbyEntitiesSummary);
        }

        private NearbyEntitiesSummary BuildNearbyEntitiesSummary()
        {
            var player = ResolvePlayer();
            var world = ResolveWorld();
            if (player == null || world == null)
            {
                return new NearbyEntitiesSummary
                {
                    HostileCount = 0,
                    NpcCount = 0,
                    NearestHostileDistance = null,
                    TopEntities = new List<EntityObservation>()
                };
            }

            var result = QueryEntitiesInRadius(new Dictionary<string, object>
            {
                { "radius", 16f },
                { "max_results", 8 },
                { "include_hostile", true },
                { "include_npc", true },
                { "include_animals", true },
                { "include_neutral", true },
                { "include_dead", false }
            });
            var hostiles = result.Entities.Where(item => item.Hostile).ToList();
            var npcs = result.Entities.Where(item => string.Equals(item.Kind, "npc", StringComparison.OrdinalIgnoreCase)).ToList();
            return new NearbyEntitiesSummary
            {
                HostileCount = hostiles.Count,
                NpcCount = npcs.Count,
                NearestHostileDistance = hostiles.Count == 0 ? (float?)null : hostiles.Min(item => item.Distance),
                TopEntities = result.Entities.Take(5).ToList()
            };
        }

        private T GetCached<T>(ref T cachedValue, ref DateTime cachedUtc, int ttlMs, Func<T> factory)
            where T : class
        {
            var nowUtc = DateTime.UtcNow;
            lock (cacheSyncRoot)
            {
                if (cachedValue != null && (nowUtc - cachedUtc).TotalMilliseconds < ttlMs)
                {
                    return cachedValue;
                }
            }

            var fresh = factory();
            lock (cacheSyncRoot)
            {
                cachedValue = fresh;
                cachedUtc = nowUtc;
                return fresh;
            }
        }

        private LookTargetObservation BuildLookTargetFromEntity(EntityPlayerLocal player, EntityObservation entity, Vector3 hitPoint)
        {
            return new LookTargetObservation
            {
                HasTarget = true,
                Source = "raycast",
                TargetKind = NormalizeLookTargetKind(entity.Kind, entity.EntityName),
                TargetName = entity.EntityName ?? UnknownText,
                TargetClass = entity.EntityClass ?? UnknownText,
                TargetId = entity.EntityId ?? UnknownText,
                EntityId = entity.EntityId,
                BlockId = null,
                Distance = Vector3.Distance(player.GetLookRay().origin, hitPoint),
                Position = entity.Position,
                CanInteract = entity.CanInteract,
                InteractionPromptText = InferPromptText(entity.Kind, entity.EntityName, entity.Kind == "neutral", true),
                InteractionActionKind = InferActionKind(entity.Kind, entity.EntityName, entity.Kind == "neutral", true),
                Hostile = entity.Hostile,
                Alive = entity.Alive,
                Locked = "unknown",
                Powered = "unknown",
                Active = entity.Alive,
                LineOfSightClear = true,
                IsResourceCandidate = entity.Kind == "enemy",
                CandidateCategory = InferEntityCandidateCategory(entity.Kind),
                CandidateConfidence = InferEntityConfidence(entity.Kind, entity.EntityName),
                LikelyResourceType = InferLikelyResourceTypeFromEntity(entity.Kind, entity.EntityName),
                Durability = "unknown",
                MaxDurability = "unknown",
                Note = entity.Note
            };
        }

        private LookTargetObservation BuildLookTargetFromBlock(World world, EntityPlayerLocal player, Vector3i blockPos, BlockValue blockValue, Vector3 hitPoint)
        {
            var blockName = ResolveBlockName(blockValue);
            var tileEntity = world.GetTileEntity(blockPos);
            var kind = InferBlockTargetKind(blockName, tileEntity);
            var candidate = BuildBlockCandidateMetadata(blockName, tileEntity);
            return new LookTargetObservation
            {
                HasTarget = true,
                Source = "raycast",
                TargetKind = kind,
                TargetName = string.IsNullOrWhiteSpace(blockName) ? UnknownText : blockName,
                TargetClass = ResolveBlockClass(blockValue),
                TargetId = $"block:{blockPos.x},{blockPos.y},{blockPos.z}",
                EntityId = null,
                BlockId = ReadBlockId(blockValue).HasValue ? (object)ReadBlockId(blockValue).Value : UnknownText,
                Distance = Vector3.Distance(player.GetLookRay().origin, hitPoint),
                Position = ToVector3State(new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f)),
                CanInteract = CanInteractWithBlock(kind),
                InteractionPromptText = InferPromptText(kind, blockName, kind == "loot" || kind == "container", kind == "interactable"),
                InteractionActionKind = InferActionKind(kind, blockName, kind == "loot" || kind == "container", kind == "interactable"),
                Hostile = false,
                Alive = false,
                Locked = TryReadLockState(tileEntity, blockValue),
                Powered = TryReadPowerState(tileEntity, blockValue),
                Active = TryReadActiveState(tileEntity, blockValue),
                LineOfSightClear = true,
                IsResourceCandidate = candidate.IsResourceCandidate,
                CandidateCategory = candidate.CandidateCategory,
                CandidateConfidence = candidate.CandidateConfidence,
                LikelyResourceType = candidate.LikelyResourceType,
                Durability = ReadDurability(blockValue, tileEntity),
                MaxDurability = ReadMaxDurability(blockValue, tileEntity),
                Note = candidate.Note
            };
        }

        private InteractionContextObservation BuildInteractionContext(LookTargetObservation lookTarget)
        {
            if (lookTarget == null || !lookTarget.HasTarget)
            {
                return new InteractionContextObservation
                {
                    HasFocusTarget = false,
                    CanInteractNow = false,
                    SuggestedActionKind = "none",
                    PromptText = UnknownText,
                    TargetKind = "none",
                    TargetName = UnknownText,
                    Distance = 0f,
                    Source = lookTarget == null ? "unknown" : lookTarget.Source,
                    RequiresPreciseAlignment = false,
                    RecommendedInteractDistanceMin = "unknown",
                    RecommendedInteractDistanceMax = "unknown",
                    Note = "There was no current focus target."
                };
            }

            var actionKind = lookTarget.InteractionActionKind ?? "unknown";
            var minDistance = InferRecommendedInteractMin(lookTarget.TargetKind, actionKind);
            var maxDistance = InferRecommendedInteractMax(lookTarget.TargetKind, actionKind);
            return new InteractionContextObservation
            {
                HasFocusTarget = true,
                CanInteractNow = lookTarget.CanInteract && lookTarget.Distance >= minDistance && lookTarget.Distance <= maxDistance,
                SuggestedActionKind = actionKind,
                PromptText = string.IsNullOrWhiteSpace(lookTarget.InteractionPromptText) ? UnknownText : lookTarget.InteractionPromptText,
                TargetKind = lookTarget.TargetKind ?? "unknown",
                TargetName = string.IsNullOrWhiteSpace(lookTarget.TargetName) ? UnknownText : lookTarget.TargetName,
                Distance = lookTarget.Distance,
                Source = lookTarget.Source ?? "unknown",
                RequiresPreciseAlignment = lookTarget.Distance > 3f || string.Equals(lookTarget.TargetKind, "resource", StringComparison.OrdinalIgnoreCase),
                RecommendedInteractDistanceMin = minDistance,
                RecommendedInteractDistanceMax = maxDistance,
                Note = "Interaction context was derived from the current look target."
            };
        }

        private ResourceCandidateObservation BuildResourceCandidate(World world, EntityPlayerLocal player, Vector3i blockPos, BlockValue blockValue)
        {
            var blockName = ResolveBlockName(blockValue);
            var tileEntity = world.GetTileEntity(blockPos);
            var candidate = BuildBlockCandidateMetadata(blockName, tileEntity);
            if (!candidate.IsResourceCandidate)
            {
                return null;
            }

            var worldPos = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
            var origin = player == null ? worldPos : player.position;
            var exposed = IsBlockExposed(world, blockPos);
            return new ResourceCandidateObservation
            {
                Name = string.IsNullOrWhiteSpace(blockName) ? UnknownText : blockName,
                BlockId = ReadBlockId(blockValue).HasValue ? (object)ReadBlockId(blockValue).Value : UnknownText,
                Position = ToVector3State(worldPos),
                Distance = Vector3.Distance(origin, worldPos),
                CandidateCategory = candidate.CandidateCategory,
                CandidateConfidence = candidate.CandidateConfidence,
                LikelyResourceType = candidate.LikelyResourceType,
                IsExposed = exposed,
                Biome = ResolveBiome(player),
                LineOfSightClear = player == null ? "unknown" : ComputeLineOfSight(player.position, worldPos),
                ReachableHint = player == null ? "unknown" : (object)(Mathf.Abs(worldPos.y - player.position.y) <= 3.5f),
                Note = candidate.Note
            };
        }

        private bool TryBuildLookTargetFromHitInfo(World world, EntityPlayerLocal player, out LookTargetObservation observation)
        {
            observation = null;
            var hitInfo = ReadMember(player, "HitInfo");
            if (hitInfo == null || !TryReadBool(hitInfo, "bHitValid").GetValueOrDefault(false))
            {
                return false;
            }

            var hitDetails = ReadMember(hitInfo, "hit");
            var hitPoint = TryReadVector3(hitDetails, "pos")
                ?? TryReadVector3(hitInfo, "point")
                ?? TryReadVector3(ReadMember(hitInfo, "ray"), "origin")
                ?? player.GetLookRay().origin;

            var entity = FindNearestEntity(world, player, hitPoint, 2.2f);
            if (entity != null)
            {
                observation = BuildLookTargetFromEntity(player, entity, hitPoint);
                observation.Source = "focus_context";
                observation.Note = "Resolved from EntityPlayerLocal.HitInfo.";
                return true;
            }

            var blockPos = TryReadVector3i(hitDetails, "blockPos")
                ?? TryReadVector3i(hitInfo, "lastBlockPos")
                ?? ResolveBlockPosition(hitPoint, Vector3.zero, player.GetLookRay().direction);
            var blockValue = TryReadHitBlockValue(hitDetails);
            if (IsAirBlock(blockValue))
            {
                blockValue = world.GetBlock(blockPos);
            }

            if (IsAirBlock(blockValue))
            {
                return false;
            }

            observation = BuildLookTargetFromBlock(world, player, blockPos, blockValue, hitPoint);
            observation.Source = "focus_context";
            observation.Note = "Resolved from EntityPlayerLocal.HitInfo.";
            if (TryPromoteAlignedInteractableTarget(world, player, observation, out var promotedTarget))
            {
                promotedTarget.Source = "focus_context";
                promotedTarget.Note = "Promoted an aligned interactable over a foreground focus hit.";
                observation = promotedTarget;
            }

            return true;
        }

        private bool TryBuildLookTargetFromNearbyFocus(World world, EntityPlayerLocal player, out LookTargetObservation observation)
        {
            observation = null;
            var eye = player.GetLookRay().origin;
            var forward = player.GetLookRay().direction.normalized;

            var interactables = QueryInteractablesInRadius(new Dictionary<string, object>
            {
                { "center", new Dictionary<string, object> { { "x", player.position.x }, { "y", player.position.y }, { "z", player.position.z } } },
                { "radius", 8f },
                { "max_results", 12 },
                { "include_blocks", true },
                { "include_entities", true },
                { "include_loot", true },
                { "include_doors", true },
                { "include_vehicles", true },
                { "include_npcs", true },
                { "include_traders", true },
                { "include_locked", true }
            }).Interactables;

            var bestInteractable = interactables
                .Where(item => item != null && item.Position != null && item.Distance <= 8f && ToBool(item.LineOfSightClear))
                .Select(item =>
                {
                    var offset = ToVector3(item.Position) - eye;
                    var normalized = offset.sqrMagnitude <= 0.0001f ? forward : offset.normalized;
                    return new
                    {
                        Item = item,
                        Alignment = Vector3.Dot(forward, normalized),
                        LateralDistance = Vector3.Cross(forward, normalized).magnitude
                    };
                })
                .Where(item => item.Alignment >= 0.8f && item.LateralDistance <= 0.6f)
                .OrderByDescending(item => item.Alignment)
                .ThenBy(item => item.LateralDistance)
                .ThenBy(item => item.Item.Distance)
                .FirstOrDefault();

            if (bestInteractable != null)
            {
                observation = BuildLookTargetFromInteractable(bestInteractable.Item);
                observation.Source = "interaction_context";
                observation.Note = "Resolved from the nearby interactable focus cone fallback.";
                return true;
            }

            var resources = QueryResourceCandidates(new Dictionary<string, object>
            {
                { "center", new Dictionary<string, object> { { "x", player.position.x }, { "y", player.position.y }, { "z", player.position.z } } },
                { "radius", 8f },
                { "max_results", 12 },
                { "include_surface_only", false },
                { "include_exposed_only", false },
                { "sort_by", "distance" }
            }).Candidates;

            var bestResource = resources
                .Where(item => item != null && item.Position != null && item.Distance <= 8f && ToBool(item.LineOfSightClear))
                .Select(item =>
                {
                    var offset = ToVector3(item.Position) - eye;
                    var normalized = offset.sqrMagnitude <= 0.0001f ? forward : offset.normalized;
                    return new
                    {
                        Item = item,
                        Alignment = Vector3.Dot(forward, normalized),
                        LateralDistance = Vector3.Cross(forward, normalized).magnitude
                    };
                })
                .Where(item => item.Alignment >= 0.75f && item.LateralDistance <= 0.55f)
                .OrderByDescending(item => item.Alignment)
                .ThenBy(item => item.LateralDistance)
                .ThenBy(item => item.Item.Distance)
                .FirstOrDefault();

            if (bestResource != null)
            {
                observation = BuildLookTargetFromResourceCandidate(bestResource.Item);
                observation.Source = "interaction_context";
                observation.Note = "Resolved from the nearby resource focus cone fallback.";
                return true;
            }

            var entities = QueryEntitiesInRadius(new Dictionary<string, object>
            {
                { "center", new Dictionary<string, object> { { "x", player.position.x }, { "y", player.position.y }, { "z", player.position.z } } },
                { "radius", 64f },
                { "max_results", 16 },
                { "include_hostile", true },
                { "include_npc", true },
                { "include_animals", true },
                { "include_neutral", true },
                { "include_dead", false }
            }).Entities;

            var bestEntity = entities
                .Where(item => item != null && item.Position != null && IsEntityPromotionCandidate(item) && ToBool(item.LineOfSightClear))
                .Select(item =>
                {
                    var offset = ToVector3(item.Position) - eye;
                    var normalized = offset.sqrMagnitude <= 0.0001f ? forward : offset.normalized;
                    return new
                    {
                        Item = item,
                        Alignment = Vector3.Dot(forward, normalized),
                        LateralDistance = Vector3.Cross(forward, normalized).magnitude
                    };
                })
                .Where(item => item.Alignment >= 0.97f && item.LateralDistance <= 0.25f)
                .OrderByDescending(item => item.Alignment)
                .ThenBy(item => item.LateralDistance)
                .ThenBy(item => item.Item.Distance)
                .FirstOrDefault();

            if (bestEntity != null)
            {
                observation = BuildLookTargetFromEntity(player, bestEntity.Item, ToVector3(bestEntity.Item.Position));
                observation.Source = "interaction_context";
                observation.Note = "Resolved from the nearby entity focus cone fallback.";
                return true;
            }

            return false;
        }

        private bool TryPromoteAlignedInteractableTarget(World world, EntityPlayerLocal player, LookTargetObservation currentTarget, out LookTargetObservation promotedTarget)
        {
            promotedTarget = null;
            if (world == null || player == null || currentTarget == null || !currentTarget.HasTarget || !ShouldTryInteractablePromotion(currentTarget))
            {
                return false;
            }

            var eye = player.GetLookRay().origin;
            var forward = player.GetLookRay().direction.normalized;
            var searchRadius = Mathf.Clamp(Mathf.Max(currentTarget.Distance + 3.0f, 5.5f), 3f, 8f);
            var interactables = QueryInteractablesInRadius(new Dictionary<string, object>
            {
                { "center", new Dictionary<string, object> { { "x", player.position.x }, { "y", player.position.y }, { "z", player.position.z } } },
                { "radius", searchRadius },
                { "max_results", 16 },
                { "include_blocks", true },
                { "include_entities", true },
                { "include_loot", true },
                { "include_doors", true },
                { "include_vehicles", true },
                { "include_npcs", true },
                { "include_traders", true },
                { "include_locked", true }
            }).Interactables;

            var bestInteractable = interactables
                .Where(item => item != null && item.Position != null && IsPromotionCandidate(item))
                .Select(item =>
                {
                    var targetPosition = ToVector3(item.Position);
                    var offset = targetPosition - eye;
                    var normalized = offset.sqrMagnitude <= 0.0001f ? forward : offset.normalized;
                    return new
                    {
                        Item = item,
                        Alignment = Vector3.Dot(forward, normalized),
                        LateralDistance = Vector3.Cross(forward, normalized).magnitude,
                        Priority = GetInteractablePromotionPriority(item),
                        DistanceDelta = item.Distance - currentTarget.Distance
                    };
                })
                .Where(item =>
                    item.Alignment >= 0.82f
                    && item.LateralDistance <= 0.62f
                    && item.DistanceDelta >= -0.5f
                    && item.Item.Distance <= searchRadius)
                .OrderByDescending(item => item.Priority)
                .ThenByDescending(item => item.Alignment)
                .ThenBy(item => item.LateralDistance)
                .ThenBy(item => item.Item.Distance)
                .FirstOrDefault();

            if (bestInteractable == null)
            {
                var bestEntity = QueryEntitiesInRadius(new Dictionary<string, object>
                {
                    { "center", new Dictionary<string, object> { { "x", player.position.x }, { "y", player.position.y }, { "z", player.position.z } } },
                    { "radius", 64f },
                    { "max_results", 16 },
                    { "include_hostile", true },
                    { "include_npc", true },
                    { "include_animals", true },
                    { "include_neutral", true },
                    { "include_dead", false }
                }).Entities
                    .Where(item => item != null && item.Position != null && IsEntityPromotionCandidate(item) && ToBool(item.LineOfSightClear))
                    .Select(item =>
                    {
                        var targetPosition = ToVector3(item.Position);
                        var offset = targetPosition - eye;
                        var normalized = offset.sqrMagnitude <= 0.0001f ? forward : offset.normalized;
                        return new
                        {
                            Item = item,
                            Alignment = Vector3.Dot(forward, normalized),
                            LateralDistance = Vector3.Cross(forward, normalized).magnitude,
                            Priority = GetEntityPromotionPriority(item)
                        };
                    })
                    .Where(item => item.Alignment >= 0.97f && item.LateralDistance <= 0.25f)
                    .OrderByDescending(item => item.Priority)
                    .ThenByDescending(item => item.Alignment)
                    .ThenBy(item => item.Item.Distance)
                    .FirstOrDefault();

                if (bestEntity == null)
                {
                    return false;
                }

                promotedTarget = BuildLookTargetFromEntity(player, bestEntity.Item, ToVector3(bestEntity.Item.Position));
                promotedTarget.Source = "interaction_context";
                promotedTarget.Note = "Promoted an aligned entity over a foreground block hit.";
                return true;
            }

            promotedTarget = BuildLookTargetFromInteractable(bestInteractable.Item);
            return true;
        }

        private InteractableObservation BuildInteractableFromBlock(World world, EntityPlayerLocal player, Vector3i blockPos, BlockValue blockValue, bool includeLoot, bool includeDoors, bool includeLocked)
        {
            var blockName = ResolveBlockName(blockValue);
            var tileEntity = world.GetTileEntity(blockPos);
            var kind = InferBlockTargetKind(blockName, tileEntity);
            var isLoot = kind == "loot" || kind == "container";
            var isDoor = kind == "interactable" && LooksLikeDoor(blockName);
            if (isLoot && !includeLoot)
            {
                return null;
            }

            if (isDoor && !includeDoors)
            {
                return null;
            }

            if (!isLoot && !isDoor && kind != "interactable")
            {
                return null;
            }

            var locked = TryReadLockState(tileEntity, blockValue);
            if (!includeLocked && locked is bool lockedValue && lockedValue)
            {
                return null;
            }

            var worldPos = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
            var origin = player == null ? worldPos : player.position;
            return new InteractableObservation
            {
                Kind = isDoor ? "door" : kind,
                Id = $"block:{blockPos.x},{blockPos.y},{blockPos.z}",
                Name = string.IsNullOrWhiteSpace(blockName) ? UnknownText : blockName,
                Position = ToVector3State(worldPos),
                Distance = Vector3.Distance(origin, worldPos),
                CanInteract = Vector3.Distance(origin, worldPos) <= 4.5f,
                InteractionPromptText = InferPromptText(kind, blockName, isLoot, true),
                InteractionActionKind = InferActionKind(kind, blockName, isLoot, true),
                Locked = locked,
                Powered = TryReadPowerState(tileEntity, blockValue),
                Active = TryReadActiveState(tileEntity, blockValue),
                LineOfSightClear = player == null ? "unknown" : ComputeLineOfSight(player.position, worldPos),
                Note = "Nearby interactable block."
            };
        }

        private TerrainSummary GetTerrainSummaryInternal(EntityPlayerLocal player, World world)
        {
            var center = player.position;
            var sampleRadius = 6f;
            var heights = new List<float>();
            foreach (var sample in GetTerrainSamplePoints(center, sampleRadius))
            {
                heights.Add(SampleGroundHeight(world, sample));
            }

            var footBlockPos = new Vector3i(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.y - 1.2f), Mathf.FloorToInt(center.z));
            var footBlock = world.GetBlock(footBlockPos);
            var forwardGround = SampleGroundHeight(world, center + player.GetLookRay().direction.normalized * 4f);
            return new TerrainSummary
            {
                SampleCenter = ToVector3State(center),
                SampleRadius = sampleRadius,
                MinGroundY = heights.Count == 0 ? center.y : heights.Min(),
                MaxGroundY = heights.Count == 0 ? center.y : heights.Max(),
                HeightSpan = heights.Count == 0 ? 0f : Mathf.Max(0f, heights.Max() - heights.Min()),
                FootBlockName = ResolveBlockName(footBlock),
                FootBlockId = ReadBlockId(footBlock).HasValue ? (object)ReadBlockId(footBlock).Value : UnknownText,
                WaterNearbyHint = SampleWaterNearby(world, center),
                FallHazardAheadHint = center.y - forwardGround > 3.5f,
                IndoorsHint = TryIsOpenSkyAbove(world, center),
                Note = "Terrain summary was sampled from nearby ground heights and the block beneath the player."
            };
        }

        private EntityObservation FindNearestEntity(World world, EntityPlayerLocal player, Vector3 point, float radius)
        {
            return EnumerateEntities(world, player, point, radius, true)
                .OrderBy(item => item.Distance)
                .FirstOrDefault();
        }

        private IEnumerable<EntityObservation> EnumerateEntities(World world, EntityPlayerLocal player, Vector3 center, float radius, bool includeDead)
        {
            var bounds = new Bounds(center, Vector3.one * Mathf.Max(radius * 2f, 1f));
            List<Entity> entities;
            try
            {
                entities = world.GetEntitiesInBounds((Entity)null, bounds) ?? new List<Entity>();
            }
            catch (Exception exception)
            {
                logger.Warn("Entity enumeration skipped because the world entity bounds query was not ready yet: " + exception.Message);
                yield break;
            }

            var seen = new HashSet<int>();
            foreach (var entity in entities)
            {
                if (entity == null || (player != null && entity.entityId == player.entityId) || !seen.Add(entity.entityId))
                {
                    continue;
                }

                var distance = Vector3.Distance(center, entity.position);
                if (distance > radius + 0.5f)
                {
                    continue;
                }

                var observation = BuildEntityObservation(player, entity, distance);
                if (!includeDead && !observation.Alive)
                {
                    continue;
                }

                yield return observation;
            }
        }

        private EntityObservation BuildEntityObservation(EntityPlayerLocal player, Entity entity, float distance)
        {
            var entityClass = entity.GetType().Name;
            var entityName = ResolveEntityName(entity);
            var hostile = InferHostile(entityClass, entityName);
            var kind = InferEntityKind(entityClass, entityName, hostile);
            var alive = InferAlive(entity);
            return new EntityObservation
            {
                EntityId = entity.entityId,
                EntityName = string.IsNullOrWhiteSpace(entityName) ? UnknownText : entityName,
                EntityClass = entityClass,
                Kind = kind,
                Position = ToVector3State(entity.position),
                Distance = distance,
                Alive = alive,
                Hostile = hostile,
                CanInteract = kind == "npc" || kind == "vehicle" || LooksLikeLootEntity(entityClass, entityName),
                CurrentTargetingPlayer = "unknown",
                LineOfSightClear = player == null ? "unknown" : ComputeLineOfSight(player.position, entity.position + Vector3.up),
                Note = "Nearby entity observation."
            };
        }

        private static LookTargetObservation BuildLookTargetFromInteractable(InteractableObservation interactable)
        {
            var targetKind = NormalizeInteractableKind(interactable.Kind);
            return new LookTargetObservation
            {
                HasTarget = true,
                Source = "interaction_context",
                TargetKind = targetKind,
                TargetName = string.IsNullOrWhiteSpace(interactable.Name) ? UnknownText : interactable.Name,
                TargetClass = UnknownText,
                TargetId = interactable.Id ?? UnknownText,
                EntityId = targetKind == "npc" || targetKind == "enemy" || targetKind == "vehicle" ? interactable.Id : null,
                BlockId = targetKind == "block" ? interactable.Id : null,
                Distance = interactable.Distance,
                Position = interactable.Position,
                CanInteract = interactable.CanInteract,
                InteractionPromptText = string.IsNullOrWhiteSpace(interactable.InteractionPromptText) ? UnknownText : interactable.InteractionPromptText,
                InteractionActionKind = string.IsNullOrWhiteSpace(interactable.InteractionActionKind) ? "unknown" : interactable.InteractionActionKind,
                Hostile = targetKind == "enemy",
                Alive = targetKind == "enemy" || targetKind == "npc",
                Locked = interactable.Locked,
                Powered = interactable.Powered,
                Active = interactable.Active,
                LineOfSightClear = interactable.LineOfSightClear,
                IsResourceCandidate = false,
                CandidateCategory = InferCandidateCategoryFromKind(targetKind),
                CandidateConfidence = 0.78f,
                LikelyResourceType = InferLikelyResourceTypeFromTargetKind(targetKind),
                Durability = "unknown",
                MaxDurability = "unknown",
                Note = "Focused target inferred from nearby interactable observation."
            };
        }

        private static LookTargetObservation BuildLookTargetFromResourceCandidate(ResourceCandidateObservation candidate)
        {
            return new LookTargetObservation
            {
                HasTarget = true,
                Source = "interaction_context",
                TargetKind = candidate.CandidateCategory == "loot_container" ? "loot" : "resource",
                TargetName = string.IsNullOrWhiteSpace(candidate.Name) ? UnknownText : candidate.Name,
                TargetClass = UnknownText,
                TargetId = candidate.BlockId == null ? UnknownText : candidate.BlockId.ToString(),
                EntityId = null,
                BlockId = candidate.BlockId,
                Distance = candidate.Distance,
                Position = candidate.Position,
                CanInteract = candidate.Distance <= 4.5f,
                InteractionPromptText = candidate.CandidateCategory == "loot_container" ? "Search" : InferPromptText("resource", candidate.Name, false, false),
                InteractionActionKind = candidate.CandidateCategory == "loot_container" ? "search" : InferActionKind("resource", candidate.Name, false, false),
                Hostile = false,
                Alive = false,
                Locked = "unknown",
                Powered = "unknown",
                Active = "unknown",
                LineOfSightClear = candidate.LineOfSightClear,
                IsResourceCandidate = true,
                CandidateCategory = string.IsNullOrWhiteSpace(candidate.CandidateCategory) ? "unknown" : candidate.CandidateCategory,
                CandidateConfidence = candidate.CandidateConfidence,
                LikelyResourceType = string.IsNullOrWhiteSpace(candidate.LikelyResourceType) ? "unknown" : candidate.LikelyResourceType,
                Durability = "unknown",
                MaxDurability = "unknown",
                Note = "Focused target inferred from nearby resource observation."
            };
        }

        private static IEnumerable<Vector3> GetTerrainSamplePoints(Vector3 center, float radius)
        {
            yield return center;
            yield return center + new Vector3(radius, 0f, 0f);
            yield return center + new Vector3(-radius, 0f, 0f);
            yield return center + new Vector3(0f, 0f, radius);
            yield return center + new Vector3(0f, 0f, -radius);
            yield return center + new Vector3(radius * 0.7f, 0f, radius * 0.7f);
            yield return center + new Vector3(radius * 0.7f, 0f, -radius * 0.7f);
            yield return center + new Vector3(-radius * 0.7f, 0f, radius * 0.7f);
            yield return center + new Vector3(-radius * 0.7f, 0f, -radius * 0.7f);
        }

        private static float SampleGroundHeight(World world, Vector3 point)
        {
            if (Physics.Raycast(point + Vector3.up * 20f, Vector3.down, out var hit, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            var origin = new Vector3i(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y), Mathf.FloorToInt(point.z));
            for (var offset = 8; offset >= -16; offset--)
            {
                var candidate = new Vector3i(origin.x, origin.y + offset, origin.z);
                if (!IsAirBlock(world.GetBlock(candidate)))
                {
                    return candidate.y + 1f;
                }
            }

            return point.y;
        }

        private static BlockValue TryReadHitBlockValue(object hitDetails)
        {
            if (hitDetails == null)
            {
                return default(BlockValue);
            }

            try
            {
                var property = hitDetails.GetType().GetProperty("blockValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null)
                {
                    var value = property.GetValue(hitDetails, null);
                    if (value is BlockValue blockValue)
                    {
                        return blockValue;
                    }
                }

                var method = hitDetails.GetType().GetMethod("get_blockValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    var value = method.Invoke(hitDetails, null);
                    if (value is BlockValue blockValue)
                    {
                        return blockValue;
                    }
                }
            }
            catch
            {
            }

            return default(BlockValue);
        }

        private static bool SampleWaterNearby(World world, Vector3 center)
        {
            for (var x = -2; x <= 2; x++)
            {
                for (var z = -2; z <= 2; z++)
                {
                    for (var y = -1; y <= 1; y++)
                    {
                        var block = world.GetBlock(new Vector3i(Mathf.FloorToInt(center.x) + x, Mathf.FloorToInt(center.y) + y, Mathf.FloorToInt(center.z) + z));
                        if (ResolveBlockName(block).IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static Vector3i ResolveBlockPosition(Vector3 point, Vector3 normal, Vector3 rayDirection)
        {
            var adjusted = point - rayDirection.normalized * 0.02f;
            if (normal != Vector3.zero)
            {
                adjusted -= normal.normalized * 0.02f;
            }

            return new Vector3i(Mathf.FloorToInt(adjusted.x), Mathf.FloorToInt(adjusted.y), Mathf.FloorToInt(adjusted.z));
        }

        private static object ComputeLineOfSight(Vector3 from, Vector3 to)
        {
            try
            {
                if (!Physics.Linecast(from + Vector3.up * 1.5f, to, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    return true;
                }

                return Vector3.Distance(hit.point, to) <= 1f;
            }
            catch
            {
                return "unknown";
            }
        }

        private static Vector3 ToVector3(Vector3State value)
        {
            return value == null ? Vector3.zero : new Vector3(value.X, value.Y, value.Z);
        }

        private static bool ToBool(object value)
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }

            try
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }
        }

        private static bool ShouldTryInteractablePromotion(LookTargetObservation currentTarget)
        {
            var targetKind = currentTarget.TargetKind ?? "unknown";
            return string.Equals(targetKind, "resource", StringComparison.OrdinalIgnoreCase)
                || string.Equals(targetKind, "block", StringComparison.OrdinalIgnoreCase)
                || string.Equals(targetKind, "terrain", StringComparison.OrdinalIgnoreCase)
                || string.Equals(targetKind, "unknown", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPromotionCandidate(InteractableObservation interactable)
        {
            if (interactable == null || interactable.Position == null)
            {
                return false;
            }

            var kind = interactable.Kind ?? "unknown";
            return string.Equals(kind, "loot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "container", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "enemy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "door", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "interactable", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "npc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "trader", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "vehicle", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetInteractablePromotionPriority(InteractableObservation interactable)
        {
            if (interactable == null)
            {
                return 0;
            }

            var kind = interactable.Kind ?? "unknown";
            if (string.Equals(kind, "enemy", StringComparison.OrdinalIgnoreCase))
            {
                return 6;
            }

            if (string.Equals(kind, "loot", StringComparison.OrdinalIgnoreCase) || string.Equals(kind, "container", StringComparison.OrdinalIgnoreCase))
            {
                return 5;
            }

            if (string.Equals(kind, "door", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (string.Equals(kind, "interactable", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (string.Equals(kind, "trader", StringComparison.OrdinalIgnoreCase) || string.Equals(kind, "npc", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (string.Equals(kind, "vehicle", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 0;
        }

        private static bool IsEntityPromotionCandidate(EntityObservation entity)
        {
            if (entity == null || entity.Position == null || !entity.Alive)
            {
                return false;
            }

            var kind = entity.Kind ?? "unknown";
            return string.Equals(kind, "enemy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "npc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "vehicle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "animal", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetEntityPromotionPriority(EntityObservation entity)
        {
            if (entity == null)
            {
                return 0;
            }

            var kind = entity.Kind ?? "unknown";
            if (string.Equals(kind, "enemy", StringComparison.OrdinalIgnoreCase))
            {
                return 5;
            }

            if (string.Equals(kind, "npc", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (string.Equals(kind, "vehicle", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (string.Equals(kind, "animal", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return 1;
        }

        private static bool IsBlockExposed(World world, Vector3i blockPos)
        {
            var neighbors = new[]
            {
                new Vector3i(blockPos.x + 1, blockPos.y, blockPos.z),
                new Vector3i(blockPos.x - 1, blockPos.y, blockPos.z),
                new Vector3i(blockPos.x, blockPos.y + 1, blockPos.z),
                new Vector3i(blockPos.x, blockPos.y - 1, blockPos.z),
                new Vector3i(blockPos.x, blockPos.y, blockPos.z + 1),
                new Vector3i(blockPos.x, blockPos.y, blockPos.z - 1)
            };
            return neighbors.Any(pos => IsAirBlock(world.GetBlock(pos)));
        }

        private static bool IsAirBlock(BlockValue blockValue)
        {
            var blockId = ReadBlockId(blockValue);
            if (!blockId.HasValue || blockId.Value == 0)
            {
                return true;
            }

            var name = ResolveBlockName(blockValue);
            return name.Equals("air", StringComparison.OrdinalIgnoreCase) || name.IndexOf("air", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static EntityPlayerLocal ResolvePlayer()
        {
            var world = ResolveWorld();
            return world == null ? null : world.GetPrimaryPlayer();
        }

        private static World ResolveWorld()
        {
            var manager = GameManager.Instance;
            return manager == null ? null : manager.World;
        }

        private static RotationState ReadRotation(EntityPlayerLocal player)
        {
            if (player == null)
            {
                return null;
            }

            if (player.vp_FPCamera != null)
            {
                return new RotationState
                {
                    Yaw = TryReadFloat(player.vp_FPCamera, "Yaw", "m_Yaw"),
                    Pitch = TryReadFloat(player.vp_FPCamera, "Pitch", "m_Pitch")
                };
            }

            return new RotationState
            {
                Yaw = player.rotation.y,
                Pitch = player.rotation.x
            };
        }

        private static Vector3State ToVector3State(Vector3 value)
        {
            return new Vector3State { X = value.x, Y = value.y, Z = value.z };
        }

        private static LookTargetObservation CreateEmptyLookTarget(string source, Vector3 fallbackPosition, string note)
        {
            return new LookTargetObservation
            {
                HasTarget = false,
                Source = source,
                TargetKind = "none",
                TargetName = UnknownText,
                TargetClass = UnknownText,
                TargetId = UnknownText,
                EntityId = null,
                BlockId = null,
                Distance = 0f,
                Position = ToVector3State(fallbackPosition),
                CanInteract = false,
                InteractionPromptText = UnknownText,
                InteractionActionKind = "none",
                Hostile = false,
                Alive = false,
                Locked = "unknown",
                Powered = "unknown",
                Active = "unknown",
                LineOfSightClear = "unknown",
                IsResourceCandidate = false,
                CandidateCategory = "unknown",
                CandidateConfidence = 0f,
                LikelyResourceType = "unknown",
                Durability = "unknown",
                MaxDurability = "unknown",
                Note = note
            };
        }

        private static string ResolveBiome(EntityPlayerLocal player)
        {
            var biome = ReadMember(player, "biomeStandingOn") ?? ReadMember(player, "BiomeStandingOn") ?? ReadMember(player, "biome");
            var biomeName = NormalizeBiomeName(biome);
            if ((biome == null || string.IsNullOrWhiteSpace(biomeName) || string.Equals(biomeName, UnknownText, StringComparison.OrdinalIgnoreCase)) && player != null)
            {
                var world = GameManager.Instance == null ? null : GameManager.Instance.World;
                if (world != null)
                {
                    biome = InvokeMember(world, "GetBiomeInWorld", Mathf.FloorToInt(player.position.x), Mathf.FloorToInt(player.position.z))
                        ?? InvokeMember(world, "GetBiome", Mathf.FloorToInt(player.position.x), Mathf.FloorToInt(player.position.z));
                    biomeName = NormalizeBiomeName(biome);
                }
            }

            if (biome == null || string.IsNullOrWhiteSpace(biomeName))
            {
                return UnknownText;
            }

            return biomeName;
        }

        private static string NormalizeBiomeName(object biome)
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

        private static string ResolveEntityName(Entity entity)
        {
            var value = InvokeMember(entity, "GetDebugName") ?? InvokeMember(entity, "GetEntityName") ?? ReadMember(entity, "EntityName") ?? ReadMember(entity, "entityName") ?? ReadMember(entity, "name");
            var text = value == null ? null : value.ToString();
            return string.IsNullOrWhiteSpace(text) ? entity.GetType().Name : text;
        }

        private static string ResolveBlockName(BlockValue blockValue)
        {
            var block = ResolveBlock(blockValue);
            var value = InvokeMember(block, "GetBlockName") ?? ReadMember(block, "BlockName");
            var text = value == null ? null : value.ToString();
            return string.IsNullOrWhiteSpace(text) ? (block == null ? UnknownText : block.GetType().Name) : text;
        }

        private static string ResolveBlockClass(BlockValue blockValue)
        {
            var block = ResolveBlock(blockValue);
            return block == null ? UnknownText : block.GetType().Name;
        }

        private static Block ResolveBlock(BlockValue blockValue)
        {
            var block = ReadMember(blockValue, "Block") as Block;
            if (block != null)
            {
                return block;
            }

            var blockId = ReadBlockId(blockValue);
            return blockId.HasValue && blockId.Value >= 0 && blockId.Value < Block.list.Length ? Block.list[blockId.Value] : null;
        }

        private static int? ReadBlockId(BlockValue blockValue)
        {
            var raw = ReadMember(blockValue, "type") ?? ReadMember(blockValue, "Type") ?? ReadMember(blockValue, "BlockID");
            if (raw == null)
            {
                return null;
            }

            try { return Convert.ToInt32(raw, CultureInfo.InvariantCulture); } catch { return null; }
        }

        private static object ReadDurability(BlockValue blockValue, TileEntity tileEntity)
        {
            var raw = ReadMember(tileEntity, "currentDurability") ?? ReadMember(tileEntity, "Durability") ?? ReadMember(blockValue, "damage");
            if (raw == null)
            {
                return "unknown";
            }

            try { return Convert.ToSingle(raw, CultureInfo.InvariantCulture); } catch { return "unknown"; }
        }

        private static object ReadMaxDurability(BlockValue blockValue, TileEntity tileEntity)
        {
            var raw = ReadMember(tileEntity, "maxDurability") ?? ReadMember(tileEntity, "MaxDurability") ?? ReadMember(ResolveBlock(blockValue), "MaxDamage") ?? ReadMember(ResolveBlock(blockValue), "maxDamage");
            if (raw == null)
            {
                return "unknown";
            }

            try { return Convert.ToSingle(raw, CultureInfo.InvariantCulture); } catch { return "unknown"; }
        }

        private static object TryReadLockState(TileEntity tileEntity, BlockValue blockValue)
        {
            var raw = ReadMember(tileEntity, "IsLocked") ?? ReadMember(tileEntity, "bLocked") ?? InvokeMember(tileEntity, "IsLocked") ?? ReadMember(ResolveBlock(blockValue), "IsLocked");
            if (raw == null)
            {
                return "unknown";
            }

            try { return Convert.ToBoolean(raw, CultureInfo.InvariantCulture); } catch { return "unknown"; }
        }

        private static object TryReadPowerState(TileEntity tileEntity, BlockValue blockValue)
        {
            var raw = ReadMember(tileEntity, "IsPowered") ?? ReadMember(tileEntity, "powered") ?? InvokeMember(tileEntity, "IsPowered") ?? ReadMember(ResolveBlock(blockValue), "IsPowered");
            if (raw == null)
            {
                return "unknown";
            }

            return raw is bool boolValue ? (object)boolValue : true;
        }

        private static object TryReadActiveState(TileEntity tileEntity, BlockValue blockValue)
        {
            var raw = ReadMember(tileEntity, "IsActive") ?? ReadMember(tileEntity, "bActive") ?? InvokeMember(tileEntity, "IsActive") ?? ReadMember(ResolveBlock(blockValue), "IsActive");
            if (raw == null)
            {
                return "unknown";
            }

            try { return Convert.ToBoolean(raw, CultureInfo.InvariantCulture); } catch { return "unknown"; }
        }

        private static bool CanInteractWithBlock(string kind)
        {
            return kind == "loot" || kind == "container" || kind == "interactable" || kind == "vehicle" || kind == "npc" || kind == "trader";
        }

        private static bool? TryIsOpenSkyAbove(World world, Vector3 position)
        {
            try
            {
                var raw = InvokeMember(
                    world,
                    "IsOpenSkyAbove",
                    0,
                    Mathf.FloorToInt(position.x),
                    Mathf.FloorToInt(position.y),
                    Mathf.FloorToInt(position.z));
                if (raw == null)
                {
                    return null;
                }

                return Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeSort(string sortBy)
        {
            switch ((sortBy ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "confidence":
                case "type":
                    return sortBy.ToLowerInvariant();
                default:
                    return "distance";
            }
        }

        private static IEnumerable<ResourceCandidateObservation> SortResourceCandidates(IEnumerable<ResourceCandidateObservation> candidates, string sortBy)
        {
            switch (NormalizeSort(sortBy))
            {
                case "confidence":
                    return candidates.OrderByDescending(item => item.CandidateConfidence).ThenBy(item => item.Distance);
                case "type":
                    return candidates.OrderBy(item => item.LikelyResourceType ?? UnknownText, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Distance);
                default:
                    return candidates.OrderBy(item => item.Distance).ThenByDescending(item => item.CandidateConfidence);
            }
        }

        private static string NormalizeLookTargetKind(string kind, string entityName)
        {
            if (kind == "npc" && LooksLikeTraderEntity(string.Empty, entityName))
            {
                return "trader";
            }

            return string.IsNullOrWhiteSpace(kind) ? "unknown" : kind;
        }

        private static string NormalizeInteractableKind(string kind)
        {
            switch ((kind ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "door":
                    return "interactable";
                case "loot":
                case "container":
                case "entity":
                case "enemy":
                case "npc":
                case "interactable":
                case "block":
                case "vehicle":
                case "trader":
                    return kind.ToLowerInvariant();
                default:
                    return "unknown";
            }
        }

        private static string InferCandidateCategoryFromKind(string kind)
        {
            switch ((kind ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "enemy":
                    return "hostile_entity";
                case "npc":
                case "trader":
                    return "npc_entity";
                case "loot":
                case "container":
                    return "loot_container";
                case "interactable":
                    return "interactable_block";
                case "resource":
                    return "surface_resource_node";
                default:
                    return "unknown";
            }
        }

        private static string InferLikelyResourceTypeFromTargetKind(string kind)
        {
            switch ((kind ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "loot":
                case "container":
                    return "loot";
                case "enemy":
                    return "enemy";
                case "npc":
                case "trader":
                    return "npc";
                default:
                    return "unknown";
            }
        }

        private static string InferActionKind(string targetKind, string targetName, bool isLoot, bool isInteractable)
        {
            var name = (targetName ?? string.Empty).ToLowerInvariant();
            if (targetKind == "enemy") return "attack";
            if (targetKind == "resource") return LooksLikeTreeResource(name) ? "harvest" : "mine";
            if (targetKind == "npc" || targetKind == "trader") return "talk";
            if (targetKind == "vehicle") return name.Contains("4x4") || name.Contains("motor") ? "drive" : "ride";
            if (isLoot || targetKind == "loot" || targetKind == "container") return name.Contains("backpack") || name.Contains("bag") ? "loot" : "search";
            if (LooksLikeDoor(name)) return name.Contains("locked") ? "unlock" : "open";
            if (isInteractable || targetKind == "interactable") return "use";
            return targetKind == "none" ? "none" : "unknown";
        }

        private static string InferPromptText(string targetKind, string targetName, bool isLoot, bool isInteractable)
        {
            switch (InferActionKind(targetKind, targetName, isLoot, isInteractable))
            {
                case "search": return "Search";
                case "open": return "Open";
                case "talk": return "Talk";
                case "pickup": return "Pick up";
                case "use": return "Use";
                case "loot": return "Loot";
                case "harvest": return "Harvest";
                case "ride": return "Ride";
                case "drive": return "Drive";
                case "activate": return "Activate";
                case "read": return "Read";
                case "repair": return "Repair";
                case "unlock": return "Unlock";
                case "attack": return "Attack";
                case "mine": return "Mine";
                case "none": return "None";
                default: return UnknownText;
            }
        }

        private static float InferRecommendedInteractMin(string targetKind, string actionKind)
        {
            return (actionKind == "attack" || actionKind == "mine") ? 1.1f : (targetKind == "vehicle" ? 1.3f : 0.7f);
        }

        private static float InferRecommendedInteractMax(string targetKind, string actionKind)
        {
            if (actionKind == "attack" || actionKind == "mine") return 4.2f;
            if (targetKind == "npc" || targetKind == "trader") return 4.5f;
            return 4.0f;
        }

        private static string InferBlockTargetKind(string blockName, TileEntity tileEntity)
        {
            if (LooksLikeLootContainer(blockName, tileEntity)) return "loot";
            if (LooksLikeDoor(blockName) || LooksLikeInteractableBlock((blockName ?? string.Empty).ToLowerInvariant())) return "interactable";
            return BuildBlockCandidateMetadata(blockName, tileEntity).IsResourceCandidate ? "resource" : "block";
        }

        private static string InferEntityKind(string entityClass, string entityName, bool hostile)
        {
            var normalized = (entityClass + " " + entityName).ToLowerInvariant();
            if (LooksLikeTraderEntity(entityClass, entityName) || normalized.Contains("npc") || normalized.Contains("questgiver")) return "npc";
            if (normalized.Contains("vehicle") || normalized.Contains("bicycle") || normalized.Contains("minibike") || normalized.Contains("motorcycle") || normalized.Contains("4x4") || normalized.Contains("gyrocopter") || normalized.Contains("drone")) return "vehicle";
            if (normalized.Contains("animal") || normalized.Contains("deer") || normalized.Contains("boar") || normalized.Contains("chicken") || normalized.Contains("rabbit") || normalized.Contains("wolf") || normalized.Contains("bear") || normalized.Contains("vulture") || normalized.Contains("dog")) return hostile ? "enemy" : "animal";
            if (hostile) return "enemy";
            return LooksLikeLootEntity(entityClass, entityName) ? "neutral" : "neutral";
        }

        private static bool InferHostile(string entityClass, string entityName)
        {
            var normalized = (entityClass + " " + entityName).ToLowerInvariant();
            if (normalized.Contains("zombie") || normalized.Contains("enemy") || normalized.Contains("bandit")) return true;
            if (normalized.Contains("wolf") || normalized.Contains("bear") || normalized.Contains("vulture") || normalized.Contains("dog") || normalized.Contains("direwolf")) return true;
            return false;
        }

        private static bool InferAlive(Entity entity)
        {
            if (entity is EntityAlive aliveEntity) return !aliveEntity.IsDead();
            var isDead = TryReadBool(entity, "IsDead", "isDead");
            return !isDead.GetValueOrDefault(false);
        }

        private static string InferEntityCandidateCategory(string kind)
        {
            switch (kind)
            {
                case "enemy": return "hostile_entity";
                case "npc": return "npc_entity";
                case "animal":
                case "neutral":
                case "vehicle": return "neutral_entity";
                default: return "unknown";
            }
        }

        private static float InferEntityConfidence(string kind, string name)
        {
            if (kind == "enemy") return 0.98f;
            if (kind == "npc") return 0.95f;
            if (kind == "vehicle") return 0.92f;
            return LooksLikeLootEntity(kind, name) ? 0.9f : 0.6f;
        }

        private static string InferLikelyResourceTypeFromEntity(string kind, string name)
        {
            if (kind == "enemy") return "enemy";
            if (kind == "npc") return "npc";
            return LooksLikeLootEntity(kind, name) ? "loot" : "unknown";
        }

        private static BlockCandidateMetadata BuildBlockCandidateMetadata(string blockName, TileEntity tileEntity)
        {
            var normalized = (blockName ?? string.Empty).ToLowerInvariant();
            if (LooksLikeLootContainer(blockName, tileEntity)) return new BlockCandidateMetadata { IsResourceCandidate = true, CandidateCategory = "loot_container", CandidateConfidence = 0.98f, LikelyResourceType = "loot", Note = "Candidate classified as a loot-capable container." };
            if (LooksLikeTreeResource(normalized)) return new BlockCandidateMetadata { IsResourceCandidate = true, CandidateCategory = "surface_resource_node", CandidateConfidence = 0.94f, LikelyResourceType = "wood", Note = "Candidate classified as a wood/harvest resource." };
            if (normalized.Contains("iron")) return CreateOreMetadata("iron");
            if (normalized.Contains("coal")) return CreateOreMetadata("coal");
            if (normalized.Contains("nitrate")) return CreateOreMetadata("nitrate");
            if (normalized.Contains("lead")) return CreateOreMetadata("lead");
            if (normalized.Contains("oil") || normalized.Contains("shale")) return CreateOreMetadata("oil_shale");
            if (normalized.Contains("clay")) return new BlockCandidateMetadata { IsResourceCandidate = true, CandidateCategory = "terrain_resource_candidate", CandidateConfidence = 0.88f, LikelyResourceType = "clay", Note = "Candidate classified as clay-rich terrain." };
            if (normalized.Contains("stone") || normalized.Contains("rock") || normalized.Contains("gravel")) return new BlockCandidateMetadata { IsResourceCandidate = true, CandidateCategory = normalized.Contains("gravel") ? "terrain_resource_candidate" : "surface_resource_node", CandidateConfidence = 0.82f, LikelyResourceType = "stone", Note = "Candidate classified as a stone/mining resource." };
            if (normalized.Contains("resource") || normalized.Contains("ore")) return new BlockCandidateMetadata { IsResourceCandidate = true, CandidateCategory = "ore_related_block", CandidateConfidence = 0.75f, LikelyResourceType = "unknown", Note = "Candidate name suggests an ore/resource block." };
            return new BlockCandidateMetadata { IsResourceCandidate = false, CandidateCategory = LooksLikeInteractableBlock(normalized) ? "interactable_block" : "unknown", CandidateConfidence = LooksLikeInteractableBlock(normalized) ? 0.65f : 0.25f, LikelyResourceType = "unknown", Note = LooksLikeInteractableBlock(normalized) ? "Block is interactable but not a resource candidate." : "Block did not match a resource or loot heuristic." };
        }

        private static BlockCandidateMetadata CreateOreMetadata(string resourceType)
        {
            return new BlockCandidateMetadata { IsResourceCandidate = true, CandidateCategory = "ore_related_block", CandidateConfidence = 0.93f, LikelyResourceType = resourceType, Note = "Candidate classified as an ore-related block." };
        }

        private static bool LooksLikeTreeResource(string name) => name.Contains("tree") || name.Contains("wood") || name.Contains("log") || name.Contains("stump");
        private static bool LooksLikeDoor(string name) { var n = (name ?? string.Empty).ToLowerInvariant(); return n.Contains("door") || n.Contains("hatch") || n.Contains("gate") || n.Contains("shutter"); }
        private static bool LooksLikeInteractableBlock(string name) => LooksLikeDoor(name) || name.Contains("switch") || name.Contains("lever") || name.Contains("button") || name.Contains("workbench") || name.Contains("forge") || name.Contains("chemistry") || name.Contains("generator") || name.Contains("battery") || name.Contains("campfire") || name.Contains("vending") || name.Contains("vehicle");
        private static bool LooksLikeTraderEntity(string entityClass, string entityName) => (entityClass + " " + entityName).ToLowerInvariant().Contains("trader");
        private static bool LooksLikeLootEntity(string entityClass, string entityName) { var n = (entityClass + " " + entityName).ToLowerInvariant(); return n.Contains("backpack") || n.Contains("item") || n.Contains("loot"); }

        private static bool LooksLikeLootContainer(string blockName, TileEntity tileEntity)
        {
            var normalized = (blockName ?? string.Empty).ToLowerInvariant();
            if (tileEntity != null)
            {
                var typeName = tileEntity.GetType().Name.ToLowerInvariant();
                if (typeName.Contains("loot") || typeName.Contains("chest") || typeName.Contains("secure") || typeName.Contains("container")) return true;
            }

            return normalized.Contains("loot") || normalized.Contains("chest") || normalized.Contains("backpack") || normalized.Contains("crate") || normalized.Contains("cabinet") || normalized.Contains("trash") || normalized.Contains("dumpster") || normalized.Contains("cooler") || normalized.Contains("bag") || normalized.Contains("corpse") || normalized.Contains("sack") || normalized.StartsWith("cnt");
        }

        private static string InferBiomeHazard(string biomeName)
        {
            var normalized = (biomeName ?? string.Empty).ToLowerInvariant();
            if (normalized.Contains("snow")) return "cold";
            if (normalized.Contains("desert")) return "heat";
            if (normalized.Contains("wasteland")) return "high_hostility";
            if (normalized.Contains("burnt")) return "fire";
            return "none";
        }

        private static object ReadMember(object target, string name)
        {
            if (target == null || string.IsNullOrWhiteSpace(name)) return null;
            var type = target.GetType();
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property != null) return property.GetValue(target, null);
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            return field == null ? null : field.GetValue(target);
        }

        private static object InvokeMember(object target, string methodName, params object[] args)
        {
            if (target == null || string.IsNullOrWhiteSpace(methodName)) return null;
            foreach (var candidate in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal) || candidate.GetParameters().Length != args.Length) continue;
                try { return candidate.Invoke(target, args); } catch { return null; }
            }
            return null;
        }

        private static float? TryReadFloat(object target, params string[] names)
        {
            foreach (var name in names)
            {
                var raw = ReadMember(target, name) ?? InvokeMember(target, name);
                if (raw == null) continue;
                try { return Convert.ToSingle(raw, CultureInfo.InvariantCulture); } catch { }
            }
            return null;
        }

        private static bool? TryReadBool(object target, params string[] names)
        {
            foreach (var name in names)
            {
                var raw = ReadMember(target, name) ?? InvokeMember(target, name);
                if (raw == null) continue;
                try { return Convert.ToBoolean(raw, CultureInfo.InvariantCulture); } catch { }
            }
            return null;
        }

        private static Vector3? TryReadVector3(object target, params string[] names)
        {
            if (target == null || names == null)
            {
                return null;
            }

            foreach (var name in names)
            {
                var raw = ReadMember(target, name) ?? InvokeMember(target, name);
                if (raw is Vector3 vector)
                {
                    return vector;
                }
            }

            return null;
        }

        private static Vector3i? TryReadVector3i(object target, params string[] names)
        {
            if (target == null || names == null)
            {
                return null;
            }

            foreach (var name in names)
            {
                var raw = ReadMember(target, name) ?? InvokeMember(target, name);
                if (raw is Vector3i vector)
                {
                    return vector;
                }
            }

            return null;
        }

        private static Vector3 ReadCenter(IDictionary<string, object> arguments, Vector3 fallback)
        {
            var rawCenter = ReadRaw(arguments, "center");
            if (rawCenter is IDictionary<string, object> centerValues)
            {
                return new Vector3(ReadFloat(centerValues, "x", fallback.x), ReadFloat(centerValues, "y", fallback.y), ReadFloat(centerValues, "z", fallback.z));
            }

            if (rawCenter is IDictionary dictionary)
            {
                var normalized = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key == null)
                    {
                        continue;
                    }

                    normalized[entry.Key.ToString()] = entry.Value;
                }

                return new Vector3(ReadFloat(normalized, "x", fallback.x), ReadFloat(normalized, "y", fallback.y), ReadFloat(normalized, "z", fallback.z));
            }

            if (rawCenter != null)
            {
                var x = ReadMember(rawCenter, "x") ?? ReadMember(rawCenter, "X");
                var y = ReadMember(rawCenter, "y") ?? ReadMember(rawCenter, "Y");
                var z = ReadMember(rawCenter, "z") ?? ReadMember(rawCenter, "Z");
                if (x != null || y != null || z != null)
                {
                    try
                    {
                        return new Vector3(
                            x == null ? fallback.x : Convert.ToSingle(x, CultureInfo.InvariantCulture),
                            y == null ? fallback.y : Convert.ToSingle(y, CultureInfo.InvariantCulture),
                            z == null ? fallback.z : Convert.ToSingle(z, CultureInfo.InvariantCulture));
                    }
                    catch
                    {
                    }
                }
            }

            return new Vector3(ReadFloat(arguments, "x", fallback.x), ReadFloat(arguments, "y", fallback.y), ReadFloat(arguments, "z", fallback.z));
        }

        private static string ReadString(IDictionary<string, object> arguments, string key, string fallback)
        {
            var raw = ReadRaw(arguments, key);
            return raw == null ? fallback : Convert.ToString(raw, CultureInfo.InvariantCulture);
        }

        private static float ReadFloat(IDictionary<string, object> arguments, string key, float fallback)
        {
            var raw = ReadRaw(arguments, key);
            if (raw == null) return fallback;
            try { return Convert.ToSingle(raw, CultureInfo.InvariantCulture); } catch { return fallback; }
        }

        private static int ReadInt(IDictionary<string, object> arguments, string key, int fallback)
        {
            var raw = ReadRaw(arguments, key);
            if (raw == null) return fallback;
            try { return Convert.ToInt32(raw, CultureInfo.InvariantCulture); } catch { return fallback; }
        }

        private static bool ReadBool(IDictionary<string, object> arguments, string key, bool fallback)
        {
            var raw = ReadRaw(arguments, key);
            if (raw == null) return fallback;
            try { return Convert.ToBoolean(raw, CultureInfo.InvariantCulture); } catch { return fallback; }
        }

        private static HashSet<string> ReadStringSet(IDictionary<string, object> arguments, string key)
        {
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var raw = ReadRaw(arguments, key);
            if (raw == null) return values;
            if (raw is string text)
            {
                foreach (var item in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) values.Add(item.Trim());
                return values;
            }
            if (raw is IEnumerable enumerable)
            {
                foreach (var item in enumerable) if (item != null) values.Add(item.ToString());
            }
            return values;
        }

        private static object ReadRaw(IDictionary<string, object> arguments, string key)
        {
            if (arguments == null) return null;
            foreach (var pair in arguments)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Value;
            }
            return null;
        }

        private sealed class BlockCandidateMetadata
        {
            public bool IsResourceCandidate { get; set; }
            public string CandidateCategory { get; set; }
            public float CandidateConfidence { get; set; }
            public string LikelyResourceType { get; set; }
            public string Note { get; set; }
        }
    }
}
