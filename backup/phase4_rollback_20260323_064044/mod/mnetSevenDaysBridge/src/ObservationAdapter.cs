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

        public ObservationAdapter(
            BridgeLogger logger,
            GameStateCollector collector,
            ObservationService observationService,
            ObservationCommandQueue queue)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.collector = collector ?? throw new ArgumentNullException(nameof(collector));
            this.observationService = observationService ?? throw new ArgumentNullException(nameof(observationService));
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
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
