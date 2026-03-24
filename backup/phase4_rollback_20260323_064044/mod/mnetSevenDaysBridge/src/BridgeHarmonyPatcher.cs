using System;
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
        private static bool autoQuickContinueEnabled;
        private static string autoQuickContinueGameWorld;
        private static string autoQuickContinueGameName;
        private static bool autoQuickContinueTriggered;
        private static DateTime autoQuickContinueScheduledUtc = DateTime.MinValue;
        private static bool autoNewGameMenuRequested;
        private static bool mainMenuButtonsUpdateProbeLogged;
        private static BridgeLogger logger;
        private static DateTime lastEnteringAreaSuppressionLogUtc = DateTime.MinValue;

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
                autoQuickContinueEnabled = config.AutoQuickContinueOnStartup;
                autoQuickContinueGameWorld = config.AutoQuickContinueGameWorld;
                autoQuickContinueGameName = config.AutoQuickContinueGameName;
                autoQuickContinueTriggered = false;
                autoQuickContinueScheduledUtc = DateTime.MinValue;
                autoNewGameMenuRequested = false;
                mainMenuButtonsUpdateProbeLogged = false;
                if (harmony != null)
                {
                    return;
                }

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
                autoQuickContinueEnabled = false;
                autoQuickContinueGameWorld = null;
                autoQuickContinueGameName = null;
                autoQuickContinueTriggered = false;
                autoQuickContinueScheduledUtc = DateTime.MinValue;
                autoNewGameMenuRequested = false;
                mainMenuButtonsUpdateProbeLogged = false;
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

        [HarmonyPatch(typeof(XUiC_MainMenu), "OnOpen")]
        private static class XUiMainMenuOnOpenPatch
        {
            private static void Postfix(XUiC_MainMenu __instance)
            {
                try
                {
                    logger?.Info("Harmony observed XUiC_MainMenu.OnOpen.");
                }
                catch (Exception exception)
                {
                    logger?.Error("Failed during MainMenu OnOpen Harmony postfix.", exception);
                }
            }
        }

        [HarmonyPatch(typeof(XUiC_MainMenu), "Init")]
        private static class XUiMainMenuInitPatch
        {
            private static void Postfix(XUiC_MainMenu __instance)
            {
                try
                {
                    logger?.Info($"Harmony observed XUiC_MainMenu.Init. instance_null={ReferenceEquals(__instance, null)}");
                }
                catch (Exception exception)
                {
                    logger?.Error("Failed during MainMenu Init Harmony postfix.", exception);
                }
            }
        }

        [HarmonyPatch(typeof(XUiC_MainMenu), "Open")]
        private static class XUiMainMenuOpenPatch
        {
            private static void Postfix(XUiC_MainMenu __instance)
            {
                try
                {
                    logger?.Info("Harmony observed XUiC_MainMenu.Open.");
                }
                catch (Exception exception)
                {
                    logger?.Error("Failed during MainMenu Open Harmony postfix.", exception);
                }
            }
        }

        [HarmonyPatch(typeof(XUiC_MainMenuButtons), "Init")]
        private static class XUiMainMenuButtonsInitPatch
        {
            private static void Postfix(XUiC_MainMenuButtons __instance)
            {
                try
                {
                    logger?.Info($"Harmony observed XUiC_MainMenuButtons.Init. instance_null={ReferenceEquals(__instance, null)}");
                    startupAutomationController?.OnMainMenuButtonsInitialized(__instance);
                }
                catch (Exception exception)
                {
                    logger?.Error("Failed during MainMenuButtons Init Harmony postfix.", exception);
                }
            }
        }

        [HarmonyPatch(typeof(XUiC_MainMenuButtons), "OnOpen")]
        private static class XUiMainMenuButtonsOnOpenPatch
        {
            private static void Postfix(XUiC_MainMenuButtons __instance)
            {
                try
                {
                    logger?.Info($"Harmony observed XUiC_MainMenuButtons.OnOpen. instance_null={ReferenceEquals(__instance, null)}");
                    startupAutomationController?.OnMainMenuButtonsInitialized(__instance);
                }
                catch (Exception exception)
                {
                    logger?.Error("Failed during MainMenuButtons OnOpen Harmony postfix.", exception);
                }
            }
        }

        [HarmonyPatch(typeof(XUiC_MainMenuButtons), "Update")]
        private static class XUiMainMenuButtonsUpdatePatch
        {
            private static void Postfix(XUiC_MainMenuButtons __instance)
            {
                try
                {
                    if (!mainMenuButtonsUpdateProbeLogged)
                    {
                        mainMenuButtonsUpdateProbeLogged = true;
                        logger?.Info($"Main menu buttons update probe: instance_null={ReferenceEquals(__instance, null)} enabled={autoQuickContinueEnabled} triggered={autoQuickContinueTriggered} scheduled={autoQuickContinueScheduledUtc:o}");
                    }
                }
                catch (Exception exception)
                {
                    logger?.Error("Failed during MainMenuButtons Update Harmony postfix.", exception);
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
                    logger?.Info("Harmony observed XUiC_NewContinueGame.OnOpen.");
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

    }
}
