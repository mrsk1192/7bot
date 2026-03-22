using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace mnetSevenDaysBridge
{
    public sealed class HttpCommandReceiver : IDisposable
    {
        private readonly BridgeConfig config;
        private readonly BridgeLogger logger;
        private readonly BridgeJson json;
        private readonly GameStateCollector collector;
        private readonly VersionInfo versionInfo;
        private readonly InputAdapter inputAdapter;
        private readonly CommandQueue commandQueue;
        private readonly TcpListener listener;
        private Thread workerThread;
        private volatile bool running;

        public HttpCommandReceiver(
            BridgeConfig config,
            BridgeLogger logger,
            BridgeJson json,
            GameStateCollector collector,
            VersionInfo versionInfo,
            InputAdapter inputAdapter,
            CommandQueue commandQueue)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.json = json ?? throw new ArgumentNullException(nameof(json));
            this.collector = collector ?? throw new ArgumentNullException(nameof(collector));
            this.versionInfo = versionInfo ?? throw new ArgumentNullException(nameof(versionInfo));
            this.inputAdapter = inputAdapter ?? throw new ArgumentNullException(nameof(inputAdapter));
            this.commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
            listener = new TcpListener(IPAddress.Parse(config.Host), config.Port);
        }

        public void Start()
        {
            if (running)
            {
                return;
            }

            listener.Start();
            running = true;
            workerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "mnetSevenDaysBridge.HttpListener"
            };
            workerThread.Start();
        }

        public void Dispose()
        {
            running = false;
            try
            {
                listener.Stop();
            }
            catch (SocketException exception)
            {
                logger.Warn("Socket exception while stopping listener: " + exception.Message);
            }
        }

        private void ListenLoop()
        {
            logger.Info($"HTTP listener started at {config.BaseUrl}");
            while (running)
            {
                TcpClient client = null;
                try
                {
                    client = listener.AcceptTcpClient();
                    HandleClient(client);
                }
                catch (SocketException exception)
                {
                    if (running)
                    {
                        logger.Error("Socket error inside the HTTP listener loop.", exception);
                    }
                }
                catch (ObjectDisposedException)
                {
                    if (running)
                    {
                        throw;
                    }
                }
                catch (Exception exception)
                {
                    logger.Error("Unhandled error inside the HTTP listener loop.", exception);
                }
                finally
                {
                    if (client != null)
                    {
                        client.Close();
                    }
                }
            }

            logger.Info("HTTP listener stopped.");
        }

        private void HandleClient(TcpClient client)
        {
            var remoteAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true))
            {
                try
                {
                    if (!IPAddress.IsLoopback(remoteAddress))
                    {
                        WriteResponse(stream, 403, BuildError("forbidden", "Only localhost requests are allowed."));
                        return;
                    }

                    var requestLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(requestLine))
                    {
                        logger.Warn("Ignoring connection with an empty HTTP request line.");
                        return;
                    }

                    var requestParts = requestLine.Split(' ');
                    if (requestParts.Length < 2)
                    {
                        logger.Warn("Ignoring malformed HTTP request line: " + requestLine);
                        WriteResponse(stream, 400, BuildError("bad_request", "HTTP request line was malformed."));
                        return;
                    }

                    var method = requestParts[0].Trim().ToUpperInvariant();
                    var rawTarget = requestParts[1].Trim();
                    var headers = ReadHeaders(reader);
                    var body = ReadBody(reader, headers);

                    if (!Uri.TryCreate("http://127.0.0.1" + rawTarget, UriKind.Absolute, out var uri))
                    {
                        logger.Warn("Ignoring malformed request URI: " + rawTarget);
                        WriteResponse(stream, 400, BuildError("bad_request", "Request URI was malformed."));
                        return;
                    }

                    var path = uri.AbsolutePath.Trim('/');
                    var query = ParseQuery(uri.Query);
                    logger.Info($"Incoming request: {method} {rawTarget}");

                    if (method == "POST" && string.Equals(path, "api/command", StringComparison.OrdinalIgnoreCase))
                    {
                        CommandRequest commandRequest;
                        using (var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(body)))
                        {
                            commandRequest = json.DeserializeCommandRequest(bodyStream);
                        }

                        WriteResponse(
                            stream,
                            200,
                            BuildSuccess(
                                commandRequest.Command,
                                ExecuteCommand(commandRequest.Command, commandRequest.Arguments)));
                        return;
                    }

                    if (method == "GET" && TryHandleGet(stream, path, query))
                    {
                        return;
                    }

                    WriteResponse(stream, 404, BuildError("not_found", $"No route matched {method} {rawTarget}"));
                }
                catch (BridgeCommandException exception)
                {
                    WriteResponse(stream, exception.StatusCode, BuildError(exception.ErrorType, exception.Message));
                }
                catch (Exception exception)
                {
                    logger.Error("Unhandled request processing exception.", exception);
                    WriteResponse(stream, 500, BuildError("internal_error", exception.Message));
                }
            }
        }

        private Dictionary<string, string> ReadHeaders(StreamReader reader)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    throw new EndOfStreamException("HTTP headers were truncated.");
                }

                if (line.Length == 0)
                {
                    break;
                }

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var name = line.Substring(0, separatorIndex).Trim();
                var value = line.Substring(separatorIndex + 1).Trim();
                headers[name] = value;
            }

            return headers;
        }

        private string ReadBody(StreamReader reader, Dictionary<string, string> headers)
        {
            if (!headers.TryGetValue("Content-Length", out var contentLengthHeader)
                || !int.TryParse(contentLengthHeader, out var contentLength)
                || contentLength <= 0)
            {
                return string.Empty;
            }

            var buffer = new char[contentLength];
            var totalRead = 0;
            while (totalRead < contentLength)
            {
                var read = reader.Read(buffer, totalRead, contentLength - totalRead);
                if (read <= 0)
                {
                    throw new EndOfStreamException("HTTP body was truncated.");
                }

                totalRead += read;
            }

            return new string(buffer);
        }

        private Dictionary<string, string> ParseQuery(string rawQuery)
        {
            var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rawQuery))
            {
                return query;
            }

            var trimmed = rawQuery.TrimStart('?');
            if (trimmed.Length == 0)
            {
                return query;
            }

            foreach (var pair in trimmed.Split('&'))
            {
                if (string.IsNullOrWhiteSpace(pair))
                {
                    continue;
                }

                var parts = pair.Split(new[] { '=' }, 2);
                var key = Uri.UnescapeDataString(parts[0]);
                var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
                query[key] = value;
            }

            return query;
        }

        private bool TryHandleGet(Stream stream, string path, Dictionary<string, string> query)
        {
            switch (path.ToLowerInvariant())
            {
                case "":
                case "api/ping":
                    WriteResponse(stream, 200, BuildSuccess("ping", CreatePongPayload()));
                    return true;
                case "api/get_version":
                    WriteResponse(stream, 200, BuildSuccess("get_version", versionInfo));
                    return true;
                case "api/get_capabilities":
                    WriteResponse(stream, 200, BuildSuccess("get_capabilities", inputAdapter.GetCapabilities()));
                    return true;
                case "api/get_state":
                    WriteResponse(stream, 200, BuildSuccess("get_state", collector.CollectState()));
                    return true;
                case "api/get_player_position":
                    var position = collector.CollectPosition();
                    WriteResponse(stream, 200, BuildSuccess("get_player_position", new Dictionary<string, object>
                    {
                        { "position", position },
                        { "available", position != null }
                    }));
                    return true;
                case "api/get_player_rotation":
                    var rotation = collector.CollectRotation();
                    WriteResponse(stream, 200, BuildSuccess("get_player_rotation", new Dictionary<string, object>
                    {
                        { "rotation", rotation },
                        { "available", rotation != null && (rotation.Yaw.HasValue || rotation.Pitch.HasValue) }
                    }));
                    return true;
                case "api/get_logs_tail":
                    var requestedLines = 50;
                    if (query.TryGetValue("lines", out var linesRaw) && int.TryParse(linesRaw, out var parsedLines))
                    {
                        requestedLines = parsedLines;
                    }

                    var lines = logger.GetTail(requestedLines);
                    WriteResponse(stream, 200, BuildSuccess("get_logs_tail", new LogsTailResult
                    {
                        RequestedLines = requestedLines,
                        ReturnedLines = lines.Count,
                        Lines = lines
                    }));
                    return true;
                default:
                    return false;
            }
        }

        private object ExecuteCommand(string command, Dictionary<string, object> arguments)
        {
            switch ((command ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "ping":
                    return CreatePongPayload();
                case "get_version":
                    return versionInfo;
                case "get_capabilities":
                    return inputAdapter.GetCapabilities();
                case "get_state":
                    return collector.CollectState();
                case "get_player_position":
                    var position = collector.CollectPosition();
                    return new Dictionary<string, object>
                    {
                        { "position", position },
                        { "available", position != null }
                    };
                case "get_player_rotation":
                    var rotation = collector.CollectRotation();
                    return new Dictionary<string, object>
                    {
                        { "rotation", rotation },
                        { "available", rotation != null && (rotation.Yaw.HasValue || rotation.Pitch.HasValue) }
                    };
                case "get_logs_tail":
                    var requestedLines = 50;
                    if (arguments != null && arguments.TryGetValue("lines", out var linesValue) && linesValue != null)
                    {
                        int.TryParse(linesValue.ToString(), out requestedLines);
                    }

                    var lines = logger.GetTail(requestedLines);
                    return new LogsTailResult
                    {
                        RequestedLines = requestedLines,
                        ReturnedLines = lines.Count,
                        Lines = lines
                    };
                default:
                    if (ActionCatalog.IsRespawnWaitAction(command))
                    {
                        return inputAdapter.WaitForRespawnAction(command, arguments);
                    }

                    if (ActionCatalog.IsKnown(command))
                    {
                        return ExecuteQueuedAction(command, arguments);
                    }

                    throw new BridgeCommandException(400, "unsupported_command", "Unsupported command: " + command);
            }
        }

        private object ExecuteQueuedAction(string action, Dictionary<string, object> arguments)
        {
            var bridgeCommand = new BridgeCommand
            {
                Name = action,
                Arguments = arguments
            };

            if (!commandQueue.TryEnqueue(bridgeCommand, out var queuedCommand, out var error))
            {
                var statusCode = string.Equals(error.Type, "rate_limited", StringComparison.OrdinalIgnoreCase) ? 429 : 503;
                throw new BridgeCommandException(statusCode, error.Type, error.Message);
            }

            if (!queuedCommand.Completion.Wait(config.RequestTimeoutMs))
            {
                throw new BridgeCommandException(504, "command_timeout", "Timed out waiting for the input command to execute on the main thread.");
            }

            if (queuedCommand.Error != null)
            {
                throw new BridgeCommandException(400, queuedCommand.Error.Type, queuedCommand.Error.Message);
            }

            return queuedCommand.ResultData;
        }

        private Dictionary<string, object> CreatePongPayload()
        {
            return new Dictionary<string, object>
            {
                { "message", "pong" },
                { "bridge_version", versionInfo.BridgeVersion },
                { "active_backend", inputAdapter.ActiveBackendName }
            };
        }

        private BridgeResponse BuildSuccess(string command, object data)
        {
            return new BridgeResponse
            {
                Ok = true,
                Command = command,
                TimestampUtc = DateTime.UtcNow.ToString("o"),
                Data = data
            };
        }

        private BridgeResponse BuildError(string errorType, string errorMessage)
        {
            return new BridgeResponse
            {
                Ok = false,
                TimestampUtc = DateTime.UtcNow.ToString("o"),
                Error = new BridgeError
                {
                    Type = errorType,
                    Message = errorMessage
                }
            };
        }

        private void WriteResponse(Stream stream, int statusCode, BridgeResponse payload)
        {
            var body = json.Serialize(payload);
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var statusText = GetStatusText(statusCode);
            var header = string.Join(
                "\r\n",
                $"HTTP/1.1 {statusCode} {statusText}",
                "Content-Type: application/json; charset=utf-8",
                $"Content-Length: {bodyBytes.Length}",
                "Connection: close",
                string.Empty,
                string.Empty);
            var headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
            stream.Flush();
        }

        private string GetStatusText(int statusCode)
        {
            switch (statusCode)
            {
                case 200:
                    return "OK";
                case 400:
                    return "Bad Request";
                case 403:
                    return "Forbidden";
                case 404:
                    return "Not Found";
                case 408:
                    return "Request Timeout";
                case 429:
                    return "Too Many Requests";
                case 500:
                    return "Internal Server Error";
                case 503:
                    return "Service Unavailable";
                case 504:
                    return "Gateway Timeout";
                default:
                    return "Unknown";
            }
        }
    }
}
