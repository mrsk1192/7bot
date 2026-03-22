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
        private static BridgeLogger logger;
        private static DateTime lastEnteringAreaSuppressionLogUtc = DateTime.MinValue;

        public static void Apply(BridgeLogger bridgeLogger, InternalInputBackend internalInputBackend, RespawnController bridgeRespawnController)
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

            lock (SyncRoot)
            {
                logger = bridgeLogger;
                backend = internalInputBackend;
                respawnController = bridgeRespawnController;
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
