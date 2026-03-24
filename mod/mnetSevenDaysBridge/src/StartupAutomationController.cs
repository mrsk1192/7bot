using System;
using System.Linq;
using HarmonyLib;

namespace mnetSevenDaysBridge
{
    public sealed class StartupAutomationController
    {
        private readonly BridgeLogger logger;
        private readonly BridgeConfig config;
        private object pendingMainMenuButtons;
        private XUiC_NewContinueGame pendingNewContinueGame;
        private bool newGameMenuRequested;
        private bool loadAutomationTriggered;
        private bool automationFinished;
        private bool loadAutomationRetriedOnce;
        private int automationAttemptCount;
        private DateTime lastLoadAttemptUtc = DateTime.MinValue;
        private DateTime scheduledButtonsUtc = DateTime.MinValue;
        private DateTime scheduledNewContinueUtc = DateTime.MinValue;
        private const int MaxAutomationAttempts = 5;

        public StartupAutomationController(BridgeLogger logger, BridgeConfig config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void OnMainMenuButtonsInitialized(object mainMenuButtons)
        {
            if (mainMenuButtons == null || !ShouldRunAutomation())
            {
                return;
            }

            if (!ReferenceEquals(pendingMainMenuButtons, mainMenuButtons))
            {
                pendingMainMenuButtons = mainMenuButtons;
                scheduledButtonsUtc = DateTime.UtcNow.AddSeconds(2);
                logger.Info("Startup automation captured main menu buttons controller.");
            }
        }

        public void OnNewContinueGameOpened(XUiC_NewContinueGame newContinueGame)
        {
            if (newContinueGame == null || !ShouldRunAutomation())
            {
                return;
            }

            if (!ReferenceEquals(pendingNewContinueGame, newContinueGame))
            {
                pendingNewContinueGame = newContinueGame;
                loadAutomationRetriedOnce = false;
                scheduledNewContinueUtc = DateTime.UtcNow.AddSeconds(5);
                logger.Info("Startup automation captured new/continue game controller.");
            }
        }

        public void Update()
        {
            if (!ShouldRunAutomation())
            {
                return;
            }

            var gameManager = GameManager.Instance;
            var hasPrimaryPlayer = gameManager != null
                && gameManager.World != null
                && gameManager.World.GetPrimaryPlayer() != null;
            if (hasPrimaryPlayer)
            {
                automationFinished = true;
                return;
            }

            if (!newGameMenuRequested
                && pendingMainMenuButtons != null
                && ReflectionUtils.ReadMember(pendingMainMenuButtons, "xui") != null
                && DateTime.UtcNow >= scheduledButtonsUtc)
            {
                TriggerNewGameMenu();
            }

            if (!loadAutomationTriggered
                && pendingNewContinueGame != null
                && ReflectionUtils.ReadMember(pendingNewContinueGame, "xui") != null
                && DateTime.UtcNow >= scheduledNewContinueUtc)
            {
                TriggerLoadAutomation();
                return;
            }

            if (loadAutomationTriggered
                && pendingNewContinueGame != null
                && ReflectionUtils.ReadMember(pendingNewContinueGame, "xui") != null
                && !loadAutomationRetriedOnce
                && DateTime.UtcNow - lastLoadAttemptUtc >= TimeSpan.FromSeconds(8))
            {
                logger.Warn("Startup automation is retrying the save-load trigger one final time because the world is still unavailable.");
                loadAutomationTriggered = false;
                loadAutomationRetriedOnce = true;
                scheduledNewContinueUtc = DateTime.UtcNow;
                TriggerLoadAutomation();
                return;
            }

            if (newGameMenuRequested
                && pendingNewContinueGame == null
                && automationAttemptCount < MaxAutomationAttempts
                && DateTime.UtcNow - scheduledButtonsUtc >= TimeSpan.FromSeconds(10))
            {
                logger.Warn("Startup automation is retrying the continue flow because the new/continue controller did not become available.");
                newGameMenuRequested = false;
                scheduledButtonsUtc = DateTime.UtcNow.AddSeconds(2);
                TriggerNewGameMenu();
                return;
            }

        }

        private void TriggerNewGameMenu()
        {
            if (pendingMainMenuButtons == null || newGameMenuRequested)
            {
                return;
            }

            try
            {
                ApplyTargetPrefs();
                newGameMenuRequested = true;
                automationFinished = false;
                logger.Info(
                    $"Startup automation opening continue flow for world={config.AutoQuickContinueGameWorld} save={config.AutoQuickContinueGameName}.");

                // Try quickContinue via XUiC_MainMenu parent (reflection, no direct type reference)
                var mainMenuController = ReflectionUtils.InvokeOptional(pendingMainMenuButtons, "GetParentByType", null);
                var quickContinueMethod = AccessTools.Method("XUiC_MainMenu:quickContinue");
                if (mainMenuController != null && quickContinueMethod != null)
                {
                    automationAttemptCount++;
                    lastLoadAttemptUtc = DateTime.UtcNow;
                    logger.Info(
                        $"Startup automation invoking main-menu quickContinue for world={config.AutoQuickContinueGameWorld} save={config.AutoQuickContinueGameName}. Attempt={automationAttemptCount}/{MaxAutomationAttempts}.");
                    quickContinueMethod.Invoke(mainMenuController, null);
                    return;
                }

                var continueMethod = AccessTools.Method("XUiC_MainMenuButtons:btnContinueGame_OnPressed");
                if (continueMethod != null)
                {
                    automationAttemptCount++;
                    lastLoadAttemptUtc = DateTime.UtcNow;
                    logger.Info(
                        $"Startup automation invoking main-menu continue button for world={config.AutoQuickContinueGameWorld} save={config.AutoQuickContinueGameName}. Attempt={automationAttemptCount}/{MaxAutomationAttempts}.");
                    continueMethod.Invoke(pendingMainMenuButtons, new object[] { null, -1 });
                    return;
                }

                var mainMenuAutomationMethod = AccessTools.Method("XUiC_MainMenuButtons:DoLoadSaveGameAutomation");
                if (mainMenuAutomationMethod != null)
                {
                    automationAttemptCount++;
                    lastLoadAttemptUtc = DateTime.UtcNow;
                    logger.Info(
                        $"Startup automation invoking main-menu load automation fallback for world={config.AutoQuickContinueGameWorld} save={config.AutoQuickContinueGameName}. Attempt={automationAttemptCount}/{MaxAutomationAttempts}.");
                    mainMenuAutomationMethod.Invoke(pendingMainMenuButtons, null);
                    return;
                }

                logger.Warn("Main menu continue method was unavailable; falling back to new game flow.");
                var method = AccessTools.Method("XUiC_MainMenuButtons:btnNewGame_OnPressed");
                method?.Invoke(pendingMainMenuButtons, new object[] { null, -1 });
            }
            catch (Exception exception)
            {
                newGameMenuRequested = false;
                logger.Error("Startup automation failed while opening the new game flow.", exception);
            }
        }

        private void TriggerLoadAutomation()
        {
            if (pendingNewContinueGame == null || loadAutomationTriggered)
            {
                return;
            }

            try
            {
                ApplyTargetPrefs();
                loadAutomationTriggered = true;
                automationAttemptCount++;
                lastLoadAttemptUtc = DateTime.UtcNow;
                automationFinished = false;
                logger.Info(
                    $"Startup automation triggering new/continue automation for world={config.AutoQuickContinueGameWorld} save={config.AutoQuickContinueGameName}. Attempt={automationAttemptCount}/{MaxAutomationAttempts}.");

                var setContinueMethod = AccessTools.Method(typeof(XUiC_NewContinueGame), "SetIsContinueGame");
                setContinueMethod?.Invoke(pendingNewContinueGame, new object[] { pendingNewContinueGame.xui, true });

                var selectWorldMethod = AccessTools.Method(typeof(XUiC_NewContinueGame), "SelectWorld");
                if (selectWorldMethod != null && !string.IsNullOrWhiteSpace(config.AutoQuickContinueGameWorld))
                {
                    selectWorldMethod.Invoke(pendingNewContinueGame, new object[] { config.AutoQuickContinueGameWorld });
                }

                var automationMethod = AccessTools.Method(typeof(XUiC_NewContinueGame), "DoLoadSaveGameAutomation");
                automationMethod?.Invoke(pendingNewContinueGame, null);

                var saveOptionsMethod = AccessTools.Method(typeof(XUiC_NewContinueGame), "SaveGameOptions");
                saveOptionsMethod?.Invoke(pendingNewContinueGame, null);

                var startMethod = AccessTools.Method(typeof(XUiC_NewContinueGame), "BtnStart_OnPressed");
                startMethod?.Invoke(pendingNewContinueGame, new object[] { null, -1 });
            }
            catch (Exception exception)
            {
                loadAutomationTriggered = false;
                logger.Error("Startup automation failed while triggering the new/continue automation.", exception);
            }
        }

        private void ApplyTargetPrefs()
        {
            if (!string.IsNullOrWhiteSpace(config.AutoQuickContinueGameWorld))
            {
                GamePrefs.Set(EnumGamePrefs.GameWorld, config.AutoQuickContinueGameWorld);
            }

            if (!string.IsNullOrWhiteSpace(config.AutoQuickContinueGameName))
            {
                GamePrefs.Set(EnumGamePrefs.GameName, config.AutoQuickContinueGameName);
            }

            GamePrefs.Instance.Save();
        }

        private bool ShouldRunAutomation()
        {
            if (!config.AutoQuickContinueOnStartup || automationFinished)
            {
                return false;
            }

            var args = Environment.GetCommandLineArgs();
            return !args.Any(arg =>
                string.Equals(arg, "-quick-continue", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-LoadSaveGame=true", StringComparison.OrdinalIgnoreCase));
        }
    }
}
