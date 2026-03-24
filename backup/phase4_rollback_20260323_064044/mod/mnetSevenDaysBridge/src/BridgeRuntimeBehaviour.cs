using System;
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
    }
}
