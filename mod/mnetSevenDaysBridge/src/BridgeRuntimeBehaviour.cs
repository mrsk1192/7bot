using System;
using System.Reflection;
using UnityEngine;

namespace mnetSevenDaysBridge
{
    public sealed class BridgeRuntimeBehaviour : MonoBehaviour
    {
        private InputAdapter inputAdapter;
        private ObservationAdapter observationAdapter;
        private StartupAutomationController startupAutomationController;
        private BridgeLogger logger;
        private bool initialized;
        private bool hadAvailablePlayer;
        private bool visibilityRecoveryPending;
        private float visibilityRecoveryNotBeforeTime;
        private int visibilityRecoveryAttempts;

        public void Initialize(
            InputAdapter inputAdapter,
            ObservationAdapter observationAdapter,
            StartupAutomationController startupAutomationController,
            BridgeLogger logger)
        {
            this.inputAdapter = inputAdapter ?? throw new ArgumentNullException(nameof(inputAdapter));
            this.observationAdapter = observationAdapter ?? throw new ArgumentNullException(nameof(observationAdapter));
            this.startupAutomationController = startupAutomationController ?? throw new ArgumentNullException(nameof(startupAutomationController));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            initialized = true;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            try
            {
                startupAutomationController.Update();
                inputAdapter.Update();
                observationAdapter.Update();
                TryApplyVisibilityRecovery();
            }
            catch (Exception exception)
            {
                logger.Error("Unhandled exception in BridgeRuntimeBehaviour.Update.", exception);
            }
        }

        private void OnDestroy()
        {
            if (!initialized)
            {
                return;
            }

            try
            {
                inputAdapter.ForceNeutralState();
            }
            catch (Exception exception)
            {
                logger.Error("Failed to neutralize input backend during BridgeRuntimeBehaviour teardown.", exception);
            }
        }

        private void TryApplyVisibilityRecovery()
        {
            return;
        }

        private void ScheduleVisibilityRecovery(float delaySeconds, string reason)
        {
            visibilityRecoveryPending = true;
            visibilityRecoveryAttempts = 0;
            visibilityRecoveryNotBeforeTime = Time.unscaledTime + delaySeconds;
            logger.Info("Scheduled one-shot visibility recovery because " + reason + ".");
        }

        private static EntityPlayerLocal TryResolvePlayer()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.World == null)
            {
                return null;
            }

            return gameManager.World.GetPrimaryPlayer() as EntityPlayerLocal;
        }

        private void TryCloseConsoleIfOpen()
        {
            try
            {
                var gameManager = GameManager.Instance;
                if (gameManager == null)
                {
                    return;
                }

                var guiConsole = ReadMember(gameManager, "m_GUIConsole");
                if (guiConsole == null)
                {
                    return;
                }

                var isShowing = TryReadBool(guiConsole, "isShowing", "isInputActive", "shouldOpen", "openWindowOnEsc");
                if (isShowing.HasValue && isShowing.Value)
                {
                    InvokeOptional(gameManager, "SetConsoleWindowVisible", false);
                    logger.Info("Closed the in-game console during startup visibility recovery.");
                }
            }
            catch (Exception exception)
            {
                logger.Warn("Failed to close the in-game console during visibility recovery: " + exception.Message);
            }
        }

        private static bool? TryReadBool(object target, params string[] memberNames)
            => ReflectionUtils.TryReadBool(target, memberNames);

        private static object ReadMember(object target, string name)
            => ReflectionUtils.ReadMember(target, name);

        private static object InvokeOptional(object target, string methodName, params object[] args)
            => ReflectionUtils.InvokeOptional(target, methodName, args);
    }
}
