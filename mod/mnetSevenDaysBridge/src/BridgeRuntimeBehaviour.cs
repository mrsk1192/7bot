using System;
using UnityEngine;

namespace mnetSevenDaysBridge
{
    public sealed class BridgeRuntimeBehaviour : MonoBehaviour
    {
        private InputAdapter inputAdapter;
        private BridgeLogger logger;
        private bool initialized;

        public void Initialize(InputAdapter inputAdapter, BridgeLogger logger)
        {
            this.inputAdapter = inputAdapter ?? throw new ArgumentNullException(nameof(inputAdapter));
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
                inputAdapter.Update();
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
