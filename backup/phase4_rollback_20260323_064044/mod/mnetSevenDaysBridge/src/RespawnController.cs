using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace mnetSevenDaysBridge
{
    public sealed class RespawnController
    {
        private readonly BridgeLogger logger;
        private readonly DeathRespawnStateTracker tracker;
        private readonly object spawnSelectionSyncRoot = new object();
        private readonly int mainThreadId;
        private XUiC_SpawnSelectionWindow trackedSpawnSelectionWindow;
        private PendingSpawnInvocation pendingSpawnInvocation;
        private bool dumpedUi = false;

        public RespawnController(BridgeLogger logger, DeathRespawnStateTracker tracker)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public DeathRespawnStateUpdateResult RefreshState()
        {
            var player = ResolvePlayer();
            var uiState = CollectUiState(player);
            return tracker.Refresh(player, uiState);
        }

        public DeathRespawnStateSnapshot GetSnapshot()
        {
            return tracker.GetSnapshot();
        }

        public void OnSpawnSelectionWindowOpened(XUiC_SpawnSelectionWindow controller)
        {
            if (!IsSpawnSelectionWindowUsable(controller))
            {
                return;
            }

            PendingSpawnInvocation pending = null;
            lock (spawnSelectionSyncRoot)
            {
                trackedSpawnSelectionWindow = controller;
                if (pendingSpawnInvocation != null)
                {
                    pending = pendingSpawnInvocation;
                    pendingSpawnInvocation = null;
                }
            }

            try
            {
                controller.RefreshButtons();
            }
            catch
            {
            }

            if (pending == null)
            {
                return;
            }

            try
            {
                var player = ResolvePlayer();
                var ui = ResolveUi(player);
                var refreshedState = CollectUiState(player, ui);
                EnsureRespawnReady(controller, refreshedState);
                controller.SpawnButtonPressed(pending.Method, 0);
                tracker.MarkRespawnRequested(
                    pending.Method,
                    BuildSpawnSummary(
                        pending.Method,
                        ReadMember(controller, "spawnTarget"),
                        refreshedState.BedrollSpawnAvailable,
                        player));
                logger.Info("Respawn command executed through spawn-selection open hook with method=" + pending.Method);
                pending.Note = pending.Note + " Event hook captured the spawn selection window.";
                pending.Complete(null);
            }
            catch (Exception exception)
            {
                logger.Warn("Spawn-selection open hook failed to execute the pending respawn command: " + exception.Message);
                pending.Complete(exception);
            }
        }

        public void OnSpawnSelectionWindowClosed(XUiC_SpawnSelectionWindow controller)
        {
            lock (spawnSelectionSyncRoot)
            {
                if (trackedSpawnSelectionWindow == null)
                {
                    return;
                }

                if (ReferenceEquals(trackedSpawnSelectionWindow, controller) || !IsSpawnSelectionWindowUsable(trackedSpawnSelectionWindow))
                {
                    trackedSpawnSelectionWindow = null;
                }
            }
        }

        public RespawnActionExecutionResult ExecuteAction(string action, Dictionary<string, object> arguments)
        {
            var player = ResolvePlayer();
            var ui = ResolveUi(player);
            var state = CollectUiState(player, ui);
            var controller = GetSpawnSelectionController(ui);

            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "respawn_select_default":
                    return TriggerSpawn(ui, controller, ChooseDefaultMethod(state), state, "Triggered the default respawn option.");
                case "respawn_at_bedroll":
                    if (!state.BedrollSpawnAvailable)
                    {
                        throw new BridgeCommandException(409, "RESPAWN_NOT_AVAILABLE", "Bedroll respawn was not available.");
                    }

                    return TriggerSpawn(ui, controller, SpawnMethod.OnBedRoll, state, "Triggered respawn at the bedroll.");
                case "respawn_near_bedroll":
                    if (!state.BedrollSpawnAvailable)
                    {
                        throw new BridgeCommandException(409, "RESPAWN_NOT_AVAILABLE", "Near-bedroll respawn was not available.");
                    }

                    return TriggerSpawn(ui, controller, SpawnMethod.NearBedroll, state, "Triggered respawn near the bedroll.");
                case "respawn_at_random":
                    return TriggerSpawn(ui, controller, SpawnMethod.NewRandomSpawn, state, "Triggered a random respawn.");
                case "respawn_confirm":
                    return TriggerSpawn(ui, controller, ChooseConfirmedMethod(state), state, "Confirmed the current respawn selection.");
                case "respawn_cancel":
                    if (controller == null || !state.RespawnScreenOpen)
                    {
                        throw new BridgeCommandException(409, "INVALID_RESPAWN_STATE", "Respawn cancel was requested without an open respawn screen.");
                    }

                    XUiC_SpawnSelectionWindow.Close(ui);
                    tracker.MarkRespawnCanceled();
                    logger.Info("Respawn selection was canceled.");
                    return new RespawnActionExecutionResult
                    {
                        StateChanged = true,
                        Note = "Closed the respawn selection window."
                    };
                default:
                    throw new BridgeCommandException(400, "unsupported_action", "Unsupported respawn action: " + action);
            }
        }

        public RespawnActionExecutionResult WaitForRespawnScreen(int timeoutMs)
        {
            return WaitUntil(
                timeoutMs,
                snapshot =>
                {
                    if (!snapshot.IsDead && !snapshot.DeathScreenVisible
                        && !snapshot.RespawnScreenOpen && !snapshot.RespawnAvailable)
                    {
                        return false;
                    }

                    var player = ResolvePlayer();
                    var ui = ResolveUi(player);
                    var controller = GetSpawnSelectionController(ui);
                    if (controller != null)
                    {
                        return true;
                    }

                    if (!dumpedUi && ui != null && ui.xui != null)
                    {
                        dumpedUi = true;
                        DumpXUiToLogger(ui.xui);
                    }

                    return false;
                },
                "Respawn screen and spawn selection controller became available.",
                "Timed out waiting for the respawn screen.");
        }

        private void DumpXUiToLogger(XUi xui)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== XUi Tree Dump ===");
            var windowGroups = ReadMember(xui, "WindowGroups") as IEnumerable;
            if (windowGroups == null) { sb.AppendLine("WindowGroups is null!"); }
            else
            {
                foreach (var windowGroup in windowGroups)
                {
                    string id = ReadMember(windowGroup, "ID") as string ?? "unknown_id";
                    bool isShowing = TryReadBool(windowGroup, "isShowing") ?? false;
                    var controller = ReadMember(windowGroup, "Controller") as XUiController;
                    string ctlName = controller != null ? controller.GetType().Name : "null";
                    sb.AppendLine($"Group: {id} (isShowing={isShowing}) -> [Controller: {ctlName}]");
                    if (isShowing && controller != null)
                    {
                        DumpChildren(controller, sb, "  ");
                    }
                }
            }
            logger.Info(sb.ToString());
        }

        private void DumpChildren(XUiController parent, System.Text.StringBuilder sb, string indent)
        {
            IEnumerable children = null;
            foreach (var name in new[] { "Children", "childControllers", "m_ChildControllers", "children" })
            {
                children = ReadMember(parent, name) as IEnumerable;
                if (children != null) break;
            }
            if (children == null) return;

            foreach (var childObj in children)
            {
                var child = childObj as XUiController;
                if (child == null) continue;
                string id = ReadMember(child.ViewComponent, "ID") as string ?? "unknown_view_id";
                sb.AppendLine($"{indent}- Child: {id} [Controller: {child.GetType().Name}]");
                DumpChildren(child, sb, indent + "  ");
            }
        }

        public RespawnActionExecutionResult WaitForRespawnComplete(int timeoutMs)
        {
            return WaitUntil(
                timeoutMs,
                snapshot => snapshot.Alive.GetValueOrDefault(false) && !snapshot.IsDead && !snapshot.RespawnInProgress,
                "Respawn completed and the player is alive again.",
                "Timed out waiting for respawn completion.");
        }

        public RespawnUiState CollectUiState(EntityPlayerLocal player = null)
        {
            return CollectUiState(player, ResolveUi(player));
        }

        private RespawnUiState CollectUiState(EntityPlayerLocal player, LocalPlayerUI ui)
        {
            var playerIsDead = player != null && player.IsDead();
            if (ui == null)
            {
                if (!playerIsDead)
                {
                    return RespawnUiState.CreateUnavailable("LocalPlayerUI was unavailable.");
                }

                var fallbackCooldown = NormalizeCooldown(ComputeFallbackCooldown(player));
                var fallbackBedroll = HasBedrollSpawn(player);
                var fallbackAvailable = !fallbackCooldown.HasValue || fallbackCooldown.Value <= 0.05f;
                return new RespawnUiState
                {
                    Available = true,
                    DeathScreenOpen = true,
                    RespawnScreenOpen = true,
                    RespawnConfirmationOpen = fallbackAvailable,
                    RespawnAvailable = fallbackAvailable,
                    RespawnCooldownSeconds = fallbackCooldown,
                    BedrollSpawnAvailable = fallbackBedroll,
                    SelectedSpawnMethod = fallbackBedroll ? SpawnMethod.OnBedRoll : SpawnMethod.NewRandomSpawn,
                    NearestSpawnOptionSummary = BuildSpawnSummary(
                        fallbackBedroll ? SpawnMethod.OnBedRoll : SpawnMethod.NewRandomSpawn,
                        player == null ? null : player.GetSpawnPoint(),
                        fallbackBedroll,
                        player),
                    Note = "LocalPlayerUI was unavailable; player-only respawn fallback is active."
                };
            }

            try
            {
                var windowManager = ui.windowManager;
                var deathScreenOpen = windowManager != null
                    && windowManager.HasWindow("windowDeathBar")
                    && windowManager.IsWindowOpen("windowDeathBar");
                var controller = GetSpawnSelectionController(ui);
                var inSpawnSelection = TryIsInSpawnSelection(ui);
                var isOpenInUi = TryIsOpenInUi(ui);
                var windowMode = controller == null ? (XUiC_SpawnSelectionWindow.ESpawnWindowMode?)null : controller.WindowMode;
                var respawnScreenOpen = controller != null
                    && (controller.IsOpen
                        || isOpenInUi
                        || inSpawnSelection
                        || windowMode == XUiC_SpawnSelectionWindow.ESpawnWindowMode.Progress
                        || windowMode == XUiC_SpawnSelectionWindow.ESpawnWindowMode.SpawnSelection
                        || playerIsDead);
                var cooldown = controller == null ? null : TryReadFloat(controller, "delayCountdownTime");
                cooldown = NormalizeCooldown(cooldown);
                if (!cooldown.HasValue && playerIsDead)
                {
                    cooldown = ComputeFallbackCooldown(player);
                }

                var bedrollAvailable = (controller != null && (TryReadBool(controller, "bHasBedroll") ?? false))
                    || HasBedrollSpawn(player);
                var selectedMethod = controller == null ? (SpawnMethod?)null : TryReadEnum<SpawnMethod>(controller, "spawnMethod");
                var spawnTarget = controller == null ? null : ReadMember(controller, "spawnTarget");
                var respawnAvailable = playerIsDead
                    && (!cooldown.HasValue || cooldown.Value <= 0.05f);

                return new RespawnUiState
                {
                    Available = true,
                    DeathScreenOpen = deathScreenOpen || playerIsDead,
                    RespawnScreenOpen = respawnScreenOpen || playerIsDead,
                    RespawnConfirmationOpen = respawnAvailable,
                    RespawnAvailable = respawnAvailable,
                    RespawnCooldownSeconds = cooldown,
                    BedrollSpawnAvailable = bedrollAvailable,
                    SelectedSpawnMethod = selectedMethod,
                    NearestSpawnOptionSummary = BuildSpawnSummary(selectedMethod, spawnTarget, bedrollAvailable, player),
                    Note = controller == null
                        ? "Respawn UI controller was not found; fallback timing/state detection is active."
                        : "Respawn UI state collected from XUiC_SpawnSelectionWindow with hidden-flow fallback."
                };
            }
            catch (Exception exception)
            {
                logger.Warn("Respawn UI probing failed, falling back to player-only state: " + exception.Message);
                var fallbackCooldown = NormalizeCooldown(ComputeFallbackCooldown(player));
                var fallbackBedroll = HasBedrollSpawn(player);
                var fallbackAvailable = playerIsDead && (!fallbackCooldown.HasValue || fallbackCooldown.Value <= 0.05f);
                return new RespawnUiState
                {
                    Available = true,
                    DeathScreenOpen = playerIsDead,
                    RespawnScreenOpen = playerIsDead,
                    RespawnConfirmationOpen = fallbackAvailable,
                    RespawnAvailable = fallbackAvailable,
                    RespawnCooldownSeconds = fallbackCooldown,
                    BedrollSpawnAvailable = fallbackBedroll,
                    SelectedSpawnMethod = fallbackBedroll ? SpawnMethod.OnBedRoll : SpawnMethod.NewRandomSpawn,
                    NearestSpawnOptionSummary = BuildSpawnSummary(
                        fallbackBedroll ? SpawnMethod.OnBedRoll : SpawnMethod.NewRandomSpawn,
                        player == null ? null : player.GetSpawnPoint(),
                        fallbackBedroll,
                        player),
                    Note = "Respawn UI probing failed; player-only respawn fallback is active."
                };
            }
        }

        private RespawnActionExecutionResult TriggerSpawn(
            LocalPlayerUI ui,
            XUiC_SpawnSelectionWindow controller,
            SpawnMethod method,
            RespawnUiState state,
            string note)
        {
            if (controller == null)
            {
                return TriggerSpawnWithoutUi(method, state, note);
            }

            EnsureRespawnReady(controller, state);
            controller.SpawnButtonPressed(method, 0);
            tracker.MarkRespawnRequested(method, BuildSpawnSummary(method, ReadMember(controller, "spawnTarget"), state.BedrollSpawnAvailable, ResolvePlayer()));
            logger.Info("Respawn command executed with method=" + method);

            return new RespawnActionExecutionResult
            {
                StateChanged = true,
                Note = note
            };
        }

        private RespawnActionExecutionResult TriggerSpawnWithoutUi(
            SpawnMethod method,
            RespawnUiState state,
            string note)
        {
            var ui = ResolveUi(ResolvePlayer());
            var openedController = TryOpenSpawnSelection(ui);
            for (int i = 0; i < 3 && openedController == null; i++)
            {
                Thread.Sleep(200);
                openedController = TryOpenSpawnSelection(ui);
            }
            if (openedController != null)
            {
                var refreshedState = CollectUiState(ResolvePlayer(), ui);
                if (!refreshedState.RespawnAvailable)
                {
                    throw new BridgeCommandException(409, "RESPAWN_NOT_AVAILABLE", "Respawn was not yet available.");
                }

                openedController.SpawnButtonPressed(method, 0);
                tracker.MarkRespawnRequested(
                    method,
                    BuildSpawnSummary(
                        method,
                        ReadMember(openedController, "spawnTarget"),
                        refreshedState.BedrollSpawnAvailable,
                        ResolvePlayer()));
                logger.Info("Respawn command executed through opened spawn-selection UI with method=" + method);

                return new RespawnActionExecutionResult
                {
                    StateChanged = true,
                    Note = note + " Fallback path opened the spawn selection UI."
                };
            }

            var pending = QueuePendingSpawnInvocation(method, state, note);
            try
            {
                if (pending.Completion.Wait(2000))
                {
                    if (pending.Error != null)
                    {
                        if (pending.Error is BridgeCommandException bridgeCommandException)
                        {
                            throw bridgeCommandException;
                        }

                        throw new BridgeCommandException(409, "RESPAWN_NOT_AVAILABLE", pending.Error.Message);
                    }

                    return new RespawnActionExecutionResult
                    {
                        StateChanged = true,
                        Note = pending.Note
                    };
                }
                else
                {
                    throw new BridgeCommandException(409, "RESPAWN_NOT_AVAILABLE", "Timed out waiting for the spawn selection window to open. A cooldown might be active.");
                }
            }
            finally
            {
                ClearPendingSpawnInvocation(pending);
            }

        }

        private XUiC_SpawnSelectionWindow TryOpenSpawnSelection(LocalPlayerUI ui)
        {
            if (ui == null)
            {
                return null;
            }

            // First, check if the controller is already embedded in the
            // death bar UI (the most common case in 7DTD).
            var existing = GetSpawnSelectionController(ui);
            if (existing != null)
            {
                try { existing.RefreshButtons(); } catch { }
                return existing;
            }

            // Only attempt Open() if the controller was not found embedded.
            try
            {
                XUiC_SpawnSelectionWindow.Open(ui, true, false, false);
                var controller = GetSpawnSelectionController(ui);
                if (controller != null)
                {
                    try { controller.RefreshButtons(); } catch { }
                }
                return controller;
            }
            catch (Exception exception)
            {
                logger.Warn("Failed to open the spawn selection UI for respawn fallback: " + exception.Message);
                return null;
            }
        }

        private static SpawnMethod ChooseDefaultMethod(RespawnUiState state)
        {
            if (state.BedrollSpawnAvailable)
            {
                return SpawnMethod.OnBedRoll;
            }

            return SpawnMethod.NewRandomSpawn;
        }

        private static SpawnMethod ChooseConfirmedMethod(RespawnUiState state)
        {
            if (state.SelectedSpawnMethod.HasValue && state.SelectedSpawnMethod.Value != SpawnMethod.Invalid)
            {
                return state.SelectedSpawnMethod.Value;
            }

            return ChooseDefaultMethod(state);
        }

        private static void EnsureRespawnReady(XUiC_SpawnSelectionWindow controller, RespawnUiState state)
        {
            if (controller == null)
            {
                throw new BridgeCommandException(409, "INVALID_RESPAWN_STATE", "Respawn controller was unavailable.");
            }

            if (!state.RespawnAvailable)
            {
                throw new BridgeCommandException(409, "RESPAWN_NOT_AVAILABLE", "Respawn was not yet available.");
            }
        }

        private RespawnActionExecutionResult WaitUntil(int timeoutMs, Func<DeathRespawnStateSnapshot, bool> predicate, string successNote, string timeoutMessage)
        {
            var effectiveTimeoutMs = timeoutMs <= 0 ? 15000 : timeoutMs;
            var deadline = DateTime.UtcNow.AddMilliseconds(effectiveTimeoutMs);
            while (DateTime.UtcNow <= deadline)
            {
                try { RefreshState(); } catch { }
                var snapshot = tracker.GetSnapshot();
                if (predicate(snapshot))
                {
                    return new RespawnActionExecutionResult
                    {
                        StateChanged = false,
                        Note = successNote
                    };
                }

                Thread.Sleep(200);
            }

            throw new BridgeCommandException(408, "RESPAWN_WAIT_TIMEOUT", timeoutMessage);
        }

        private EntityPlayerLocal ResolvePlayer()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.World == null)
            {
                return null;
            }

            return gameManager.World.GetPrimaryPlayer() as EntityPlayerLocal;
        }

        private static LocalPlayerUI ResolveUi(EntityPlayerLocal player)
        {
            if (player != null && player.PlayerUI != null)
            {
                return player.PlayerUI;
            }

            return LocalPlayerUI.GetUIForPrimaryPlayer();
        }

        private XUiC_SpawnSelectionWindow GetSpawnSelectionController(LocalPlayerUI ui)
        {
            var tracked = GetTrackedSpawnSelectionWindow();
            if (tracked != null)
            {
                return tracked;
            }

            if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
            {
                return null;
            }

            if (ui == null)
            {
                return null;
            }

            XUiC_SpawnSelectionWindow direct = null;
            try
            {
                direct = XUiC_SpawnSelectionWindow.GetWindow(ui);
            }
            catch (Exception)
            {
                direct = null;
            }

            if (direct != null)
            {
                return direct;
            }

            var xui = ui == null ? null : ui.xui;
            if (xui == null)
            {
                return null;
            }

            var windowGroups = ReadMember(xui, "WindowGroups") as IEnumerable;
            if (windowGroups == null)
            {
                return null;
            }

            foreach (var windowGroup in windowGroups)
            {
                var controller = ReadMember(windowGroup, "Controller") as XUiController;
                if (controller is XUiC_SpawnSelectionWindow spawnSelectionWindow)
                {
                    return spawnSelectionWindow;
                }
                // Recursively search child controllers (death bar embeds spawn
                // selection as a child, not as a top-level window group)
                var found = FindSpawnSelectionInChildren(controller);
                if (found != null)
                {
                    return found;
                }
            }

            // Ultimate fallback: scan all MonoBehaviour instances via Unity's
            // FindObjectsOfTypeAll to locate XUiC_SpawnSelectionWindow even when
            // the XUi window tree is not yet fully built (load-on-death race).
            try
            {
                var targetType = typeof(XUiC_SpawnSelectionWindow);
                var monoBehaviourType = typeof(UnityEngine.MonoBehaviour);
                var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll(monoBehaviourType);
                foreach (var obj in allObjects)
                {
                    if (obj == null || !targetType.IsAssignableFrom(obj.GetType()))
                    {
                        continue;
                    }
                    var mono = (UnityEngine.MonoBehaviour)obj;
                    if (mono.gameObject != null && mono.gameObject.activeInHierarchy)
                    {
                        return (XUiC_SpawnSelectionWindow)(object)mono;
                    }
                }
            }
            catch { }

            return null;
        }

        private XUiC_SpawnSelectionWindow GetTrackedSpawnSelectionWindow()
        {
            lock (spawnSelectionSyncRoot)
            {
                if (!IsSpawnSelectionWindowUsable(trackedSpawnSelectionWindow))
                {
                    trackedSpawnSelectionWindow = null;
                }

                return trackedSpawnSelectionWindow;
            }
        }

        private static XUiC_SpawnSelectionWindow FindSpawnSelectionInChildren(XUiController parent)
        {
            if (parent == null)
            {
                return null;
            }

            // Try multiple property names used in different 7DTD versions.
            IEnumerable children = null;
            foreach (var name in new[] { "Children", "childControllers", "m_ChildControllers", "children" })
            {
                children = ReadMember(parent, name) as IEnumerable;
                if (children != null)
                {
                    break;
                }
            }

            if (children == null)
            {
                return null;
            }

            foreach (var child in children)
            {
                var childController = child as XUiController;
                if (childController == null)
                {
                    continue;
                }

                if (childController is XUiC_SpawnSelectionWindow found)
                {
                    return found;
                }

                var deeper = FindSpawnSelectionInChildren(childController);
                if (deeper != null)
                {
                    return deeper;
                }
            }

            return null;
        }

        private static bool TryIsInSpawnSelection(LocalPlayerUI ui)
        {
            if (ui == null)
            {
                return false;
            }

            try
            {
                return XUiC_SpawnSelectionWindow.IsInSpawnSelection(ui);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryIsOpenInUi(LocalPlayerUI ui)
        {
            if (ui == null)
            {
                return false;
            }

            try
            {
                return XUiC_SpawnSelectionWindow.IsOpenInUI(ui);
            }
            catch
            {
                return false;
            }
        }

        private PendingSpawnInvocation QueuePendingSpawnInvocation(SpawnMethod method, RespawnUiState state, string note)
        {
            var invocation = new PendingSpawnInvocation
            {
                Method = method,
                State = state,
                Note = note
            };

            lock (spawnSelectionSyncRoot)
            {
                pendingSpawnInvocation = invocation;
            }

            return invocation;
        }

        private void ClearPendingSpawnInvocation(PendingSpawnInvocation invocation)
        {
            lock (spawnSelectionSyncRoot)
            {
                if (ReferenceEquals(pendingSpawnInvocation, invocation))
                {
                    pendingSpawnInvocation = null;
                }
            }
        }

        private static bool IsSpawnSelectionWindowUsable(XUiC_SpawnSelectionWindow controller)
        {
            if (controller == null)
            {
                return false;
            }

            try
            {
                var gameObject = ReadMember(controller, "gameObject") as GameObject;
                return gameObject == null || gameObject.activeInHierarchy;
            }
            catch
            {
                return true;
            }
        }

        private float? ComputeFallbackCooldown(EntityPlayerLocal player)
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

            var snapshot = tracker.GetSnapshot();
            if (string.IsNullOrWhiteSpace(snapshot.LastDeathTime)
                || !DateTime.TryParse(
                    snapshot.LastDeathTime,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var deathUtc))
            {
                return 0f;
            }

            var elapsed = (float)Math.Max(0d, (DateTime.UtcNow - deathUtc).TotalSeconds);
            return Math.Max(0f, waitSeconds - elapsed);
        }

        private static float? NormalizeCooldown(float? cooldown)
        {
            if (!cooldown.HasValue)
            {
                return null;
            }

            return cooldown.Value < 0f ? 0f : cooldown.Value;
        }

        private static bool HasBedrollSpawn(EntityPlayerLocal player)
        {
            if (player == null)
            {
                return false;
            }

            var spawnPoints = player.SpawnPoints;
            if (spawnPoints != null && spawnPoints.Count > 0)
            {
                return true;
            }

            return player.CheckSpawnPointStillThere();
        }

        private static string BuildSpawnSummary(SpawnMethod? method, object spawnTarget, bool bedrollAvailable, EntityPlayerLocal player)
        {
            var targetSummary = BuildSpawnTargetSummary(spawnTarget);
            var methodSummary = method.HasValue ? method.Value.ToString() : "unknown";
            if (!string.IsNullOrWhiteSpace(targetSummary))
            {
                return methodSummary + " -> " + targetSummary;
            }

            if (bedrollAvailable)
            {
                return methodSummary + " -> bedroll available";
            }

            if (player != null)
            {
                var spawnPoint = player.GetSpawnPoint();
                var spawnPointSummary = BuildSpawnTargetSummary(spawnPoint);
                if (!string.IsNullOrWhiteSpace(spawnPointSummary))
                {
                    return methodSummary + " -> " + spawnPointSummary;
                }
            }

            return methodSummary;
        }

        private static string BuildSpawnTargetSummary(object spawnTarget)
        {
            if (spawnTarget == null)
            {
                return null;
            }

            var position = ReadMember(spawnTarget, "position");
            if (position is UnityEngine.Vector3 vector3)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "({0:0.0}, {1:0.0}, {2:0.0})",
                    vector3.x,
                    vector3.y,
                    vector3.z);
            }

            return spawnTarget.ToString();
        }

        private static object ReadMember(object target, string name)
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

        private static float? TryReadFloat(object target, string name)
        {
            var raw = ReadMember(target, name);
            if (raw == null)
            {
                return null;
            }

            if (raw is float single)
            {
                return single;
            }

            if (raw is double @double)
            {
                return (float)@double;
            }

            return float.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (float?)null;
        }

        private static bool? TryReadBool(object target, string name)
        {
            var raw = ReadMember(target, name);
            if (raw == null)
            {
                return null;
            }

            if (raw is bool value)
            {
                return value;
            }

            return bool.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var parsed)
                ? parsed
                : (bool?)null;
        }

        private static TEnum? TryReadEnum<TEnum>(object target, string name) where TEnum : struct
        {
            var raw = ReadMember(target, name);
            if (raw == null)
            {
                return null;
            }

            if (raw is TEnum value)
            {
                return value;
            }

            return Enum.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), true, out TEnum parsed)
                ? parsed
                : (TEnum?)null;
        }

        private sealed class PendingSpawnInvocation
        {
            public PendingSpawnInvocation()
            {
                Completion = new ManualResetEventSlim(false);
            }

            public SpawnMethod Method { get; set; }

            public RespawnUiState State { get; set; }

            public string Note { get; set; }

            public Exception Error { get; private set; }

            public ManualResetEventSlim Completion { get; }

            public void Complete(Exception error)
            {
                Error = error;
                Completion.Set();
            }
        }
    }

    public sealed class RespawnActionExecutionResult
    {
        public bool StateChanged { get; set; }

        public string Note { get; set; }
    }
}
