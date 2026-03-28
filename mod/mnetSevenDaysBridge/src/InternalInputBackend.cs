using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace mnetSevenDaysBridge
{
    public sealed class InternalInputBackend : IInputBackend
    {
        private static readonly string[] QuestLogWindowNames =
        {
            "windowQuestList",
            "windowQuestDescription",
            "windowQuestObjectives",
            "windowQuestRewards",
            "windowQuestSharedList"
        };

        private readonly object syncRoot = new object();
        private static readonly object actionTickSyncRoot = new object();
        private static ulong lastInjectedActionTick;
        private readonly BridgeLogger logger;
        private InputFrameState latestFrameState = new InputFrameState();
        private bool? lastAutoRun;
        private bool? lastPrimaryAction;
        private bool? lastSecondaryAction;
        private bool? lastHoldInteract;
        private UiState lastUiState = CreateUnavailableUiState("UI state has not been sampled yet.");

        public InternalInputBackend(BridgeLogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name
        {
            get { return "internal_input"; }
        }

        public bool IsAvailable
        {
            get { return true; }
        }

        public string AvailabilityNote
        {
            get { return "Uses EntityPlayerLocal and PlayerActionsLocal for internal input injection."; }
        }

        public bool TryGetPlayer(out EntityPlayerLocal player, out string reason)
        {
            player = null;
            reason = null;

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                reason = "GameManager.Instance was null.";
                return false;
            }

            if (gameManager.World == null)
            {
                reason = "Game world was null.";
                return false;
            }

            player = gameManager.World.GetPrimaryPlayer() as EntityPlayerLocal;
            if (player == null)
            {
                reason = "Primary local player was unavailable.";
                return false;
            }

            return true;
        }

        public void Apply(InputFrameState frameState)
        {
            if (frameState == null)
            {
                throw new ArgumentNullException(nameof(frameState));
            }

            lock (syncRoot)
            {
                latestFrameState = CloneFrameState(frameState);
            }

            if (!TryGetPlayer(out var player, out var reason))
            {
                lastUiState = CreateUnavailableUiState(reason);
                return;
            }

            ApplyMovementDirect(player, frameState);

            if (!lastAutoRun.HasValue || lastAutoRun.Value != frameState.AutoRun)
            {
                player.EnableAutoMove(frameState.AutoRun);
                lastAutoRun = frameState.AutoRun;
            }

            lastUiState = BuildUiState(player);
        }

        public void ForceNeutralState()
        {
            lock (syncRoot)
            {
                latestFrameState = new InputFrameState();
            }

            if (!TryGetPlayer(out var player, out _))
            {
                lastAutoRun = false;
                return;
            }

            if (player.movementInput != null)
            {
                player.movementInput.moveForward = 0f;
                player.movementInput.moveStrafe = 0f;
                player.movementInput.jump = false;
                player.movementInput.running = false;
                player.movementInput.sneak = false;
            }

            try
            {
                player.OnValue_InputMoveVector = Vector2.zero;
                player.OnValue_InputSmoothLook = Vector2.zero;
            }
            catch
            {
            }

            try
            {
                player.SetMoveState(EntityPlayerLocal.MoveState.None, false);
            }
            catch
            {
            }

            try
            {
                player.SetMoveStateToDefault();
            }
            catch
            {
            }

            try
            {
                InvokeOptional(player, "ClearMovementInputs");
            }
            catch
            {
            }

            try
            {
                player.EnableAutoMove(false);
            }
            catch
            {
            }

            lastAutoRun = false;
            ReleasePlayerActions(player);
            try
            {
                lastUiState = BuildUiState(player);
            }
            catch
            {
                lastUiState = CreateUnavailableUiState("UI state was unavailable during neutral-state teardown.");
            }
        }

        public UiState GetUiState()
        {
            return lastUiState;
        }

        public IList<string> GetAvailableBackendNames()
        {
            return new List<string> { Name };
        }

        private void ApplyMovementDirect(EntityPlayerLocal player, InputFrameState frameState)
        {
            var moveX = (frameState.MoveRight ? 1f : 0f) - (frameState.MoveLeft ? 1f : 0f);
            var moveY = (frameState.MoveForward ? 1f : 0f) - (frameState.MoveBack ? 1f : 0f);
            if (Math.Abs(moveY) < 0.001f && frameState.AutoRun)
            {
                moveY = 1f;
            }

            var moveVector = new Vector2(moveX, moveY);
            if (moveVector.sqrMagnitude > 1f)
            {
                moveVector.Normalize();
                moveX = moveVector.x;
                moveY = moveVector.y;
            }

            player.OnValue_InputMoveVector = moveVector;
            if (player.movementInput != null)
            {
                player.movementInput.moveStrafe = moveX;
                player.movementInput.moveForward = moveY;
                player.movementInput.running = frameState.Sprint;
                player.movementInput.down = frameState.Crouch;
                player.movementInput.jump = frameState.JumpPulse;
            }

            if (frameState.Crouch)
            {
                player.SetMoveState(EntityPlayerLocal.MoveState.Crouch, true);
            }
            else
            {
                player.SetMoveState(EntityPlayerLocal.MoveState.Crouch, false);
            }

            if (Math.Abs(moveX) > 0.001f || Math.Abs(moveY) > 0.001f || frameState.JumpPulse)
            {
                player.MoveEntityHeaded(new Vector3(moveX, 0f, moveY), false);
                return;
            }

            InvokeOptional(player, "ClearMovementInputs");
        }

        public void OnBeforeMoveByInput(EntityPlayerLocal player)
        {
            if (player == null)
            {
                return;
            }

            InputFrameState frameState;
            lock (syncRoot)
            {
                frameState = CloneFrameState(latestFrameState);
            }

            var moveX = (frameState.MoveRight ? 1f : 0f) - (frameState.MoveLeft ? 1f : 0f);
            var moveY = (frameState.MoveForward ? 1f : 0f) - (frameState.MoveBack ? 1f : 0f);
            if (Math.Abs(moveY) < 0.001f && frameState.AutoRun)
            {
                moveY = 1f;
            }

            if (player.movementInput != null)
            {
                player.movementInput.moveStrafe = moveX;
                player.movementInput.moveForward = moveY;
                player.movementInput.running = frameState.Sprint;
                player.movementInput.down = frameState.Crouch;
                player.movementInput.downToggle = false;
                player.movementInput.jump = frameState.JumpPulse;
                player.movementInput.rotation.x += frameState.LookDy;
                player.movementInput.rotation.y += frameState.LookDx;
            }

            var fpCamera = player.vp_FPCamera;
            if (fpCamera != null && (Math.Abs(frameState.LookDx) > 0.001f || Math.Abs(frameState.LookDy) > 0.001f))
            {
                var nextPitch = fpCamera.Pitch + frameState.LookDy;
                var nextYaw = fpCamera.Yaw + frameState.LookDx;
                fpCamera.SetRotation(new Vector2(nextPitch, nextYaw), true);
                player.OnValue_InputSmoothLook = new Vector2(frameState.LookDx, frameState.LookDy);
            }

            ApplyPlayerActions(player, frameState);

            if (!frameState.MoveForward
                && !frameState.MoveBack
                && !frameState.MoveLeft
                && !frameState.MoveRight
                && !frameState.AutoRun
                && !frameState.Crouch
                && !frameState.Sprint
                && !frameState.JumpPulse
                && Math.Abs(frameState.LookDx) < 0.001f
                && Math.Abs(frameState.LookDy) < 0.001f)
            {
                InvokeOptional(player, "ClearMovementInputs");
            }
        }

        public void SetAbsoluteLook(float yaw, float pitch)
        {
            if (!TryGetPlayer(out var player, out var reason))
            {
                throw new BridgeCommandException(409, "player_unavailable", reason);
            }

            var fpCamera = player.vp_FPCamera;
            if (fpCamera == null)
            {
                throw new BridgeCommandException(409, "camera_unavailable", "vp_FPCamera was unavailable for absolute look.");
            }

            fpCamera.SetRotation(new Vector2(pitch, yaw), true);
            player.OnValue_InputSmoothLook = Vector2.zero;
            lastUiState = BuildUiState(player);
        }

        public void ExecuteImmediateAction(string action, Dictionary<string, object> arguments)
        {
            if (!TryGetPlayer(out var player, out var reason))
            {
                throw new BridgeCommandException(409, "player_unavailable", reason);
            }

            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "select_hotbar_slot":
                    SetHotbarSlot(player, GetRequiredSlot(arguments) - 1);
                    break;
                case "hotbar_next":
                case "mouse_wheel_down":
                    MoveHotbar(player, 1);
                    break;
                case "hotbar_prev":
                case "mouse_wheel_up":
                    MoveHotbar(player, -1);
                    break;
                case "toggle_inventory":
                    ToggleWindow(player, "inventory", "windowbackpack", "backpack", "looting");
                    break;
                case "toggle_map":
                    ToggleWindow(player, "windowmap", "map");
                    break;
                case "toggle_quest_log":
                    ToggleQuestLog(player);
                    break;
                case "escape_menu":
                    ToggleWindow(player, "ingamemenu", "mainmenu");
                    break;
                case "confirm":
                    ConfirmCurrentSelection(player);
                    break;
                case "cancel":
                    CloseFirstOpenWindow(
                        player,
                        "ingamemenu",
                        "mainmenu",
                        "windowQuestRewards",
                        "windowQuestObjectives",
                        "windowQuestDescription",
                        "windowQuestSharedList",
                        "windowQuestList",
                        "windowmap",
                        "windowbackpack",
                        "backpack",
                        "looting");
                    break;
                case "toggle_flashlight":
                    ToggleFlashlight(player);
                    break;
                case "console_toggle":
                    ToggleConsole();
                    break;
                default:
                    throw new BridgeCommandException(400, "unsupported_action", "Internal backend cannot execute action: " + action);
            }

            lastUiState = BuildUiState(player);
        }

        private UiState BuildUiState(EntityPlayerLocal player)
        {
            var ui = player.PlayerUI;
            if (ui == null || ui.windowManager == null)
            {
                return CreateUnavailableUiState("LocalPlayerUI was unavailable.");
            }

            var windowManager = ui.windowManager;
            var inventoryOpen = TryWindowState(windowManager, "inventory", "windowbackpack", "backpack", "looting");
            var mapOpen = TryWindowState(windowManager, "map", "windowmap");
            var questLogOpen = TryQuestLogWindowState(windowManager);
            var consoleOpen = TryConsoleState();
            var pauseMenuOpen = TryWindowState(windowManager, "mainmenu", "ingamemenu");
            var menuOpen = windowManager.IsCursorWindowOpen()
                || windowManager.IsModalWindowOpen()
                || IsOpen(inventoryOpen)
                || IsOpen(mapOpen)
                || IsOpen(questLogOpen)
                || IsOpen(consoleOpen)
                || IsOpen(pauseMenuOpen);

            return new UiState
            {
                HudAvailable = windowManager.IsHUDEnabled(),
                MenuOpen = menuOpen,
                InventoryOpen = inventoryOpen,
                MapOpen = mapOpen,
                QuestLogOpen = questLogOpen,
                ConsoleOpen = consoleOpen,
                PauseMenuOpen = pauseMenuOpen,
                Note = "Quest UI detection uses grouped quest windows. Console state uses GameManager GUI console when available."
            };
        }

        private void SetHotbarSlot(EntityPlayerLocal player, int zeroBasedSlot)
        {
            var inventory = ReadMember(player, "inventory");
            if (inventory == null)
            {
                throw new BridgeCommandException(409, "inventory_unavailable", "Player inventory was unavailable.");
            }

            var publicSlots = TryReadInt(inventory, "PUBLIC_SLOTS") ?? 10;
            if (zeroBasedSlot < 0 || zeroBasedSlot >= publicSlots)
            {
                throw new BridgeCommandException(400, "bad_request", "Requested hotbar slot is out of range.");
            }

            InvokeRequired(inventory, "SetHoldingItemIdxNoHolsterTime", zeroBasedSlot);
            InvokeOptional(inventory, "SetHoldingItemIdx", zeroBasedSlot);
            InvokeOptional(inventory, "ForceHoldingItemUpdate");
            InvokeOptional(player, "callInventoryChanged");
        }

        private void MoveHotbar(EntityPlayerLocal player, int delta)
        {
            var inventory = ReadMember(player, "inventory");
            if (inventory == null)
            {
                throw new BridgeCommandException(409, "inventory_unavailable", "Player inventory was unavailable.");
            }

            var current = TryReadInt(inventory, "holdingItemIdx", "HoldingItemIdx") ?? 0;
            var publicSlots = TryReadInt(inventory, "PUBLIC_SLOTS") ?? 10;
            var next = current + delta;
            while (next < 0)
            {
                next += publicSlots;
            }

            if (publicSlots > 0)
            {
                next %= publicSlots;
            }

            SetHotbarSlot(player, next);
        }

        private void ToggleWindow(EntityPlayerLocal player, params string[] candidates)
        {
            var windowManager = GetWindowManager(player);
            foreach (var candidate in candidates)
            {
                if (!HasWindow(windowManager, candidate))
                {
                    continue;
                }

                if (IsWindowOpen(windowManager, candidate))
                {
                    InvokeRequired(windowManager, "CloseIfOpen", candidate);
                }
                else
                {
                    InvokeRequired(windowManager, "OpenIfNotOpen", candidate, false, false, false);
                }

                return;
            }

            throw new BridgeCommandException(409, "window_unavailable", "No matching UI window was found for: " + string.Join(", ", candidates));
        }

        private void ToggleQuestLog(EntityPlayerLocal player)
        {
            var windowManager = GetWindowManager(player);
            var questWindowGroup = GetQuestWindowGroup(player);

            if (questWindowGroup != null)
            {
                var isOpen = TryReadBool(questWindowGroup, "isShowing")
                    ?? TryReadBool(ReadMember(questWindowGroup, "Controller"), "IsOpen")
                    ?? false;

                if (isOpen)
                {
                    InvokeOptional(questWindowGroup, "OnClose");
                    SetMember(ReadMember(questWindowGroup, "Controller"), "IsOpen", false);
                    CloseQuestWindows(windowManager);
                    return;
                }

                OpenQuestWindow(windowManager, "windowQuestList");
                OpenQuestWindow(windowManager, "windowQuestDescription");
                OpenQuestWindow(windowManager, "windowQuestObjectives");
                InvokeOptional(questWindowGroup, "OnOpen");
                SetMember(ReadMember(questWindowGroup, "Controller"), "IsOpen", true);
                return;
            }

            var openedAny = false;
            foreach (var name in QuestLogWindowNames)
            {
                if (TryIsWindowOpen(windowManager, name))
                {
                    CloseQuestWindows(windowManager);
                    return;
                }
            }

            openedAny |= TryOpenQuestWindow(windowManager, "windowQuestList");
            openedAny |= TryOpenQuestWindow(windowManager, "windowQuestDescription");
            openedAny |= TryOpenQuestWindow(windowManager, "windowQuestObjectives");

            if (!openedAny)
            {
                throw new BridgeCommandException(409, "window_unavailable", "No matching quest log windows were found.");
            }
        }

        private void CloseFirstOpenWindow(EntityPlayerLocal player, params string[] candidates)
        {
            var windowManager = GetWindowManager(player);
            foreach (var candidate in candidates)
            {
                if (HasWindow(windowManager, candidate) && IsWindowOpen(windowManager, candidate))
                {
                    InvokeRequired(windowManager, "CloseIfOpen", candidate);
                    return;
                }
            }
        }

        private object GetWindowManager(EntityPlayerLocal player)
        {
            var ui = player.PlayerUI;
            if (ui == null || ui.windowManager == null)
            {
                throw new BridgeCommandException(409, "ui_unavailable", "LocalPlayerUI or GUIWindowManager was unavailable.");
            }

            return ui.windowManager;
        }

        private void OpenQuestWindow(object windowManager, string windowName)
        {
            InvokeRequired(windowManager, "Open", windowName, false, false, false);
        }

        private bool TryOpenQuestWindow(object windowManager, string windowName)
        {
            try
            {
                OpenQuestWindow(windowManager, windowName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void CloseQuestWindows(object windowManager)
        {
            foreach (var name in QuestLogWindowNames)
            {
                try
                {
                    InvokeRequired(windowManager, "CloseIfOpen", name);
                }
                catch
                {
                }
            }
        }

        private static bool? TryWindowState(GUIWindowManager windowManager, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                try
                {
                    if (windowManager.HasWindow(candidate))
                    {
                        return windowManager.IsWindowOpen(candidate);
                    }
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static bool? TryQuestLogWindowState(GUIWindowManager windowManager)
        {
            var sawWindow = false;
            foreach (var name in QuestLogWindowNames)
            {
                try
                {
                    if (!windowManager.HasWindow(name))
                    {
                        continue;
                    }

                    sawWindow = true;
                    if (windowManager.IsWindowOpen(name))
                    {
                        return true;
                    }
                }
                catch
                {
                    return null;
                }
            }

            return sawWindow ? false : (bool?)null;
        }

        private XUiWindowGroup GetQuestWindowGroup(EntityPlayerLocal player)
        {
            var xui = GetXui(player);
            if (xui == null)
            {
                return null;
            }

            var questGroup = InvokeOptional(xui, "GetWindowGroupById", "Quests") as XUiWindowGroup;
            if (questGroup != null)
            {
                return questGroup;
            }

            var controller = InvokeOptional(xui, "FindWindowGroupByName", "Quests");
            return ReadMember(controller, "WindowGroup") as XUiWindowGroup ?? ReadMember(controller, "windowGroup") as XUiWindowGroup;
        }

        private XUi GetXui(EntityPlayerLocal player)
        {
            var ui = player.PlayerUI;
            if (ui == null)
            {
                return null;
            }

            var nguiWindowManager = ReadMember(ui, "nguiWindowManager") ?? ReadMember(ui, "mNGUIWindowManager");
            if (nguiWindowManager == null)
            {
                return null;
            }

            return InvokeOptional(nguiWindowManager, "GetComponentInChildren", typeof(XUi), true) as XUi
                ?? InvokeOptional(nguiWindowManager, "GetComponentInChildren", typeof(XUi)) as XUi;
        }

        private static bool? TryConsoleState()
        {
            try
            {
                var gameManager = GameManager.Instance;
                if (gameManager == null)
                {
                    return null;
                }

                var guiConsole = ReadMember(gameManager, "m_GUIConsole");
                if (guiConsole == null)
                {
                    return null;
                }

                return TryReadBool(guiConsole, "isShowing", "isInputActive", "shouldOpen", "openWindowOnEsc");
            }
            catch
            {
                return null;
            }
        }

        private void ToggleConsole()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                throw new BridgeCommandException(409, "game_manager_unavailable", "GameManager.Instance was unavailable.");
            }

            var nextState = !(TryConsoleState() ?? false);
            InvokeRequired(gameManager, "SetConsoleWindowVisible", nextState);
        }

        private void ToggleFlashlight(EntityPlayerLocal player)
        {
            var playerInput = ReadMember(player, "playerInput") as PlayerActionsLocal;
            if (playerInput == null || playerInput.ToggleFlashlight == null)
            {
                throw new BridgeCommandException(409, "input_unavailable", "PlayerActionsLocal.ToggleFlashlight was unavailable.");
            }

            PulseAction(playerInput.ToggleFlashlight);
        }

        private void ApplyPlayerActions(EntityPlayerLocal player, InputFrameState frameState)
        {
            var playerInput = ReadMember(player, "playerInput") as PlayerActionsLocal;
            if (playerInput == null)
            {
                return;
            }

            SetActionState(playerInput.Primary, frameState.PrimaryAction, ref lastPrimaryAction);
            SetActionState(playerInput.Secondary, frameState.SecondaryAction, ref lastSecondaryAction);
            SetActionState(playerInput.Activate, frameState.HoldInteract, ref lastHoldInteract);

            if (frameState.ReloadPulse)
            {
                PulseAction(playerInput.Reload);
            }

            if (frameState.UseInteractPulse)
            {
                PulseAction(playerInput.Activate);
            }

            if (frameState.PrimaryTapPulse)
            {
                PulseAction(playerInput.Primary);
            }
        }

        private void ReleasePlayerActions(EntityPlayerLocal player)
        {
            var playerInput = ReadMember(player, "playerInput") as PlayerActionsLocal;
            if (playerInput == null)
            {
                lastPrimaryAction = false;
                lastSecondaryAction = false;
                lastHoldInteract = false;
                return;
            }

            SetActionState(playerInput.Primary, false, ref lastPrimaryAction);
            SetActionState(playerInput.Secondary, false, ref lastSecondaryAction);
            SetActionState(playerInput.Activate, false, ref lastHoldInteract);
        }

        private void ConfirmCurrentSelection(EntityPlayerLocal player)
        {
            var xui = GetXui(player);
            if (xui == null)
            {
                throw new BridgeCommandException(409, "xui_unavailable", "XUi was unavailable.");
            }

            var selectedEntry = ReadMember(xui, "currentSelectedEntry");
            if (selectedEntry == null)
            {
                selectedEntry = FindDefaultSelectableEntry(xui);
            }

            if (selectedEntry == null)
            {
                throw new BridgeCommandException(409, "confirm_unavailable", "No selected or default XUi entry was available to confirm.");
            }

            InvokeRequired(selectedEntry, "Pressed", 0);
        }

        private object FindDefaultSelectableEntry(XUi xui)
        {
            var windowGroups = ReadMember(xui, "WindowGroups") as System.Collections.IEnumerable;
            if (windowGroups == null)
            {
                return null;
            }

            foreach (var group in windowGroups)
            {
                var isShowing = TryReadBool(group, "isShowing") ?? false;
                if (!isShowing)
                {
                    continue;
                }

                var defaultSelectedView = ReadMember(group, "defaultSelectedView") as string;
                var controller = ReadMember(group, "Controller");
                if (controller == null || string.IsNullOrWhiteSpace(defaultSelectedView))
                {
                    continue;
                }

                var entry = InvokeOptional(controller, "GetChildById", defaultSelectedView);
                if (entry != null)
                {
                    return entry;
                }
            }

            return null;
        }

        private static void PulseAction(InControl.PlayerAction action)
        {
            if (action == null)
            {
                return;
            }

            var deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            var tick = ReserveNextActionTick(action);
            action.CommitWithState(true, tick, deltaTime);
            action.CommitWithState(false, ReserveNextActionTick(action, tick), deltaTime);
        }

        private static void SetActionState(InControl.PlayerAction action, bool pressed, ref bool? lastState)
        {
            if (action == null)
            {
                lastState = pressed;
                return;
            }

            if (lastState.HasValue && lastState.Value == pressed)
            {
                return;
            }

            var deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            var tick = ReserveNextActionTick(action);
            action.CommitWithState(pressed, tick, deltaTime);
            lastState = pressed;
        }

        private static ulong ReserveNextActionTick(InControl.PlayerAction action, ulong minimumExclusive = 0UL)
        {
            lock (actionTickSyncRoot)
            {
                var currentTick = InControl.InputManager.CurrentTick;
                var actionTick = action.UpdateTick;
                var baseline = Math.Max(currentTick, actionTick);
                baseline = Math.Max(baseline, lastInjectedActionTick);
                baseline = Math.Max(baseline, minimumExclusive);
                var nextTick = baseline + 1UL;
                lastInjectedActionTick = nextTick;
                return nextTick;
            }
        }

        private static bool HasWindow(object windowManager, string name)
        {
            var result = InvokeOptional(windowManager, "HasWindow", name);
            return result is bool value && value;
        }

        private static bool TryIsWindowOpen(object windowManager, string name)
        {
            try
            {
                var result = InvokeOptional(windowManager, "IsWindowOpen", name);
                return result is bool value && value;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWindowOpen(object windowManager, string name)
        {
            var result = InvokeOptional(windowManager, "IsWindowOpen", name);
            return result is bool value && value;
        }

        private static UiState CreateUnavailableUiState(string reason)
        {
            return new UiState
            {
                HudAvailable = false,
                MenuOpen = false,
                InventoryOpen = null,
                MapOpen = null,
                QuestLogOpen = null,
                ConsoleOpen = null,
                PauseMenuOpen = null,
                Note = reason
            };
        }

        private static InputFrameState CloneFrameState(InputFrameState source)
        {
            return new InputFrameState
            {
                MovementLocked = source.MovementLocked,
                MoveForward = source.MoveForward,
                MoveBack = source.MoveBack,
                MoveLeft = source.MoveLeft,
                MoveRight = source.MoveRight,
                Sprint = source.Sprint,
                Crouch = source.Crouch,
                PrimaryAction = source.PrimaryAction,
                SecondaryAction = source.SecondaryAction,
                HoldInteract = source.HoldInteract,
                AutoRun = source.AutoRun,
                JumpPulse = source.JumpPulse,
                ReloadPulse = source.ReloadPulse,
                UseInteractPulse = source.UseInteractPulse,
                PrimaryTapPulse = source.PrimaryTapPulse,
                HotbarNextPulse = source.HotbarNextPulse,
                HotbarPrevPulse = source.HotbarPrevPulse,
                MouseWheelUpPulse = source.MouseWheelUpPulse,
                MouseWheelDownPulse = source.MouseWheelDownPulse,
                ToggleInventoryPulse = source.ToggleInventoryPulse,
                ToggleMapPulse = source.ToggleMapPulse,
                ToggleQuestLogPulse = source.ToggleQuestLogPulse,
                ToggleFlashlightPulse = source.ToggleFlashlightPulse,
                EscapeMenuPulse = source.EscapeMenuPulse,
                ConfirmPulse = source.ConfirmPulse,
                CancelPulse = source.CancelPulse,
                ConsoleTogglePulse = source.ConsoleTogglePulse,
                SelectHotbarSlotPulse = source.SelectHotbarSlotPulse,
                LookDx = source.LookDx,
                LookDy = source.LookDy
            };
        }

        private static int GetRequiredSlot(Dictionary<string, object> arguments)
        {
            if (arguments != null)
            {
                foreach (var pair in arguments)
                {
                    if (string.Equals(pair.Key, "slot", StringComparison.OrdinalIgnoreCase))
                    {
                        return Convert.ToInt32(pair.Value, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }

            throw new BridgeCommandException(400, "bad_request", "Missing required argument: slot");
        }

        private static bool? TryReadBool(object target, params string[] memberNames)
        {
            if (target == null)
            {
                return null;
            }

            foreach (var memberName in memberNames)
            {
                var raw = ReadMember(target, memberName) ?? InvokeOptional(target, memberName);
                if (raw == null)
                {
                    continue;
                }

                if (raw is bool boolValue)
                {
                    return boolValue;
                }

                if (bool.TryParse(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture), out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static bool IsOpen(bool? value)
        {
            return value.HasValue && value.Value;
        }

        private static int? TryReadInt(object target, params string[] memberNames)
        {
            if (target == null)
            {
                return null;
            }

            foreach (var memberName in memberNames)
            {
                var raw = ReadMember(target, memberName) ?? InvokeOptional(target, memberName);
                if (raw == null)
                {
                    continue;
                }

                if (raw is int intValue)
                {
                    return intValue;
                }

                if (int.TryParse(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture), out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static object ReadMember(object target, string name)
        {
            if (target == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var type = target.GetType();
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return property.GetValue(target, null);
            }

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(target);
        }

        private static bool SetMember(object target, string name, object value)
        {
            if (target == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var type = target.GetType();
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value, null);
                return true;
            }

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
                return true;
            }

            return false;
        }

        private static object InvokeOptional(object target, string methodName, params object[] args)
        {
            if (target == null || string.IsNullOrWhiteSpace(methodName))
            {
                return null;
            }

            foreach (var method in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                return method.Invoke(target, args);
            }

            return null;
        }

        private static object InvokeRequired(object target, string methodName, params object[] args)
        {
            var result = InvokeOptional(target, methodName, args);
            if (result == null && !MethodExists(target, methodName, args.Length))
            {
                throw new BridgeCommandException(409, "method_unavailable", "Required method was unavailable: " + methodName);
            }

            return result;
        }

        private static bool MethodExists(object target, string methodName, int parameterCount)
        {
            if (target == null)
            {
                return false;
            }

            foreach (var method in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (string.Equals(method.Name, methodName, StringComparison.Ordinal) && method.GetParameters().Length == parameterCount)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
