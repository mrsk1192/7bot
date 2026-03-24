using System;

namespace mnetSevenDaysBridge
{
    public sealed class DeathRespawnStateTracker
    {
        private readonly object syncRoot = new object();
        private readonly BridgeLogger logger;
        private DeathRespawnStateSnapshot snapshot = DeathRespawnStateSnapshot.CreateDefault();
        private DateTime? justRespawnedUntilUtc;
        private SpawnMethod? lastRequestedSpawnMethod;
        private bool isFirstRefresh = true;

        public DeathRespawnStateTracker(BridgeLogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public DeathRespawnStateUpdateResult Refresh(EntityPlayerLocal player, RespawnUiState uiState)
        {
            if (uiState == null)
            {
                uiState = RespawnUiState.CreateUnavailable("Respawn UI state was unavailable.");
            }

            lock (syncRoot)
            {
                var previous = snapshot.Clone();
                var nowUtc = DateTime.UtcNow;
                var position = TryReadCurrentPosition(player);
                var alive = player != null ? !player.IsDead() : (bool?)null;
                var isDead = player != null
                    ? player.IsDead()
                    : (uiState.DeathScreenOpen || uiState.RespawnScreenOpen);
                var died = !isFirstRefresh && !previous.IsDead && isDead;
                if (isFirstRefresh) isFirstRefresh = false;
                var respawned = previous.IsDead && !isDead && alive.GetValueOrDefault(false);

                if (died)
                {
                    snapshot.LastDeathTime = nowUtc.ToString("o");
                    snapshot.LastDeathPosition = position ?? snapshot.LastDeathPosition;
                    justRespawnedUntilUtc = null;
                    logger.Info("Death/respawn tracker detected a death transition.");
                }

                if (respawned)
                {
                    justRespawnedUntilUtc = nowUtc.AddSeconds(3);
                    snapshot.RespawnInProgress = false;
                    logger.Info("Death/respawn tracker detected a respawn transition.");
                }

                if (isDead)
                {
                    justRespawnedUntilUtc = null;
                }

                snapshot.Alive = alive ?? !isDead;
                snapshot.IsDead = isDead;
                snapshot.DeathScreenVisible = uiState.DeathScreenOpen || uiState.RespawnScreenOpen;
                snapshot.DeathScreenOpen = uiState.DeathScreenOpen;
                snapshot.RespawnScreenOpen = uiState.RespawnScreenOpen;
                snapshot.RespawnConfirmationOpen = uiState.RespawnConfirmationOpen;
                snapshot.RespawnCooldownSeconds = uiState.RespawnCooldownSeconds;
                snapshot.RespawnAvailable = uiState.RespawnAvailable;
                snapshot.BedrollSpawnAvailable = uiState.BedrollSpawnAvailable;
                snapshot.NearestSpawnOptionSummary = string.IsNullOrWhiteSpace(uiState.NearestSpawnOptionSummary)
                    ? snapshot.NearestSpawnOptionSummary
                    : uiState.NearestSpawnOptionSummary;
                snapshot.JustRespawned = justRespawnedUntilUtc.HasValue && nowUtc <= justRespawnedUntilUtc.Value;

                if (snapshot.Alive == true && !snapshot.IsDead)
                {
                    snapshot.DeathScreenVisible = false;
                    snapshot.DeathScreenOpen = false;
                    snapshot.RespawnScreenOpen = false;
                    snapshot.RespawnConfirmationOpen = false;
                    snapshot.RespawnAvailable = false;
                    snapshot.RespawnCooldownSeconds = null;
                    snapshot.RespawnInProgress = false;
                }
                else if (!snapshot.IsDead && !snapshot.RespawnScreenOpen)
                {
                    snapshot.RespawnInProgress = false;
                }

                // Canonicalize availability from the tracked cooldown so UI probing
                // quirks cannot leave cooldown=0 while availability stays false.
                if (snapshot.IsDead)
                {
                    snapshot.DeathScreenVisible = true;
                    snapshot.DeathScreenOpen = true;
                    snapshot.RespawnScreenOpen = true;
                    snapshot.RespawnAvailable = !snapshot.RespawnCooldownSeconds.HasValue
                        || snapshot.RespawnCooldownSeconds.Value <= 0.05f;
                    snapshot.RespawnConfirmationOpen = snapshot.RespawnAvailable || snapshot.RespawnConfirmationOpen;
                }
                else if (!snapshot.RespawnInProgress)
                {
                    snapshot.RespawnAvailable = false;
                }

                if (respawned)
                {
                    snapshot.JustRespawned = true;
                }

                return new DeathRespawnStateUpdateResult
                {
                    Died = died,
                    Respawned = respawned,
                    Snapshot = snapshot.Clone()
                };
            }
        }

        public DeathRespawnStateSnapshot GetSnapshot()
        {
            lock (syncRoot)
            {
                return snapshot.Clone();
            }
        }

        public void MarkRespawnRequested(SpawnMethod? method, string summary)
        {
            lock (syncRoot)
            {
                snapshot.RespawnInProgress = true;
                snapshot.JustRespawned = false;
                snapshot.RespawnAvailable = false;
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    snapshot.NearestSpawnOptionSummary = summary;
                }

                lastRequestedSpawnMethod = method;
            }
        }

        public void MarkRespawnCanceled()
        {
            lock (syncRoot)
            {
                snapshot.RespawnInProgress = false;
            }
        }

        public float? ComputeFallbackCooldown(EntityPlayerLocal player)
        {
            if (player == null)
            {
                return null;
            }

            var waitSeconds = player.GetTimeStayAfterDeath();
            if (waitSeconds <= 0)
            {
                return 0f;
            }

            lock (syncRoot)
            {
                if (string.IsNullOrWhiteSpace(snapshot.LastDeathTime)
                    || !DateTime.TryParse(
                        snapshot.LastDeathTime,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out var deathUtc))
                {
                    return 0f;
                }

                var elapsed = (float)Math.Max(0d, (DateTime.UtcNow - deathUtc).TotalSeconds);
                return Math.Max(0f, waitSeconds - elapsed);
            }
        }

        public void ClearForRespawnReset()
        {
            lock (syncRoot)
            {
                snapshot.RespawnInProgress = false;
            }
        }

        public SpawnMethod? GetLastRequestedSpawnMethod()
        {
            lock (syncRoot)
            {
                return lastRequestedSpawnMethod;
            }
        }

        private static Vector3State TryReadCurrentPosition(EntityPlayerLocal player)
        {
            if (player == null)
            {
                return null;
            }

            var position = player.position;
            return new Vector3State
            {
                X = position.x,
                Y = position.y,
                Z = position.z
            };
        }
    }

    public sealed class DeathRespawnStateUpdateResult
    {
        public bool Died { get; set; }

        public bool Respawned { get; set; }

        public DeathRespawnStateSnapshot Snapshot { get; set; }
    }

    public sealed class DeathRespawnStateSnapshot
    {
        public bool? Alive { get; set; }

        public bool IsDead { get; set; }

        public bool DeathScreenVisible { get; set; }

        public bool DeathScreenOpen { get; set; }

        public bool RespawnScreenOpen { get; set; }

        public bool RespawnConfirmationOpen { get; set; }

        public bool RespawnAvailable { get; set; }

        public float? RespawnCooldownSeconds { get; set; }

        public string LastDeathTime { get; set; }

        public Vector3State LastDeathPosition { get; set; }

        public bool BedrollSpawnAvailable { get; set; }

        public string NearestSpawnOptionSummary { get; set; }

        public bool RespawnInProgress { get; set; }

        public bool JustRespawned { get; set; }

        public DeathRespawnStateSnapshot Clone()
        {
            return new DeathRespawnStateSnapshot
            {
                Alive = Alive,
                IsDead = IsDead,
                DeathScreenVisible = DeathScreenVisible,
                DeathScreenOpen = DeathScreenOpen,
                RespawnScreenOpen = RespawnScreenOpen,
                RespawnConfirmationOpen = RespawnConfirmationOpen,
                RespawnAvailable = RespawnAvailable,
                RespawnCooldownSeconds = RespawnCooldownSeconds,
                LastDeathTime = LastDeathTime,
                LastDeathPosition = LastDeathPosition == null
                    ? null
                    : new Vector3State
                    {
                        X = LastDeathPosition.X,
                        Y = LastDeathPosition.Y,
                        Z = LastDeathPosition.Z
                    },
                BedrollSpawnAvailable = BedrollSpawnAvailable,
                NearestSpawnOptionSummary = NearestSpawnOptionSummary,
                RespawnInProgress = RespawnInProgress,
                JustRespawned = JustRespawned
            };
        }

        public static DeathRespawnStateSnapshot CreateDefault()
        {
            return new DeathRespawnStateSnapshot
            {
                Alive = null,
                IsDead = false,
                DeathScreenVisible = false,
                DeathScreenOpen = false,
                RespawnScreenOpen = false,
                RespawnConfirmationOpen = false,
                RespawnAvailable = false,
                RespawnCooldownSeconds = null,
                LastDeathTime = null,
                LastDeathPosition = null,
                BedrollSpawnAvailable = false,
                NearestSpawnOptionSummary = null,
                RespawnInProgress = false,
                JustRespawned = false
            };
        }
    }

    public sealed class RespawnUiState
    {
        public bool Available { get; set; }

        public bool DeathScreenOpen { get; set; }

        public bool RespawnScreenOpen { get; set; }

        public bool RespawnConfirmationOpen { get; set; }

        public bool RespawnAvailable { get; set; }

        public float? RespawnCooldownSeconds { get; set; }

        public bool BedrollSpawnAvailable { get; set; }

        public string NearestSpawnOptionSummary { get; set; }

        public SpawnMethod? SelectedSpawnMethod { get; set; }

        public string Note { get; set; }

        public static RespawnUiState CreateUnavailable(string note)
        {
            return new RespawnUiState
            {
                Available = false,
                DeathScreenOpen = false,
                RespawnScreenOpen = false,
                RespawnConfirmationOpen = false,
                RespawnAvailable = false,
                RespawnCooldownSeconds = null,
                BedrollSpawnAvailable = false,
                NearestSpawnOptionSummary = null,
                SelectedSpawnMethod = null,
                Note = note
            };
        }
    }
}
