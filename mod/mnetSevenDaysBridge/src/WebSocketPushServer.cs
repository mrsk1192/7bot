using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mnetSevenDaysBridge
{
    /// <summary>
    /// A lightweight WebSocket push server that broadcasts game state events
    /// to connected Python clients on a separate port.
    /// Commands continue to use the existing HTTP endpoint unchanged.
    /// </summary>
    public sealed class WebSocketPushServer : IDisposable
    {
        private readonly BridgeConfig config;
        private readonly BridgeLogger logger;
        private readonly BridgeJson json;
        private readonly HttpListener listener;
        private readonly ConcurrentBag<WebSocket> clients = new ConcurrentBag<WebSocket>();
        private Thread listenerThread;
        private volatile bool running;
        private Timer pingTimer;

        public WebSocketPushServer(BridgeConfig config, BridgeLogger logger, BridgeJson json)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.json = json ?? throw new ArgumentNullException(nameof(json));
            listener = new HttpListener();
            listener.Prefixes.Add($"http://{config.Host}:{config.WebSocketPort}/");
        }

        public void Start()
        {
            if (running)
            {
                return;
            }

            running = true;
            listener.Start();
            listenerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "mnetSevenDaysBridge.WebSocketListener"
            };
            listenerThread.Start();
            pingTimer = new Timer(SendPing, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            logger.Info($"WebSocket push server started at ws://{config.Host}:{config.WebSocketPort}/");
        }

        public void Dispose()
        {
            if (!running)
            {
                return;
            }

            running = false;
            try { pingTimer?.Dispose(); } catch { }
            try { listener.Stop(); } catch { }
            logger.Info("WebSocket push server stopped.");
        }

        /// <summary>Broadcasts an event to all connected WebSocket clients.</summary>
        public void BroadcastEvent(string type, object data)
        {
            if (!running || clients.IsEmpty)
            {
                return;
            }

            var payload = new Dictionary<string, object>
            {
                { "Type", type },
                { "TimestampUtc", DateTime.UtcNow.ToString("o") },
                { "Data", data }
            };

            var message = json.Serialize(payload);
            BroadcastRaw(message);
        }

        private void BroadcastRaw(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var segment = new ArraySegment<byte>(bytes);
            var dead = new List<WebSocket>();

            foreach (var ws in clients)
            {
                try
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None).Wait(500);
                    }
                    else
                    {
                        dead.Add(ws);
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn("WebSocket send failed (client will be removed): " + ex.Message);
                    dead.Add(ws);
                }
            }

            // Rebuild without dead connections.
            if (dead.Count > 0)
            {
                RemoveDeadClients(dead);
            }
        }

        private void RemoveDeadClients(List<WebSocket> dead)
        {
            // ConcurrentBag has no remove — drain and refill without dead entries.
            var live = new List<WebSocket>();
            while (clients.TryTake(out var ws))
            {
                if (!dead.Contains(ws))
                {
                    live.Add(ws);
                }
            }

            foreach (var ws in live)
            {
                clients.Add(ws);
            }
        }

        private void SendPing(object state)
        {
            try
            {
                BroadcastEvent("ping", null);
            }
            catch (Exception ex)
            {
                logger.Warn("WebSocket ping broadcast failed: " + ex.Message);
            }
        }

        private void ListenLoop()
        {
            logger.Info("WebSocket listener loop started.");
            while (running)
            {
                try
                {
                    var context = listener.GetContext();
                    if (!IPAddress.IsLoopback(IPAddress.Parse(context.Request.RemoteEndPoint.Address.ToString())))
                    {
                        context.Response.StatusCode = 403;
                        context.Response.Close();
                        continue;
                    }

                    if (context.Request.IsWebSocketRequest)
                    {
                        Task.Run(() => HandleWebSocket(context));
                    }
                    else
                    {
                        context.Response.StatusCode = 426;
                        context.Response.AddHeader("Upgrade", "websocket");
                        context.Response.Close();
                    }
                }
                catch (HttpListenerException ex)
                {
                    if (running)
                    {
                        logger.Error("WebSocket listener error.", ex);
                    }
                }
                catch (ObjectDisposedException) when (!running)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.Error("Unhandled error in WebSocket listener loop.", ex);
                }
            }

            logger.Info("WebSocket listener loop stopped.");
        }

        private async Task HandleWebSocket(HttpListenerContext context)
        {
            WebSocket ws = null;
            try
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                ws = wsContext.WebSocket;
                clients.Add(ws);
                logger.Info("WebSocket client connected. Total=" + CountClients());

                // Keep alive until client disconnects.
                var buffer = new byte[256];
                while (ws.State == WebSocketState.Open && running)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                if (running)
                {
                    logger.Warn("WebSocket client handler error: " + ex.Message);
                }
            }
            finally
            {
                logger.Info("WebSocket client disconnected.");
            }
        }

        private int CountClients()
        {
            int count = 0;
            foreach (var _ in clients)
            {
                count++;
            }

            return count;
        }
    }
}
