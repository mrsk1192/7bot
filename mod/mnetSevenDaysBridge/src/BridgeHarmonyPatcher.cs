using System;
using System.Reflection;
using HarmonyLib;

namespace mnetSevenDaysBridge
{
    public static class BridgeHarmonyPatcher
    {
        private const string HarmonyId = "mnet.sevendaysbridge.phase3";

        private static readonly object SyncRoot = new object();
        private static Harmony harmony;
        private static InternalInputBackend backend;
        private static RespawnController respawnController;
        private static StartupAutomationController startupAutomationController;
        private static BridgeLogger logger;
        private static DateTime lastEnteringAreaSuppressionLogUtc = DateTime.MinValue;
        private static bool loggedSpawnNearFriendsSuppression;
        private static bool loggedRichPresenceSuppression;
        private static bool loggedDiscordInGameUpdateSuppression;

        public static void Apply(
            BridgeLogger bridgeLogger,
            InternalInputBackend internalInputBackend,
            RespawnController bridgeRespawnController,
            StartupAutomationController bridgeStartupAutomationController,
            BridgeConfig config)
        {
            if (bridgeLogger == null)
            {
                throw new ArgumentNullException(nameof(bridgeLogger));
            }

            if (internalInputBackend == null)
            {
                throw new ArgumentNullException(nameof(internalInputBackend));
            }

            if (bridgeRespawnController == null)
            {
                throw new ArgumentNullException(nameof(bridgeRespawnController));
            }

            if (bridgeStartupAutomationController == null)
            {
                throw new ArgumentNullException(nameof(bridgeStartupAutomationController));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            lock (SyncRoot)
            {
                logger = bridgeLogger;
                backend = internalInputBackend;
                respawnController = bridgeRespawnController;
                startupAutomationController = bridgeStartupAutomationController;
                if (harmony != null)
                {
                    return;
                }

                loggedSpawnNearFriendsSuppression = false;
                loggedRichPresenceSuppression = false;
                loggedDiscordInGameUpdateSuppression = false;

                harmony = new Harmony(HarmonyId);
                harmony.PatchAll(typeof(BridgeHarmonyPatcher).Assembly);
                logger.Info("Harmony patches applied for internal input bridging.");
            }
        }

        public static void Remove()
        {
            lock (SyncRoot)
            {
                if (harmony == null)
                {
                    return;
                }

                harmony.UnpatchSelf();
                harmony = null;
                backend = null;
                respawnController = null;
                startupAutomationController = null;
                logger = null;
            }
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), "MoveByInput")]
        private static class EntityPlayerLocalMoveByInputPatch
        {
            private static void Prefix(EntityPlayerLocal __instance)
            {
                try
                {
                    backend?.OnBeforeMoveByInput(__instance);
                }
                catch (Exception exception)
                {
                    logger?.Error("Failed during MoveByInput Harmony prefix.", exception);
                }
            }
        }

        [HarmonyPatch(typeof(XUiC_SpawnSelectionWindow), "OnOpen")]
        private static class XUiSpawnSelectionWindowOnOpenPatch
        {
            private static void Postfix(XUiC_SpawnSelectionWindow __instance)
            {
                try
                {
                    respawnController?.OnSpawnSelectionWindowOpened(__instance);
                }
                catch (Exception exception)
                {
                    logger?.Error("Failed during SpawnSelectionWindow OnOpen Harmony postfix.", exception);
                }
            }
        }

        [HarmonyPatch(typeof(XUiC_SpawnSelectionWindow), "OnClose")]
        private static class XUiSpawnSelectionWindowOnClosePatch
        {
            private static void Postfix(XUiC_SpawnSelectionWindow __instance)
            {
                try
                {
                    respawnController?.OnSpawnSelectionWindowClosed(__instance);
                }
                catch (Exception exception)
                {
                    logger?.Error("Failed during SpawnSelectionWindow OnClose Harmony postfix.", exception);
                }
            }
        }

        [HarmonyPatch]
        private static class XUiMainMenuButtonsInitPatch
        {
            private static System.Reflection.MethodBase TargetMethod()
                => AccessTools.Method("XUiC_MainMenuButtons:Init");

            private static void Postfix(object __instance)
            {
                try
                {
                    startupAutomationController?.OnMainMenuButtonsInitialized(__instance);
                }
                catch (Exception exception)
                {
                    logger?.Error("Failed during MainMenuButtons Init Harmony postfix.", exception);
                }
            }
        }

        [HarmonyPatch]
        private static class XUiMainMenuButtonsOnOpenPatch
        {
            private static System.Reflection.MethodBase TargetMethod()
                => AccessTools.Method("XUiC_MainMenuButtons:OnOpen");

            private static void Postfix(object __instance)
            {
                try
                {
                    startupAutomationController?.OnMainMenuButtonsInitialized(__instance);
                }
                catch (Exception exception)
                {
                    logger?.Error("Failed during MainMenuButtons OnOpen Harmony postfix.", exception);
                }
            }
        }

        [HarmonyPatch(typeof(XUiC_NewContinueGame), "OnOpen")]
        private static class XUiNewContinueGameOnOpenPatch
        {
            private static void Postfix(XUiC_NewContinueGame __instance)
            {
                try
                {
                    startupAutomationController?.OnNewContinueGameOpened(__instance);
                }
                catch (Exception exception)
                {
                    logger?.Error("Failed during NewContinueGame OnOpen Harmony postfix.", exception);
                }
            }
        }

        [HarmonyPatch(typeof(XUiC_EnteringArea), "Update")]
        private static class XUiEnteringAreaUpdatePatch
        {
            private static Exception Finalizer(Exception __exception)
            {
                if (__exception == null)
                {
                    return null;
                }

                if (!(__exception is NullReferenceException))
                {
                    return __exception;
                }

                var snapshot = respawnController == null ? null : respawnController.GetSnapshot();
                if (snapshot == null || (!snapshot.JustRespawned && !snapshot.RespawnInProgress))
                {
                    return __exception;
                }

                var nowUtc = DateTime.UtcNow;
                if ((nowUtc - lastEnteringAreaSuppressionLogUtc).TotalSeconds >= 5)
                {
                    lastEnteringAreaSuppressionLogUtc = nowUtc;
                    logger?.Warn("Suppressed XUiC_EnteringArea null-reference during respawn stabilization.");
                }

                return null;
            }
        }

        [HarmonyPatch]
        private static class XUiSpawnNearFriendsListRebuildListPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method("XUiC_SpawnNearFriendsList:RebuildList");
            }

            private static Exception Finalizer(Exception __exception)
            {
                if (!ShouldSuppressNullReference(__exception))
                {
                    return __exception;
                }

                if (!loggedSpawnNearFriendsSuppression)
                {
                    loggedSpawnNearFriendsSuppression = true;
                    logger?.Warn("Suppressed XUiC_SpawnNearFriendsList null-reference while opening the respawn UI.");
                }

                return null;
            }
        }

        [HarmonyPatch]
        private static class SteamRichPresenceUpdatePatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method("Platform.Steam.RichPresence:UpdateRichPresence");
            }

            private static Exception Finalizer(Exception __exception)
            {
                if (!ShouldSuppressNullReference(__exception))
                {
                    return __exception;
                }

                if (!loggedRichPresenceSuppression)
                {
                    loggedRichPresenceSuppression = true;
                    logger?.Warn("Suppressed Platform.Steam.RichPresence null-reference during GameManager update.");
                }

                return null;
            }
        }

        [HarmonyPatch]
        private static class DiscordPresenceManagerSetPartyPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method("DiscordManager+PresenceManager:setParty");
            }

            private static Exception Finalizer(Exception __exception)
            {
                if (!ShouldSuppressNullReference(__exception))
                {
                    return __exception;
                }

                if (!loggedDiscordInGameUpdateSuppression)
                {
                    loggedDiscordInGameUpdateSuppression = true;
                    logger?.Warn("Suppressed DiscordManager PresenceManager.setParty null-reference during GameUpdate.");
                }

                return null;
            }
        }

        private static bool ShouldSuppressNullReference(Exception exception)
        {
            return exception is NullReferenceException;
        }
    }
}
