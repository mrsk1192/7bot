using System;
using System.Collections.Generic;

namespace mnetSevenDaysBridge
{
    public sealed class InputStateMachine
    {
        private readonly object syncRoot = new object();

        private bool moveForward;
        private bool moveBack;
        private bool moveLeft;
        private bool moveRight;
        private bool sprint;
        private bool crouch;
        private bool primaryAction;
        private bool secondaryAction;
        private bool holdInteract;
        private bool autoRun;

        private bool jumpPulse;
        private bool reloadPulse;
        private bool useInteractPulse;
        private bool primaryTapPulse;
        private bool hotbarNextPulse;
        private bool hotbarPrevPulse;
        private bool mouseWheelUpPulse;
        private bool mouseWheelDownPulse;
        private bool toggleInventoryPulse;
        private bool toggleMapPulse;
        private bool toggleQuestLogPulse;
        private bool toggleFlashlightPulse;
        private bool escapeMenuPulse;
        private bool confirmPulse;
        private bool cancelPulse;
        private bool consoleTogglePulse;
        private int? selectHotbarSlotPulse;
        private float accumulatedLookDx;
        private float accumulatedLookDy;

        public InputCommandResult ApplyCommand(string action, Dictionary<string, object> arguments, BridgeConfig config, string backendName)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                throw new BridgeCommandException(400, "bad_request", "Action name was empty.");
            }

            lock (syncRoot)
            {
                var stateChanged = false;
                var note = ActionCatalog.Get(action).Note;

                switch (action.ToLowerInvariant())
                {
                    case "move_forward_start":
                        stateChanged = SetFlag(ref moveForward, true);
                        break;
                    case "move_forward_stop":
                        stateChanged = SetFlag(ref moveForward, false);
                        break;
                    case "move_back_start":
                        stateChanged = SetFlag(ref moveBack, true);
                        break;
                    case "move_back_stop":
                        stateChanged = SetFlag(ref moveBack, false);
                        break;
                    case "move_left_start":
                        stateChanged = SetFlag(ref moveLeft, true);
                        break;
                    case "move_left_stop":
                        stateChanged = SetFlag(ref moveLeft, false);
                        break;
                    case "move_right_start":
                        stateChanged = SetFlag(ref moveRight, true);
                        break;
                    case "move_right_stop":
                        stateChanged = SetFlag(ref moveRight, false);
                        break;
                    case "jump":
                        stateChanged = QueuePulse(ref jumpPulse);
                        break;
                    case "crouch_start":
                        stateChanged = SetFlag(ref crouch, true);
                        break;
                    case "crouch_stop":
                        stateChanged = SetFlag(ref crouch, false);
                        break;
                    case "crouch_toggle":
                        crouch = !crouch;
                        stateChanged = true;
                        note = "Bridge-held crouch state toggled.";
                        break;
                    case "sprint_start":
                        stateChanged = SetFlag(ref sprint, true);
                        break;
                    case "sprint_stop":
                        stateChanged = SetFlag(ref sprint, false);
                        break;
                    case "autorun_toggle":
                        autoRun = !autoRun;
                        stateChanged = true;
                        note = "Auto-run toggled.";
                        break;
                    case "look_delta":
                        var dx = GetRequiredFloat(arguments, "dx");
                        var dy = GetRequiredFloat(arguments, "dy");
                        EnsureFinite(dx, "dx");
                        EnsureFinite(dy, "dy");
                        accumulatedLookDx += dx;
                        accumulatedLookDy += dy;
                        stateChanged = true;
                        note = "Queued look delta.";
                        break;
                    case "turn_left":
                        accumulatedLookDx -= config.DefaultLookStep;
                        stateChanged = true;
                        break;
                    case "turn_right":
                        accumulatedLookDx += config.DefaultLookStep;
                        stateChanged = true;
                        break;
                    case "look_up":
                        accumulatedLookDy -= config.DefaultLookStep;
                        stateChanged = true;
                        break;
                    case "look_down":
                        accumulatedLookDy += config.DefaultLookStep;
                        stateChanged = true;
                        break;
                    case "primary_action_start":
                        stateChanged = SetFlag(ref primaryAction, true);
                        break;
                    case "primary_action_stop":
                        stateChanged = SetFlag(ref primaryAction, false);
                        break;
                    case "secondary_action_start":
                    case "aim_start":
                        stateChanged = SetFlag(ref secondaryAction, true);
                        break;
                    case "secondary_action_stop":
                    case "aim_stop":
                        stateChanged = SetFlag(ref secondaryAction, false);
                        break;
                    case "reload":
                        stateChanged = QueuePulse(ref reloadPulse);
                        break;
                    case "use_interact":
                        stateChanged = QueuePulse(ref useInteractPulse);
                        break;
                    case "hold_interact_start":
                        stateChanged = SetFlag(ref holdInteract, true);
                        break;
                    case "hold_interact_stop":
                        stateChanged = SetFlag(ref holdInteract, false);
                        break;
                    case "attack_light_tap":
                        stateChanged = QueuePulse(ref primaryTapPulse);
                        break;
                    case "select_hotbar_slot":
                        var slot = GetRequiredInt(arguments, "slot");
                        if (slot < 1 || slot > 10)
                        {
                            throw new BridgeCommandException(400, "bad_request", "slot must be in the range 1..10.");
                        }

                        selectHotbarSlotPulse = slot;
                        stateChanged = true;
                        note = "Queued hotbar slot selection.";
                        break;
                    case "hotbar_next":
                        stateChanged = QueuePulse(ref hotbarNextPulse);
                        break;
                    case "hotbar_prev":
                        stateChanged = QueuePulse(ref hotbarPrevPulse);
                        break;
                    case "mouse_wheel_up":
                        stateChanged = QueuePulse(ref mouseWheelUpPulse);
                        break;
                    case "mouse_wheel_down":
                        stateChanged = QueuePulse(ref mouseWheelDownPulse);
                        break;
                    case "toggle_inventory":
                        stateChanged = QueuePulse(ref toggleInventoryPulse);
                        break;
                    case "toggle_map":
                        stateChanged = QueuePulse(ref toggleMapPulse);
                        break;
                    case "toggle_quest_log":
                        stateChanged = QueuePulse(ref toggleQuestLogPulse);
                        break;
                    case "escape_menu":
                        stateChanged = QueuePulse(ref escapeMenuPulse);
                        break;
                    case "confirm":
                        stateChanged = QueuePulse(ref confirmPulse);
                        break;
                    case "cancel":
                        stateChanged = QueuePulse(ref cancelPulse);
                        break;
                    case "toggle_flashlight":
                        stateChanged = QueuePulse(ref toggleFlashlightPulse);
                        break;
                    case "console_toggle":
                        stateChanged = QueuePulse(ref consoleTogglePulse);
                        break;
                    case "stop_all_input":
                    case "reset_all_toggles":
                    case "release_all_held_keys":
                    case "emergency_neutral_state":
                        stateChanged = ClearAllUnsafe();
                        note = "Bridge-held state reset to neutral.";
                        break;
                    case "look_to":
                        case "attack_heavy_start":
                        case "attack_heavy_stop":
                        if (action.StartsWith("attack_heavy", StringComparison.OrdinalIgnoreCase))
                        {
                            stateChanged = SetFlag(ref secondaryAction, action.EndsWith("_start", StringComparison.OrdinalIgnoreCase));
                            note = "Heavy attack is mapped to the secondary action path.";
                            break;
                        }

                        throw new BridgeCommandException(400, "unsupported_action", ActionCatalog.Get(action).Note);
                    default:
                        throw new BridgeCommandException(400, "unknown_action", "Unknown action: " + action);
                }

                return new InputCommandResult
                {
                    Action = action,
                    Accepted = true,
                    StateChanged = stateChanged,
                    Backend = backendName,
                    Note = stateChanged ? note : "Command was idempotent; bridge-held state was already in the requested state.",
                    InputState = BuildPublicStateUnsafe(false)
                };
            }
        }

        public InputState GetInputState(bool movementLocked)
        {
            lock (syncRoot)
            {
                return BuildPublicStateUnsafe(movementLocked);
            }
        }

        public bool ResetToNeutral()
        {
            lock (syncRoot)
            {
                return ClearAllUnsafe();
            }
        }

        public InputFrameState ConsumeFrameState(bool movementLocked)
        {
            lock (syncRoot)
            {
                var frame = new InputFrameState
                {
                    MovementLocked = movementLocked,
                    MoveForward = moveForward,
                    MoveBack = moveBack,
                    MoveLeft = moveLeft,
                    MoveRight = moveRight,
                    Sprint = sprint,
                    Crouch = crouch,
                    PrimaryAction = primaryAction,
                    SecondaryAction = secondaryAction,
                    HoldInteract = holdInteract,
                    AutoRun = autoRun,
                    JumpPulse = jumpPulse,
                    ReloadPulse = reloadPulse,
                    UseInteractPulse = useInteractPulse,
                    PrimaryTapPulse = primaryTapPulse,
                    HotbarNextPulse = hotbarNextPulse,
                    HotbarPrevPulse = hotbarPrevPulse,
                    MouseWheelUpPulse = mouseWheelUpPulse,
                    MouseWheelDownPulse = mouseWheelDownPulse,
                    ToggleInventoryPulse = toggleInventoryPulse,
                    ToggleMapPulse = toggleMapPulse,
                    ToggleQuestLogPulse = toggleQuestLogPulse,
                    ToggleFlashlightPulse = toggleFlashlightPulse,
                    EscapeMenuPulse = escapeMenuPulse,
                    ConfirmPulse = confirmPulse,
                    CancelPulse = cancelPulse,
                    ConsoleTogglePulse = consoleTogglePulse,
                    SelectHotbarSlotPulse = selectHotbarSlotPulse,
                    LookDx = accumulatedLookDx,
                    LookDy = accumulatedLookDy
                };

                jumpPulse = false;
                reloadPulse = false;
                useInteractPulse = false;
                primaryTapPulse = false;
                hotbarNextPulse = false;
                hotbarPrevPulse = false;
                mouseWheelUpPulse = false;
                mouseWheelDownPulse = false;
                toggleInventoryPulse = false;
                toggleMapPulse = false;
                toggleQuestLogPulse = false;
                toggleFlashlightPulse = false;
                escapeMenuPulse = false;
                confirmPulse = false;
                cancelPulse = false;
                consoleTogglePulse = false;
                selectHotbarSlotPulse = null;
                accumulatedLookDx = 0f;
                accumulatedLookDy = 0f;
                return frame;
            }
        }

        private bool ClearAllUnsafe()
        {
            var changed = moveForward
                || moveBack
                || moveLeft
                || moveRight
                || sprint
                || crouch
                || primaryAction
                || secondaryAction
                || holdInteract
                || autoRun
                || jumpPulse
                || reloadPulse
                || useInteractPulse
                || primaryTapPulse
                || hotbarNextPulse
                || hotbarPrevPulse
                || mouseWheelUpPulse
                || mouseWheelDownPulse
                || toggleInventoryPulse
                || toggleMapPulse
                || toggleQuestLogPulse
                || toggleFlashlightPulse
                || escapeMenuPulse
                || confirmPulse
                || cancelPulse
                || consoleTogglePulse
                || selectHotbarSlotPulse.HasValue
                || Math.Abs(accumulatedLookDx) > 0.001f
                || Math.Abs(accumulatedLookDy) > 0.001f;

            moveForward = false;
            moveBack = false;
            moveLeft = false;
            moveRight = false;
            sprint = false;
            crouch = false;
            primaryAction = false;
            secondaryAction = false;
            holdInteract = false;
            autoRun = false;
            jumpPulse = false;
            reloadPulse = false;
            useInteractPulse = false;
            primaryTapPulse = false;
            hotbarNextPulse = false;
            hotbarPrevPulse = false;
            mouseWheelUpPulse = false;
            mouseWheelDownPulse = false;
            toggleInventoryPulse = false;
            toggleMapPulse = false;
            toggleQuestLogPulse = false;
            toggleFlashlightPulse = false;
            escapeMenuPulse = false;
            confirmPulse = false;
            cancelPulse = false;
            consoleTogglePulse = false;
            selectHotbarSlotPulse = null;
            accumulatedLookDx = 0f;
            accumulatedLookDy = 0f;
            return changed;
        }

        private InputState BuildPublicStateUnsafe(bool movementLocked)
        {
            return new InputState
            {
                InputReadable = true,
                MovementLocked = movementLocked,
                MoveForward = moveForward,
                MoveBack = moveBack,
                MoveLeft = moveLeft,
                MoveRight = moveRight,
                Sprint = sprint,
                Crouch = crouch,
                PrimaryAction = primaryAction,
                SecondaryAction = secondaryAction,
                HoldInteract = holdInteract,
                AutoRun = autoRun,
                Note = "Bridge-held input state machine view."
            };
        }

        private static bool SetFlag(ref bool target, bool value)
        {
            if (target == value)
            {
                return false;
            }

            target = value;
            return true;
        }

        private static bool QueuePulse(ref bool target)
        {
            target = true;
            return true;
        }

        private static float GetRequiredFloat(Dictionary<string, object> arguments, string key)
        {
            if (!TryGetArgument(arguments, key, out var raw))
            {
                throw new BridgeCommandException(400, "bad_request", "Missing required argument: " + key);
            }

            try
            {
                return Convert.ToSingle(raw, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception exception)
            {
                throw new BridgeCommandException(400, "bad_request", "Argument '" + key + "' must be numeric: " + exception.Message);
            }
        }

        private static int GetRequiredInt(Dictionary<string, object> arguments, string key)
        {
            if (!TryGetArgument(arguments, key, out var raw))
            {
                throw new BridgeCommandException(400, "bad_request", "Missing required argument: " + key);
            }

            try
            {
                return Convert.ToInt32(raw, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception exception)
            {
                throw new BridgeCommandException(400, "bad_request", "Argument '" + key + "' must be an integer: " + exception.Message);
            }
        }

        private static bool TryGetArgument(Dictionary<string, object> arguments, string key, out object value)
        {
            if (arguments != null)
            {
                foreach (var pair in arguments)
                {
                    if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = pair.Value;
                        return true;
                    }
                }
            }

            value = null;
            return false;
        }

        private static void EnsureFinite(float value, string key)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new BridgeCommandException(400, "bad_request", "Argument '" + key + "' must be finite.");
            }
        }
    }

    public sealed class InputFrameState
    {
        public bool MovementLocked { get; set; }
        public bool MoveForward { get; set; }
        public bool MoveBack { get; set; }
        public bool MoveLeft { get; set; }
        public bool MoveRight { get; set; }
        public bool Sprint { get; set; }
        public bool Crouch { get; set; }
        public bool PrimaryAction { get; set; }
        public bool SecondaryAction { get; set; }
        public bool HoldInteract { get; set; }
        public bool AutoRun { get; set; }
        public bool JumpPulse { get; set; }
        public bool ReloadPulse { get; set; }
        public bool UseInteractPulse { get; set; }
        public bool PrimaryTapPulse { get; set; }
        public bool HotbarNextPulse { get; set; }
        public bool HotbarPrevPulse { get; set; }
        public bool MouseWheelUpPulse { get; set; }
        public bool MouseWheelDownPulse { get; set; }
        public bool ToggleInventoryPulse { get; set; }
        public bool ToggleMapPulse { get; set; }
        public bool ToggleQuestLogPulse { get; set; }
        public bool ToggleFlashlightPulse { get; set; }
        public bool EscapeMenuPulse { get; set; }
        public bool ConfirmPulse { get; set; }
        public bool CancelPulse { get; set; }
        public bool ConsoleTogglePulse { get; set; }
        public int? SelectHotbarSlotPulse { get; set; }
        public float LookDx { get; set; }
        public float LookDy { get; set; }
    }
}
