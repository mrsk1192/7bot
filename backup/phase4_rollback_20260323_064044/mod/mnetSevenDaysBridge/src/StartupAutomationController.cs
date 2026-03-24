using System;
using System.Linq;
using HarmonyLib;

namespace mnetSevenDaysBridge
{
    public sealed class StartupAutomationController
    {
        private readonly BridgeLogger logger;
        private readonly BridgeConfig config;
        private readonly bool externalQuickContinueRequested;
        private XUiC_MainMenuButtons pendingMainMenuButtons;
        private XUiC_NewContinueGame pendingNewContinueGame;
        private bool newGameMenuRequested;
        private bool loadAutomationTriggered;
        private int automationAttemptCount;
        private DateTime scheduledButtonsUtc = DateTime.MinValue;
        private DateTime scheduledNewContinueUtc = DateTime.MinValue;

        public StartupAutomationController(BridgeLogger logger, BridgeConfig config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            externalQuickContinueRequested = DetectExternalQuickContinueRequest();
        }

        public void OnMainMenuButtonsInitialized(XUiC_MainMenuButtons mainMenuButtons)
        {
            if (mainMenuButtons == null || !ShouldRunAutomation())
            {
                return;
            }

            if (newGameMenuRequested && loadAutomationTriggered && GameManager.Instance != null && GameManager.Instance.World == null)
            {
                newGameMenuRequested = false;
                loadAutomationTriggered = false;
                scheduledButtonsUtc = DateTime.UtcNow.AddSeconds(Math.Min(2 + automationAttemptCount, 5));
                logger.Info("Startup automation reset after returning to the main menu without a loaded world.");
            }

            if (!ReferenceEquals(pendingMainMenuButtons, mainMenuButtons))
            {
                pendingMainMenuButtons = mainMenuButtons;
                scheduledButtonsUtc = DateTime.UtcNow.AddSeconds(1);
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
                scheduledNewContinueUtc = DateTime.UtcNow.AddSeconds(1);
                logger.Info("Startup automation captured new/continue game controller.");
            }
        }

        public void Update()
        {
            if (!ShouldRunAutomation())
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.World != null)
            {
                return;
            }

            if (!newGameMenuRequested
                && pendingMainMenuButtons != null
                && pendingMainMenuButtons.xui != null
                && DateTime.UtcNow >= scheduledButtonsUtc)
            {
                TriggerNewGameMenu();
            }

            if (!loadAutomationTriggered
                && pendingNewContinueGame != null
                && pendingNewContinueGame.xui != null
                && DateTime.UtcNow >= scheduledNewContinueUtc)
            {
                TriggerLoadAutomation();
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
                logger.Info(
                    $"Startup automation opening new game flow for world={config.AutoQuickContinueGameWorld} save={config.AutoQuickContinueGameName}.");

                var method = AccessTools.Method(typeof(XUiC_MainMenuButtons), "btnNewGame_OnPressed");
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
                logger.Info(
                    $"Startup automation triggering new/continue automation for world={config.AutoQuickContinueGameWorld} save={config.AutoQuickContinueGameName}.");

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
            return config.AutoQuickContinueOnStartup && !externalQuickContinueRequested;
        }

        private static bool DetectExternalQuickContinueRequest()
        {
            try
            {
                return Environment.GetCommandLineArgs()
                    .Any(arg =>
                        string.Equals(arg, "-quick-continue", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(arg, "-LoadSaveGame=true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(arg, "-loadsavegame=true", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
    }
}
