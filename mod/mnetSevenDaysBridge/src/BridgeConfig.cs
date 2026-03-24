using System;
using System.IO;

namespace mnetSevenDaysBridge
{
    public sealed class BridgeConfig
    {
        public bool Enabled { get; set; } = true;

        public string Host { get; set; } = "127.0.0.1";

        public int Port { get; set; } = 18771;

        public string CommunicationMode { get; set; } = "http_polling";

        public string LogLevel { get; set; } = "info";

        public int MaxLogLinesInMemory { get; set; } = 2000;

        public int RequestTimeoutMs { get; set; } = 5000;

        public int MaxCommandsPerSecond { get; set; } = 120;

        public int MaxCommandQueueLength { get; set; } = 256;

        public float DefaultLookStep { get; set; } = 12f;

        public bool EnableOsInputBackend { get; set; } = true;

        public bool BringGameWindowToFrontForOsInput { get; set; } = true;

        public bool AutoQuickContinueOnStartup { get; set; } = true;

        public string AutoQuickContinueGameWorld { get; set; } = "Pregen08k01";

        public string AutoQuickContinueGameName { get; set; } = "VanillaDiagClientOnly";

        public int WebSocketPort { get; set; } = 18772;

        public bool EnableWebSocketPush { get; set; } = true;

        public string BaseUrl
        {
            get { return $"http://{Host}:{Port}/"; }
        }

        public static BridgeConfig Load(string modRootPath)
        {
            var configPath = Path.Combine(modRootPath, "Config", "bridge_config.json");
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException("Bridge config file was not found.", configPath);
            }

            var json = File.ReadAllText(configPath);
            var parsed = new BridgeJson().DeserializeObject(json);
            var config = BridgeConfigDto.FromDictionary(parsed);

            if (string.IsNullOrWhiteSpace(config.Host))
            {
                throw new InvalidOperationException("Bridge config host must not be empty.");
            }

            if (!string.Equals(config.Host, "127.0.0.1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Only 127.0.0.1 binding is allowed.");
            }

            if (config.Port <= 0 || config.Port > 65535)
            {
                throw new InvalidOperationException("Bridge config port must be in the range 1-65535.");
            }

            return new BridgeConfig
            {
                Enabled = config.Enabled,
                Host = config.Host,
                Port = config.Port,
                CommunicationMode = string.IsNullOrWhiteSpace(config.CommunicationMode)
                    ? "http_polling"
                    : config.CommunicationMode,
                LogLevel = string.IsNullOrWhiteSpace(config.LogLevel) ? "info" : config.LogLevel,
                MaxLogLinesInMemory = config.MaxLogLinesInMemory <= 0 ? 2000 : config.MaxLogLinesInMemory,
                RequestTimeoutMs = config.RequestTimeoutMs <= 0 ? 5000 : config.RequestTimeoutMs,
                MaxCommandsPerSecond = config.MaxCommandsPerSecond <= 0 ? 120 : config.MaxCommandsPerSecond,
                MaxCommandQueueLength = config.MaxCommandQueueLength <= 0 ? 256 : config.MaxCommandQueueLength,
                DefaultLookStep = config.DefaultLookStep <= 0f ? 12f : config.DefaultLookStep,
                EnableOsInputBackend = config.EnableOsInputBackend,
                BringGameWindowToFrontForOsInput = config.BringGameWindowToFrontForOsInput,
                AutoQuickContinueOnStartup = config.AutoQuickContinueOnStartup,
                AutoQuickContinueGameWorld = string.IsNullOrWhiteSpace(config.AutoQuickContinueGameWorld)
                    ? "Pregen08k01"
                    : config.AutoQuickContinueGameWorld,
                AutoQuickContinueGameName = string.IsNullOrWhiteSpace(config.AutoQuickContinueGameName)
                    ? "VanillaDiagClientOnly"
                    : config.AutoQuickContinueGameName,
                WebSocketPort = config.WebSocketPort <= 0 ? 18772 : config.WebSocketPort,
                EnableWebSocketPush = config.EnableWebSocketPush
            };
        }

        private sealed class BridgeConfigDto
        {
            public bool Enabled { get; set; } = true;

            public string Host { get; set; } = "127.0.0.1";

            public int Port { get; set; } = 18771;

            public string CommunicationMode { get; set; } = "http_polling";

            public string LogLevel { get; set; } = "info";

            public int MaxLogLinesInMemory { get; set; } = 2000;

            public int RequestTimeoutMs { get; set; } = 5000;

            public int MaxCommandsPerSecond { get; set; } = 120;

            public int MaxCommandQueueLength { get; set; } = 256;

            public float DefaultLookStep { get; set; } = 12f;

            public bool EnableOsInputBackend { get; set; } = true;

            public bool BringGameWindowToFrontForOsInput { get; set; } = true;

            public bool AutoQuickContinueOnStartup { get; set; } = true;

            public string AutoQuickContinueGameWorld { get; set; } = "Pregen08k01";

            public string AutoQuickContinueGameName { get; set; } = "VanillaDiagClientOnly";

            public int WebSocketPort { get; set; } = 18772;

            public bool EnableWebSocketPush { get; set; } = true;

            public static BridgeConfigDto FromDictionary(System.Collections.Generic.Dictionary<string, object> values)
            {
                var dto = new BridgeConfigDto();
                if (TryGet(values, "enabled", out var enabled) && enabled is bool enabledBool)
                {
                    dto.Enabled = enabledBool;
                }

                if (TryGet(values, "host", out var host) && host != null)
                {
                    dto.Host = host.ToString();
                }

                if (TryGet(values, "port", out var port) && port != null)
                {
                    dto.Port = Convert.ToInt32(port);
                }

                if (TryGet(values, "communication_mode", out var communicationMode) && communicationMode != null)
                {
                    dto.CommunicationMode = communicationMode.ToString();
                }

                if (TryGet(values, "log_level", out var logLevel) && logLevel != null)
                {
                    dto.LogLevel = logLevel.ToString();
                }

                if (TryGet(values, "max_log_lines_in_memory", out var maxLogLines) && maxLogLines != null)
                {
                    dto.MaxLogLinesInMemory = Convert.ToInt32(maxLogLines);
                }

                if (TryGet(values, "request_timeout_ms", out var requestTimeoutMs) && requestTimeoutMs != null)
                {
                    dto.RequestTimeoutMs = Convert.ToInt32(requestTimeoutMs);
                }

                if (TryGet(values, "max_commands_per_second", out var maxCommandsPerSecond) && maxCommandsPerSecond != null)
                {
                    dto.MaxCommandsPerSecond = Convert.ToInt32(maxCommandsPerSecond);
                }

                if (TryGet(values, "max_command_queue_length", out var maxCommandQueueLength) && maxCommandQueueLength != null)
                {
                    dto.MaxCommandQueueLength = Convert.ToInt32(maxCommandQueueLength);
                }

                if (TryGet(values, "default_look_step", out var defaultLookStep) && defaultLookStep != null)
                {
                    dto.DefaultLookStep = Convert.ToSingle(defaultLookStep, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (TryGet(values, "enable_os_input_backend", out var enableOsInputBackend) && enableOsInputBackend != null)
                {
                    dto.EnableOsInputBackend = Convert.ToBoolean(enableOsInputBackend);
                }

                if (TryGet(values, "bring_game_window_to_front_for_os_input", out var bringGameWindowToFrontForOsInput)
                    && bringGameWindowToFrontForOsInput != null)
                {
                    dto.BringGameWindowToFrontForOsInput = Convert.ToBoolean(bringGameWindowToFrontForOsInput);
                }

                if (TryGet(values, "auto_quick_continue_on_startup", out var autoQuickContinueOnStartup)
                    && autoQuickContinueOnStartup != null)
                {
                    dto.AutoQuickContinueOnStartup = Convert.ToBoolean(autoQuickContinueOnStartup);
                }

                if (TryGet(values, "auto_quick_continue_game_world", out var autoQuickContinueGameWorld)
                    && autoQuickContinueGameWorld != null)
                {
                    dto.AutoQuickContinueGameWorld = autoQuickContinueGameWorld.ToString();
                }

                if (TryGet(values, "auto_quick_continue_game_name", out var autoQuickContinueGameName)
                    && autoQuickContinueGameName != null)
                {
                    dto.AutoQuickContinueGameName = autoQuickContinueGameName.ToString();
                }

                if (TryGet(values, "websocket_port", out var websocketPort) && websocketPort != null)
                {
                    dto.WebSocketPort = Convert.ToInt32(websocketPort);
                }

                if (TryGet(values, "enable_websocket_push", out var enableWebSocketPush) && enableWebSocketPush != null)
                {
                    dto.EnableWebSocketPush = Convert.ToBoolean(enableWebSocketPush);
                }

                return dto;
            }

            private static bool TryGet(System.Collections.Generic.Dictionary<string, object> values, string key, out object value)
            {
                foreach (var pair in values)
                {
                    if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = pair.Value;
                        return true;
                    }
                }

                value = null;
                return false;
            }
        }
    }
}
