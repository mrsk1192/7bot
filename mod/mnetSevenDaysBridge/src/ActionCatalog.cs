using System;
using System.Collections.Generic;

namespace mnetSevenDaysBridge
{
    public static class ActionCatalog
    {
        private static readonly Dictionary<string, ActionDefinition> Definitions =
            new Dictionary<string, ActionDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                { "move_forward_start", Define("movement", "internal", true, true, "Start holding forward movement.") },
                { "move_forward_stop", Define("movement", "internal", true, true, "Stop holding forward movement.") },
                { "move_back_start", Define("movement", "internal", true, true, "Start holding backward movement.") },
                { "move_back_stop", Define("movement", "internal", true, true, "Stop holding backward movement.") },
                { "move_left_start", Define("movement", "internal", true, true, "Start holding left strafe.") },
                { "move_left_stop", Define("movement", "internal", true, true, "Stop holding left strafe.") },
                { "move_right_start", Define("movement", "internal", true, true, "Start holding right strafe.") },
                { "move_right_stop", Define("movement", "internal", true, true, "Stop holding right strafe.") },
                { "jump", Define("movement", "internal", true, false, "Tap jump for one update frame.") },
                { "crouch_start", Define("movement", "internal", true, true, "Start holding crouch.") },
                { "crouch_stop", Define("movement", "internal", true, true, "Stop holding crouch.") },
                { "crouch_toggle", Define("movement", "internal", true, false, "Toggle the bridge-held crouch state.") },
                { "sprint_start", Define("movement", "internal", true, true, "Start holding sprint.") },
                { "sprint_stop", Define("movement", "internal", true, true, "Stop holding sprint.") },
                { "autorun_toggle", Define("movement", "internal", true, false, "Toggle the game's auto-run flag.") },

                { "look_delta", Define("look", "internal", true, false, "Inject a relative look delta with dx and dy arguments.") },
                { "look_to", Define("look", "internal", true, false, "Set absolute yaw and pitch using the internal camera rotation path.") },
                { "turn_left", Define("look", "internal", true, false, "Inject a default left turn step.") },
                { "turn_right", Define("look", "internal", true, false, "Inject a default right turn step.") },
                { "look_up", Define("look", "internal", true, false, "Inject a default upward look step.") },
                { "look_down", Define("look", "internal", true, false, "Inject a default downward look step.") },

                { "primary_action_start", Define("combat", "os", true, true, "Hold the primary action via left mouse button down.") },
                { "primary_action_stop", Define("combat", "os", true, true, "Release the primary action via left mouse button up.") },
                { "secondary_action_start", Define("combat", "os", true, true, "Hold the secondary action via right mouse button down.") },
                { "secondary_action_stop", Define("combat", "os", true, true, "Release the secondary action via right mouse button up.") },
                { "reload", Define("combat", "os", true, false, "Tap reload via the R key.") },
                { "use_interact", Define("combat", "os", true, false, "Tap interact via the E key.") },
                { "hold_interact_start", Define("combat", "os", true, true, "Hold interact via the E key.") },
                { "hold_interact_stop", Define("combat", "os", true, true, "Release interact via the E key.") },
                { "attack_light_tap", Define("combat", "os", true, false, "Tap primary attack via a left mouse click.") },
                { "attack_heavy_start", Define("combat", "os", true, true, "Hold the heavy/alternate attack path via right mouse button down.") },
                { "attack_heavy_stop", Define("combat", "os", true, true, "Release the heavy/alternate attack path via right mouse button up.") },
                { "aim_start", Define("combat", "os", true, true, "Hold aim via right mouse button down.") },
                { "aim_stop", Define("combat", "os", true, true, "Release aim via right mouse button up.") },

                { "select_hotbar_slot", Define("hotbar", "internal", true, false, "Select a 1-based hotbar slot through the player inventory.") },
                { "hotbar_next", Define("hotbar", "internal", true, false, "Move to the next hotbar slot through the player inventory.") },
                { "hotbar_prev", Define("hotbar", "internal", true, false, "Move to the previous hotbar slot through the player inventory.") },
                { "mouse_wheel_up", Define("hotbar", "internal", true, false, "Scroll the hotbar upward through the player inventory.") },
                { "mouse_wheel_down", Define("hotbar", "internal", true, false, "Scroll the hotbar downward through the player inventory.") },

                { "toggle_inventory", Define("ui", "internal", true, false, "Toggle inventory through GUIWindowManager.") },
                { "toggle_map", Define("ui", "internal", true, false, "Toggle map through GUIWindowManager.") },
                { "toggle_quest_log", Define("ui", "internal", true, false, "Toggle quest log through GUIWindowManager.") },
                { "escape_menu", Define("ui", "internal", true, false, "Open or close the pause/menu UI through GUIWindowManager.") },
                { "confirm", Define("ui", "internal", true, false, "Confirm the current UI selection through the active XUi selection.") },
                { "cancel", Define("ui", "internal", true, false, "Cancel the current UI selection by closing the topmost open window.") },

                { "toggle_flashlight", Define("utility", "internal", true, false, "Toggle flashlight through PlayerActionsLocal.") },
                { "console_toggle", Define("utility", "internal", false, false, "Toggle the console through GameManager.SetConsoleWindowVisible.") },

                { "respawn_select_default", Define("respawn", "internal", false, false, "Trigger the default respawn option for the current death/respawn screen.") },
                { "respawn_at_bedroll", Define("respawn", "internal", false, false, "Respawn at the player's bedroll when available.") },
                { "respawn_near_bedroll", Define("respawn", "internal", false, false, "Respawn near the player's bedroll when available.") },
                { "respawn_at_random", Define("respawn", "internal", false, false, "Respawn at a random spawn position.") },
                { "respawn_confirm", Define("respawn", "internal", false, false, "Confirm the currently selected respawn option.") },
                { "respawn_cancel", Define("respawn", "internal", false, false, "Cancel or close the current respawn selection UI.") },
                { "wait_for_respawn_screen", Define("respawn", "internal", false, false, "Wait until the respawn selection screen becomes visible.") },
                { "wait_for_respawn_complete", Define("respawn", "internal", false, false, "Wait until the player has respawned and returned to a live state.") },

                { "stop_all_input", Define("safety", "hybrid", false, false, "Release held inputs and clear transient commands.") },
                { "reset_all_toggles", Define("safety", "hybrid", false, false, "Reset bridge-managed toggles to neutral.") },
                { "release_all_held_keys", Define("safety", "hybrid", false, false, "Release all bridge-managed held keys.") },
                { "emergency_neutral_state", Define("safety", "hybrid", false, false, "Force all backends back to a neutral state.") }
            };

        public static bool IsKnown(string action)
        {
            return !string.IsNullOrWhiteSpace(action) && Definitions.ContainsKey(action);
        }

        public static ActionDefinition Get(string action)
        {
            if (!Definitions.TryGetValue(action ?? string.Empty, out var definition))
            {
                throw new BridgeCommandException(400, "unknown_action", "Unknown action: " + action);
            }

            return definition;
        }

        public static CapabilitySet BuildCapabilities(string activeBackend, IList<string> availableBackends)
        {
            var actions = new Dictionary<string, ActionCapabilityInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in Definitions)
            {
                actions[pair.Key] = new ActionCapabilityInfo
                {
                    Supported = pair.Value.Supported,
                    Note = pair.Value.Note,
                    Category = pair.Value.Category,
                    Backend = pair.Value.Supported ? pair.Value.Backend : "unsupported",
                    Idempotent = pair.Value.Idempotent
                };
            }

            return new CapabilitySet
            {
                Phase = "phase3",
                ActiveBackend = activeBackend,
                AvailableBackends = availableBackends,
                Commands = new Dictionary<string, CapabilityInfo>
                {
                    { "ping", new CapabilityInfo { Supported = true, Note = "Connectivity check." } },
                    { "get_version", new CapabilityInfo { Supported = true, Note = "Returns bridge version and backend routing mode." } },
                    { "get_capabilities", new CapabilityInfo { Supported = true, Note = "Returns Phase 3 command, action, and respawn capabilities." } },
                    { "get_state", new CapabilityInfo { Supported = true, Note = "Returns current state including bridge-held input state." } },
                    { "get_player_position", new CapabilityInfo { Supported = true, Note = "Returns player position." } },
                    { "get_player_rotation", new CapabilityInfo { Supported = true, Note = "Returns player rotation." } },
                    { "get_logs_tail", new CapabilityInfo { Supported = true, Note = "Returns the in-memory log tail." } }
                },
                Actions = actions,
                Respawn = new Dictionary<string, CapabilityInfo>
                {
                    { "respawn_select_default", new CapabilityInfo { Supported = true, Note = "Default respawn is routed through XUiC_SpawnSelectionWindow." } },
                    { "respawn_at_bedroll", new CapabilityInfo { Supported = true, Note = "Bedroll respawn is supported when the current death UI reports a bedroll option." } },
                    { "respawn_near_bedroll", new CapabilityInfo { Supported = true, Note = "Near-bedroll respawn is supported when the current death UI reports a bedroll option." } },
                    { "respawn_at_random", new CapabilityInfo { Supported = true, Note = "Random respawn is supported through the spawn selection controller." } },
                    { "respawn_confirm", new CapabilityInfo { Supported = true, Note = "Confirm uses the currently selected respawn method or falls back to the default option." } },
                    { "respawn_cancel", new CapabilityInfo { Supported = true, Note = "Respawn cancel closes the spawn selection window when available." } },
                    { "wait_for_respawn_screen", new CapabilityInfo { Supported = true, Note = "Waits on the tracked respawn-screen visibility state." } },
                    { "wait_for_respawn_complete", new CapabilityInfo { Supported = true, Note = "Waits on the tracked respawn completion state." } },
                    { "respawn_state_detection", new CapabilityInfo { Supported = true, Note = "Death/respawn screen detection is tracked from UI controllers and player death state." } }
                },
                Features = new Dictionary<string, CapabilityInfo>
                {
                    { "look_to", new CapabilityInfo { Supported = true, Note = "Absolute look is implemented through the internal camera rotation path." } },
                    { "approximate_absolute_look", new CapabilityInfo { Supported = false, Note = "The current build uses direct camera rotation instead of approximation." } },
                    { "inventory_open_close", new CapabilityInfo { Supported = false, Note = "Only toggle_inventory is guaranteed; strict open/close remains unavailable." } },
                    { "map_open_close", new CapabilityInfo { Supported = false, Note = "Only toggle_map is guaranteed; strict open/close remains unavailable." } },
                    { "quest_log_open_close", new CapabilityInfo { Supported = false, Note = "Only toggle_quest_log is guaranteed; strict open/close remains unavailable." } },
                    { "debug_commands", new CapabilityInfo { Supported = false, Note = "Debug commands remain intentionally disabled." } }
                }
            };
        }

        public static bool IsRespawnAction(string action)
        {
            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "respawn_select_default":
                case "respawn_at_bedroll":
                case "respawn_near_bedroll":
                case "respawn_at_random":
                case "respawn_confirm":
                case "respawn_cancel":
                case "wait_for_respawn_screen":
                case "wait_for_respawn_complete":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsRespawnWaitAction(string action)
        {
            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "wait_for_respawn_screen":
                case "wait_for_respawn_complete":
                    return true;
                default:
                    return false;
            }
        }

        private static ActionDefinition Define(string category, string backend, bool requiresPlayer, bool idempotent, string note)
        {
            return new ActionDefinition
            {
                Category = category,
                Backend = backend,
                Supported = true,
                RequiresPlayer = requiresPlayer,
                Idempotent = idempotent,
                Note = note
            };
        }
    }

    public sealed class ActionDefinition
    {
        public string Category { get; set; }

        public string Backend { get; set; }

        public bool Supported { get; set; }

        public bool RequiresPlayer { get; set; }

        public bool Idempotent { get; set; }

        public string Note { get; set; }
    }
}
