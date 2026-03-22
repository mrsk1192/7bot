using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace mnetSevenDaysBridge
{
    public sealed class OSInputBackend : IInputBackend
    {
        private const int WmKeyDown = 0x0100;
        private const int WmKeyUp = 0x0101;
        private const int WmSysKeyDown = 0x0104;
        private const int WmSysKeyUp = 0x0105;
        private const int WmMouseMove = 0x0200;
        private const int WmLButtonDown = 0x0201;
        private const int WmLButtonUp = 0x0202;
        private const int WmRButtonDown = 0x0204;
        private const int WmRButtonUp = 0x0205;
        private const int WmMouseWheel = 0x020A;

        private const int MkLButton = 0x0001;
        private const int MkRButton = 0x0002;
        private const int WheelDelta = 120;

        private const ushort Vk0 = 0x30;
        private const ushort Vk1 = 0x31;
        private const ushort VkTab = 0x09;
        private const ushort VkReturn = 0x0D;
        private const ushort VkEscape = 0x1B;
        private const ushort VkM = 0x4D;
        private const ushort VkO = 0x4F;
        private const ushort VkE = 0x45;
        private const ushort VkR = 0x52;
        private const ushort VkF = 0x46;
        private const ushort VkF1 = 0x70;

        private readonly object syncRoot = new object();
        private readonly BridgeLogger logger;
        private readonly BridgeConfig config;
        private bool primaryHeld;
        private bool secondaryHeld;
        private bool interactHeld;
        private UiState lastUiState;

        public OSInputBackend(BridgeLogger logger, BridgeConfig config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            lastUiState = CreateUiState("Window-scoped OS input backend is ready.");
        }

        public string Name
        {
            get { return "os_input"; }
        }

        public bool IsAvailable
        {
            get { return config.EnableOsInputBackend && Environment.OSVersion.Platform == PlatformID.Win32NT; }
        }

        public string AvailabilityNote
        {
            get
            {
                return IsAvailable
                    ? "Window-scoped Win32 message injection is enabled for combat, hotbar, and UI actions."
                    : "Window-scoped Win32 message injection is disabled by configuration or unavailable on this platform.";
            }
        }

        public bool TryGetPlayer(out EntityPlayerLocal player, out string reason)
        {
            player = null;
            reason = "OS input backend does not expose an EntityPlayerLocal instance.";
            return false;
        }

        public void Apply(InputFrameState frameState)
        {
        }

        public void ForceNeutralState()
        {
            lock (syncRoot)
            {
                if (!IsAvailable)
                {
                    return;
                }

                var windowHandle = RequireGameWindow();
                if (primaryHeld)
                {
                    PostMouseButton(windowHandle, false, false);
                    primaryHeld = false;
                }

                if (secondaryHeld)
                {
                    PostMouseButton(windowHandle, true, false);
                    secondaryHeld = false;
                }

                if (interactHeld)
                {
                    PostKey(windowHandle, VkE, false);
                    interactHeld = false;
                }

                lastUiState = CreateUiState("Window-scoped held inputs were released.");
            }
        }

        public UiState GetUiState()
        {
            return lastUiState;
        }

        public IList<string> GetAvailableBackendNames()
        {
            return IsAvailable ? new List<string> { Name } : new List<string>();
        }

        public void ExecuteAction(string action, Dictionary<string, object> arguments)
        {
            if (!IsAvailable)
            {
                throw new BridgeCommandException(503, "os_backend_unavailable", AvailabilityNote);
            }

            lock (syncRoot)
            {
                var windowHandle = RequireGameWindow();
                var clientPoint = GetClientCenter(windowHandle);

                switch ((action ?? string.Empty).Trim().ToLowerInvariant())
                {
                    case "primary_action_start":
                        SetPrimaryHeld(windowHandle, true);
                        break;
                    case "primary_action_stop":
                        SetPrimaryHeld(windowHandle, false);
                        break;
                    case "secondary_action_start":
                    case "aim_start":
                    case "attack_heavy_start":
                        SetSecondaryHeld(windowHandle, true);
                        break;
                    case "secondary_action_stop":
                    case "aim_stop":
                    case "attack_heavy_stop":
                        SetSecondaryHeld(windowHandle, false);
                        break;
                    case "reload":
                        TapKey(windowHandle, VkR);
                        break;
                    case "use_interact":
                        TapKey(windowHandle, VkE);
                        break;
                    case "hold_interact_start":
                        SetInteractHeld(windowHandle, true);
                        break;
                    case "hold_interact_stop":
                        SetInteractHeld(windowHandle, false);
                        break;
                    case "attack_light_tap":
                        PostMouseMove(windowHandle, clientPoint);
                        PostMouseButton(windowHandle, false, true);
                        PostMouseButton(windowHandle, false, false);
                        break;
                    case "select_hotbar_slot":
                        TapKey(windowHandle, GetHotbarVirtualKey(GetRequiredInt(arguments, "slot")));
                        break;
                    case "hotbar_next":
                    case "mouse_wheel_down":
                        PostMouseWheel(windowHandle, clientPoint, -WheelDelta);
                        break;
                    case "hotbar_prev":
                    case "mouse_wheel_up":
                        PostMouseWheel(windowHandle, clientPoint, WheelDelta);
                        break;
                    case "toggle_inventory":
                        TapKey(windowHandle, VkTab);
                        break;
                    case "toggle_map":
                        TapKey(windowHandle, VkM);
                        break;
                    case "toggle_quest_log":
                        TapKey(windowHandle, VkO);
                        break;
                    case "escape_menu":
                    case "cancel":
                        TapKey(windowHandle, VkEscape);
                        break;
                    case "confirm":
                        TapKey(windowHandle, VkReturn);
                        break;
                    case "toggle_flashlight":
                        TapKey(windowHandle, VkF);
                        break;
                    case "console_toggle":
                        TapKey(windowHandle, VkF1, true);
                        break;
                    default:
                        throw new BridgeCommandException(400, "unsupported_action", "OS backend cannot execute action: " + action);
                }

                lastUiState = CreateUiState("Last window-scoped OS action: " + action);
            }
        }

        private void SetPrimaryHeld(IntPtr windowHandle, bool pressed)
        {
            if (primaryHeld == pressed)
            {
                return;
            }

            PostMouseButton(windowHandle, false, pressed);
            primaryHeld = pressed;
        }

        private void SetSecondaryHeld(IntPtr windowHandle, bool pressed)
        {
            if (secondaryHeld == pressed)
            {
                return;
            }

            PostMouseButton(windowHandle, true, pressed);
            secondaryHeld = pressed;
        }

        private void SetInteractHeld(IntPtr windowHandle, bool pressed)
        {
            if (interactHeld == pressed)
            {
                return;
            }

            PostKey(windowHandle, VkE, pressed);
            interactHeld = pressed;
        }

        private void TapKey(IntPtr windowHandle, ushort virtualKey, bool systemKey = false)
        {
            PostKey(windowHandle, virtualKey, true, systemKey);
            PostKey(windowHandle, virtualKey, false, systemKey);
        }

        private void PostKey(IntPtr windowHandle, ushort virtualKey, bool keyDown, bool systemKey = false)
        {
            var message = keyDown
                ? (systemKey ? WmSysKeyDown : WmKeyDown)
                : (systemKey ? WmSysKeyUp : WmKeyUp);
            var lParam = keyDown ? 0x00000001 : 0xC0000001;
            if (!PostMessage(windowHandle, message, new IntPtr(virtualKey), new IntPtr(lParam)))
            {
                throw CreateWin32Exception("Failed posting keyboard message.");
            }
        }

        private void PostMouseMove(IntPtr windowHandle, POINT clientPoint)
        {
            if (!PostMessage(windowHandle, WmMouseMove, IntPtr.Zero, MakeLParam(clientPoint.X, clientPoint.Y)))
            {
                throw CreateWin32Exception("Failed posting mouse move.");
            }
        }

        private void PostMouseButton(IntPtr windowHandle, bool rightButton, bool pressed)
        {
            var point = GetClientCenter(windowHandle);
            PostMouseMove(windowHandle, point);
            var message = rightButton
                ? (pressed ? WmRButtonDown : WmRButtonUp)
                : (pressed ? WmLButtonDown : WmLButtonUp);
            var wParam = pressed ? (rightButton ? MkRButton : MkLButton) : 0;
            if (!PostMessage(windowHandle, message, new IntPtr(wParam), MakeLParam(point.X, point.Y)))
            {
                throw CreateWin32Exception("Failed posting mouse button message.");
            }
        }

        private void PostMouseWheel(IntPtr windowHandle, POINT clientPoint, int delta)
        {
            PostMouseMove(windowHandle, clientPoint);
            var wParam = new IntPtr(delta << 16);
            if (!PostMessage(windowHandle, WmMouseWheel, wParam, MakeLParam(clientPoint.X, clientPoint.Y)))
            {
                throw CreateWin32Exception("Failed posting mouse wheel message.");
            }
        }

        private IntPtr RequireGameWindow()
        {
            var windowHandle = Process.GetCurrentProcess().MainWindowHandle;
            if (windowHandle == IntPtr.Zero)
            {
                throw new BridgeCommandException(503, "window_not_ready", "Game window handle was unavailable for window-scoped OS input.");
            }

            return windowHandle;
        }

        private static POINT GetClientCenter(IntPtr windowHandle)
        {
            if (!GetClientRect(windowHandle, out var clientRect))
            {
                throw new BridgeCommandException(500, "window_query_failed", "GetClientRect failed for the game window.");
            }

            return new POINT
            {
                X = (clientRect.Right - clientRect.Left) / 2,
                Y = (clientRect.Bottom - clientRect.Top) / 2
            };
        }

        private static ushort GetHotbarVirtualKey(int slot)
        {
            if (slot < 1 || slot > 10)
            {
                throw new BridgeCommandException(400, "bad_request", "slot must be in the range 1..10.");
            }

            return slot == 10 ? Vk0 : (ushort)(Vk1 + (slot - 1));
        }

        private static int GetRequiredInt(Dictionary<string, object> arguments, string key)
        {
            if (arguments != null)
            {
                foreach (var pair in arguments)
                {
                    if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return Convert.ToInt32(pair.Value, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }

            throw new BridgeCommandException(400, "bad_request", "Missing required argument: " + key);
        }

        private static IntPtr MakeLParam(int low, int high)
        {
            return new IntPtr((high << 16) | (low & 0xFFFF));
        }

        private static UiState CreateUiState(string note)
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
                Note = note
            };
        }

        private static BridgeCommandException CreateWin32Exception(string message)
        {
            return new BridgeCommandException(500, "os_message_failed", message + " Win32 error=" + Marshal.GetLastWin32Error() + ".");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    }
}
