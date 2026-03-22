using System;
using System.Collections.Generic;

namespace mnetSevenDaysBridge
{
    public sealed class InputAdapter
    {
        private readonly BridgeLogger logger;
        private readonly BridgeConfig config;
        private readonly CommandQueue commandQueue;
        private readonly InputStateMachine stateMachine;
        private readonly InternalInputBackend internalBackend;
        private readonly OSInputBackend osBackend;
        private readonly InputBackendRouter router;
        private readonly RespawnController respawnController;
        private UiState lastUiState;

        public InputAdapter(
            BridgeLogger logger,
            BridgeConfig config,
            CommandQueue commandQueue,
            InputStateMachine stateMachine,
            InternalInputBackend internalBackend,
            OSInputBackend osBackend,
            InputBackendRouter router,
            RespawnController respawnController)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
            this.stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            this.internalBackend = internalBackend ?? throw new ArgumentNullException(nameof(internalBackend));
            this.osBackend = osBackend ?? throw new ArgumentNullException(nameof(osBackend));
            this.router = router ?? throw new ArgumentNullException(nameof(router));
            this.respawnController = respawnController ?? throw new ArgumentNullException(nameof(respawnController));
            lastUiState = internalBackend.GetUiState();
        }

        public string ActiveBackendName
        {
            get { return router.ActiveBackendName; }
        }

        public CapabilitySet GetCapabilities()
        {
            return ActionCatalog.BuildCapabilities(router.ActiveBackendName, router.GetAvailableBackends());
        }

        public InputState GetInputState()
        {
            var movementLocked = (lastUiState != null && lastUiState.MenuOpen)
                || respawnController.GetSnapshot().IsDead;
            return stateMachine.GetInputState(movementLocked);
        }

        public UiState GetUiState()
        {
            return lastUiState ?? internalBackend.GetUiState();
        }

        public bool IsBackendAvailable()
        {
            return internalBackend.IsAvailable || osBackend.IsAvailable;
        }

        public DeathRespawnStateSnapshot GetDeathRespawnState()
        {
            return respawnController.GetSnapshot();
        }

        public void Update()
        {
            HandleDeathRespawnTransition(respawnController.RefreshState());

            var pendingCommands = commandQueue.Drain();
            var completions = new List<PendingCompletion>(pendingCommands.Count);
            foreach (var queuedCommand in pendingCommands)
            {
                completions.Add(ProcessQueuedCommand(queuedCommand));
            }

            try
            {
                var uiState = internalBackend.GetUiState();
                var snapshot = respawnController.GetSnapshot();
                var movementLocked = (uiState != null && uiState.MenuOpen) || snapshot.IsDead || snapshot.RespawnInProgress;
                internalBackend.Apply(stateMachine.ConsumeFrameState(movementLocked));
                lastUiState = internalBackend.GetUiState();
                HandleDeathRespawnTransition(respawnController.RefreshState());

                foreach (var completion in completions)
                {
                    if (completion.Error != null)
                    {
                        completion.Command.CompleteFailure(completion.Error.Type, completion.Error.Message);
                    }
                    else
                    {
                        completion.Command.CompleteSuccess(completion.Result);
                    }
                }
            }
            catch (Exception exception)
            {
                logger.Error("Failed while applying input state to the active backend.", exception);
                foreach (var completion in completions)
                {
                    completion.Command.CompleteFailure("backend_apply_failed", exception.Message);
                }
            }
        }

        public void ForceNeutralState()
        {
            stateMachine.ResetToNeutral();
            internalBackend.ForceNeutralState();
            osBackend.ForceNeutralState();
            respawnController.GetSnapshot();
            lastUiState = internalBackend.GetUiState();
        }

        public InputCommandResult WaitForRespawnAction(string action, Dictionary<string, object> arguments)
        {
            var transition = respawnController.RefreshState();
            HandleDeathRespawnTransition(transition);
            var snapshot = transition == null ? respawnController.GetSnapshot() : transition.Snapshot;
            if (snapshot.Alive.GetValueOrDefault(false) && !snapshot.IsDead && !snapshot.RespawnInProgress && !snapshot.RespawnScreenOpen)
            {
                throw new BridgeCommandException(409, "INVALID_RESPAWN_STATE", "Respawn wait commands are only valid while dead or respawning.");
            }

            var timeoutMs = GetTimeoutMs(arguments, string.Equals(action, "wait_for_respawn_complete", StringComparison.OrdinalIgnoreCase) ? 30000 : 15000);
            var result = string.Equals(action, "wait_for_respawn_complete", StringComparison.OrdinalIgnoreCase)
                ? respawnController.WaitForRespawnComplete(timeoutMs)
                : respawnController.WaitForRespawnScreen(timeoutMs);

            return new InputCommandResult
            {
                Action = action,
                Accepted = true,
                StateChanged = result.StateChanged,
                Backend = router.GetReportedBackend(action),
                Note = result.Note,
                InputState = GetInputState()
            };
        }

        private PendingCompletion ProcessQueuedCommand(QueuedBridgeCommand queuedCommand)
        {
            try
            {
                var action = queuedCommand.Command.Name;
                var definition = ActionCatalog.Get(action);
                if (!definition.Supported)
                {
                    return PendingCompletion.FromError(queuedCommand, "unsupported_action", definition.Note);
                }

                var snapshot = respawnController.GetSnapshot();
                if (ActionCatalog.IsRespawnAction(action))
                {
                    if (snapshot.Alive == true && !snapshot.IsDead && !snapshot.RespawnScreenOpen)
                    {
                        return PendingCompletion.FromError(queuedCommand, "INVALID_RESPAWN_STATE", "Respawn commands are only available while dead or on the respawn screen.");
                    }

                    var respawnResult = respawnController.ExecuteAction(action, queuedCommand.Command.Arguments);
                    return PendingCompletion.FromResult(queuedCommand, BuildCommandResult(action, router.GetReportedBackend(action), respawnResult.StateChanged, respawnResult.Note));
                }

                if ((snapshot.IsDead || snapshot.RespawnScreenOpen || snapshot.RespawnInProgress) && !IsSafetyAction(action))
                {
                    return PendingCompletion.FromError(queuedCommand, "PLAYER_DEAD", "Normal gameplay commands are rejected while dead or during respawn.");
                }

                if (definition.RequiresPlayer && !internalBackend.TryGetPlayer(out _, out var reason))
                {
                    return PendingCompletion.FromError(queuedCommand, "player_unavailable", reason);
                }

                if (string.Equals(action, "look_to", StringComparison.OrdinalIgnoreCase))
                {
                    return PendingCompletion.FromResult(queuedCommand, ExecuteLookTo(queuedCommand.Command));
                }

                var result = stateMachine.ApplyCommand(
                    action,
                    queuedCommand.Command.Arguments,
                    config,
                    router.GetReportedBackend(action));

                if (router.UsesInternal(action) && UsesImmediateInternalAction(action))
                {
                    internalBackend.ExecuteImmediateAction(action, queuedCommand.Command.Arguments);
                    lastUiState = internalBackend.GetUiState();
                }

                if (!IsSafetyAction(action) && router.UsesOs(action))
                {
                    osBackend.ExecuteAction(action, queuedCommand.Command.Arguments);
                }

                if (IsSafetyAction(action))
                {
                    internalBackend.ForceNeutralState();
                    osBackend.ForceNeutralState();
                    result.Note = "All backend-held state was reset to neutral.";
                    result.InputState = stateMachine.GetInputState(lastUiState != null && lastUiState.MenuOpen);
                }

                return PendingCompletion.FromResult(queuedCommand, result);
            }
            catch (BridgeCommandException exception)
            {
                return PendingCompletion.FromError(queuedCommand, exception.ErrorType, exception.Message);
            }
            catch (Exception exception)
            {
                logger.Error("Failed while processing a queued input command.", exception);
                return PendingCompletion.FromError(queuedCommand, "input_command_failed", exception.Message);
            }
        }

        private InputCommandResult ExecuteLookTo(BridgeCommand command)
        {
            var yaw = GetRequiredFloat(command.Arguments, "yaw");
            var pitch = GetRequiredFloat(command.Arguments, "pitch");
            if (float.IsNaN(yaw) || float.IsInfinity(yaw) || float.IsNaN(pitch) || float.IsInfinity(pitch))
            {
                throw new BridgeCommandException(400, "bad_request", "yaw and pitch must be finite.");
            }

            internalBackend.SetAbsoluteLook(yaw, pitch);
            lastUiState = internalBackend.GetUiState();

            return new InputCommandResult
            {
                Action = command.Name,
                Accepted = true,
                StateChanged = true,
                Backend = router.GetReportedBackend(command.Name),
                Note = "Absolute look was applied through the internal camera rotation path.",
                InputState = stateMachine.GetInputState(lastUiState != null && lastUiState.MenuOpen)
            };
        }

        private void HandleDeathRespawnTransition(DeathRespawnStateUpdateResult transition)
        {
            if (transition == null)
            {
                return;
            }

            if (transition.Died)
            {
                stateMachine.ResetToNeutral();
                internalBackend.ForceNeutralState();
                osBackend.ForceNeutralState();
                lastUiState = internalBackend.GetUiState();
                logger.Info("Held inputs were cleared because the player died.");
                return;
            }

            if (transition.Respawned)
            {
                stateMachine.ResetToNeutral();
                internalBackend.ForceNeutralState();
                osBackend.ForceNeutralState();
                lastUiState = internalBackend.GetUiState();
                logger.Info("Held inputs were reset after respawn completed.");
            }
        }

        private InputCommandResult BuildCommandResult(string action, string backend, bool stateChanged, string note)
        {
            return new InputCommandResult
            {
                Action = action,
                Accepted = true,
                StateChanged = stateChanged,
                Backend = backend,
                Note = note,
                InputState = GetInputState()
            };
        }

        private static bool IsSafetyAction(string action)
        {
            return string.Equals(action, "stop_all_input", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "reset_all_toggles", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "release_all_held_keys", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "emergency_neutral_state", StringComparison.OrdinalIgnoreCase);
        }

        private static bool UsesImmediateInternalAction(string action)
        {
            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "select_hotbar_slot":
                case "hotbar_next":
                case "hotbar_prev":
                case "mouse_wheel_up":
                case "mouse_wheel_down":
                case "toggle_inventory":
                case "toggle_map":
                case "toggle_quest_log":
                case "escape_menu":
                case "confirm":
                case "cancel":
                case "toggle_flashlight":
                case "console_toggle":
                    return true;
                default:
                    return false;
            }
        }

        private static float GetRequiredFloat(Dictionary<string, object> arguments, string key)
        {
            if (arguments != null)
            {
                foreach (var pair in arguments)
                {
                    if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return Convert.ToSingle(pair.Value, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }

            throw new BridgeCommandException(400, "bad_request", "Missing required argument: " + key);
        }

        private static int GetTimeoutMs(Dictionary<string, object> arguments, int defaultValue)
        {
            if (arguments != null)
            {
                foreach (var pair in arguments)
                {
                    if (string.Equals(pair.Key, "timeout_ms", StringComparison.OrdinalIgnoreCase))
                    {
                        return Convert.ToInt32(pair.Value, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }

            return defaultValue;
        }

        private sealed class PendingCompletion
        {
            public static PendingCompletion FromError(QueuedBridgeCommand command, string errorType, string errorMessage)
            {
                return new PendingCompletion
                {
                    Command = command,
                    Error = new BridgeError
                    {
                        Type = errorType,
                        Message = errorMessage
                    }
                };
            }

            public static PendingCompletion FromResult(QueuedBridgeCommand command, object result)
            {
                return new PendingCompletion
                {
                    Command = command,
                    Result = result
                };
            }

            public QueuedBridgeCommand Command { get; set; }

            public object Result { get; set; }

            public BridgeError Error { get; set; }
        }
    }
}
