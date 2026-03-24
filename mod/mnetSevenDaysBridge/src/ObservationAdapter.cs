using System;
using System.Collections.Generic;

namespace mnetSevenDaysBridge
{
    public sealed class ObservationAdapter
    {
        private readonly BridgeLogger logger;
        private readonly GameStateCollector collector;
        private readonly ObservationService observationService;
        private readonly ObservationCommandQueue queue;
        private readonly Func<WebSocketPushServer> getWebSocketServer;

        // Main thread only — no lock needed.
        private bool previousIsDead;

        public ObservationAdapter(
            BridgeLogger logger,
            GameStateCollector collector,
            ObservationService observationService,
            ObservationCommandQueue queue,
            Func<WebSocketPushServer> getWebSocketServer = null)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.collector = collector ?? throw new ArgumentNullException(nameof(collector));
            this.observationService = observationService ?? throw new ArgumentNullException(nameof(observationService));
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
            this.getWebSocketServer = getWebSocketServer;
        }

        public void Update()
        {
            var pending = queue.Drain();
            foreach (var item in pending)
            {
                try
                {
                    item.CompleteSuccess(Execute(item.CommandName, item.Arguments));
                }
                catch (BridgeCommandException exception)
                {
                    item.CompleteFailure(exception.ErrorType, exception.Message);
                }
                catch (Exception exception)
                {
                    logger.Error("Failed while executing an observation command on the main thread.", exception);
                    item.CompleteFailure("observation_command_failed", exception.Message);
                }
            }

            TryBroadcastStateEvents();
        }

        private void TryBroadcastStateEvents()
        {
            var ws = getWebSocketServer?.Invoke();
            if (ws == null)
            {
                return;
            }

            try
            {
                var state = collector.CollectState() as Dictionary<string, object>;
                if (state == null)
                {
                    return;
                }

                bool isDead = false;
                if (state.TryGetValue("Player", out var playerObj)
                    && playerObj is Dictionary<string, object> player
                    && player.TryGetValue("IsDead", out var deadObj)
                    && deadObj is bool dead)
                {
                    isDead = dead;
                }

                if (!previousIsDead && isDead)
                {
                    ws.BroadcastEvent("death", new Dictionary<string, object> { { "State", state } });
                }
                else if (previousIsDead && !isDead)
                {
                    ws.BroadcastEvent("respawn_complete", new Dictionary<string, object> { { "State", state } });
                }
                else
                {
                    ws.BroadcastEvent("state_update", state);
                }

                previousIsDead = isDead;
            }
            catch (Exception ex)
            {
                logger.Warn("WebSocket state broadcast failed: " + ex.Message);
            }
        }

        private object Execute(string commandName, Dictionary<string, object> arguments)
        {
            switch ((commandName ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "get_state":
                    return collector.CollectState();
                case "get_player_position":
                    var position = collector.CollectPosition();
                    return new Dictionary<string, object>
                    {
                        { "Position", position },
                        { "Available", position != null }
                    };
                case "get_player_rotation":
                    var rotation = collector.CollectRotation();
                    return new Dictionary<string, object>
                    {
                        { "Rotation", rotation },
                        { "Available", rotation != null && (rotation.Yaw.HasValue || rotation.Pitch.HasValue) }
                    };
                case "get_look_target":
                    return observationService.GetLookTarget();
                case "get_interaction_context":
                    return observationService.GetInteractionContext();
                case "query_resource_candidates":
                    return observationService.QueryResourceCandidates(arguments);
                case "query_interactables_in_radius":
                    return observationService.QueryInteractablesInRadius(arguments);
                case "query_entities_in_radius":
                    return observationService.QueryEntitiesInRadius(arguments);
                case "get_environment_summary":
                    return observationService.GetEnvironmentSummary();
                case "get_biome_info":
                    return observationService.GetBiomeInfo();
                case "get_terrain_summary":
                    return observationService.GetTerrainSummary();
                default:
                    throw new BridgeCommandException(400, "unsupported_command", "Unsupported observation command: " + commandName);
            }
        }
    }
}
