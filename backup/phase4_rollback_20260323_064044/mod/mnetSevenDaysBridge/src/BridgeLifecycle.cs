using System;
using System.IO;
using UnityEngine;

namespace mnetSevenDaysBridge
{
    public sealed class BridgeLifecycle : IDisposable
    {
        public const string BridgeVersion = "0.4.0.0";

        private readonly string modRootPath;
        private readonly BridgeConfig config;
        private readonly BridgeLogger logger;
        private readonly CommandQueue commandQueue;
        private readonly ObservationCommandQueue observationCommandQueue;
        private readonly InputStateMachine inputStateMachine;
        private readonly InternalInputBackend internalInputBackend;
        private readonly OSInputBackend osInputBackend;
        private readonly InputBackendRouter inputBackendRouter;
        private readonly DeathRespawnStateTracker deathRespawnStateTracker;
        private readonly RespawnController respawnController;
        private readonly InputAdapter inputAdapter;
        private readonly ObservationService observationService;
        private readonly GameStateCollector collector;
        private readonly ObservationAdapter observationAdapter;
        private readonly StartupAutomationController startupAutomationController;
        private readonly HttpCommandReceiver receiver;
        private GameObject runtimeObject;
        private BridgeRuntimeBehaviour runtimeBehaviour;
        private bool disposed;

        public BridgeLifecycle(string modRootPath)
        {
            if (string.IsNullOrWhiteSpace(modRootPath))
            {
                throw new ArgumentException("Mod root path must not be empty.", nameof(modRootPath));
            }

            this.modRootPath = Path.GetFullPath(modRootPath);
            config = BridgeConfig.Load(modRootPath);
            logger = new BridgeLogger(this.modRootPath, config.MaxLogLinesInMemory);
            commandQueue = new CommandQueue(config);
            observationCommandQueue = new ObservationCommandQueue();
            inputStateMachine = new InputStateMachine();
            internalInputBackend = new InternalInputBackend(logger);
            osInputBackend = new OSInputBackend(logger, config);
            inputBackendRouter = new InputBackendRouter(internalInputBackend, osInputBackend);
            deathRespawnStateTracker = new DeathRespawnStateTracker(logger);
            respawnController = new RespawnController(logger, deathRespawnStateTracker);
            inputAdapter = new InputAdapter(
                logger,
                config,
                commandQueue,
                inputStateMachine,
                internalInputBackend,
                osInputBackend,
                inputBackendRouter,
                respawnController);
            observationService = new ObservationService(logger);
            startupAutomationController = new StartupAutomationController(logger, config);
            collector = new GameStateCollector(
                logger,
                () => inputAdapter.GetInputState(),
                () => inputAdapter.GetUiState(),
                () => inputAdapter.IsBackendAvailable(),
                () => inputAdapter.GetDeathRespawnState(),
                observationService);
            observationAdapter = new ObservationAdapter(
                logger,
                collector,
                observationService,
                observationCommandQueue);
            receiver = new HttpCommandReceiver(
                config,
                logger,
                new BridgeJson(),
                collector,
                CreateVersionInfo(),
                inputAdapter,
                commandQueue,
                observationService,
                observationCommandQueue);
        }


        public void Start()
        {
            if (!config.Enabled)
            {
                throw new InvalidOperationException("mnetSevenDaysBridge is disabled in bridge_config.json.");
            }

            logger.Info("mnetSevenDaysBridge startup requested.");
            logger.Info(
                $"Version={BridgeVersion} Mode={config.CommunicationMode} Bind={config.Host}:{config.Port} Backend={inputAdapter.ActiveBackendName}");
            logger.Info(
                $"RateLimit={config.MaxCommandsPerSecond}/s QueueLimit={config.MaxCommandQueueLength} DefaultLookStep={config.DefaultLookStep}");
            logger.Info($"OS input backend enabled={config.EnableOsInputBackend} bring_to_front={config.BringGameWindowToFrontForOsInput}");
            logger.Info("Phase 4 policy: observation APIs return explicit look-target, interaction, nearby resource/interactable/entity, and terrain summaries without CV/OCR.");
            BridgeHarmonyPatcher.Apply(logger, internalInputBackend, respawnController, startupAutomationController, config);
            EnsureRuntimeBehaviour();
            receiver.Start();
            logger.Info($"Startup completed. Log file: {logger.LogFilePath}");
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            receiver.Dispose();
            try
            {
                inputAdapter.ForceNeutralState();
            }
            catch (Exception exception)
            {
                logger.Error("Failed to force a neutral input state during shutdown.", exception);
            }

            if (runtimeObject != null)
            {
                UnityEngine.Object.Destroy(runtimeObject);
                runtimeObject = null;
            }

            BridgeHarmonyPatcher.Remove();

            logger.Info("mnetSevenDaysBridge shutdown completed.");
        }

        private VersionInfo CreateVersionInfo()
        {
            return new VersionInfo
            {
                BridgeVersion = BridgeVersion,
                CommunicationMode = config.CommunicationMode,
                Host = config.Host,
                Port = config.Port,
                ModApiEntryPoint = "IModApi.InitMod(Mod)",
                Runtime = Environment.Version.ToString(),
                ActiveBackend = inputAdapter.ActiveBackendName
            };
        }

        private void EnsureRuntimeBehaviour()
        {
            if (runtimeBehaviour != null)
            {
                return;
            }

            runtimeObject = new GameObject("mnetSevenDaysBridgeRuntime");
            UnityEngine.Object.DontDestroyOnLoad(runtimeObject);
            runtimeBehaviour = runtimeObject.AddComponent<BridgeRuntimeBehaviour>();
            runtimeBehaviour.Initialize(inputAdapter, observationAdapter, startupAutomationController, logger);
        }
    }
}
