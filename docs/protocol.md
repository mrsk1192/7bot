# Protocol

## Transport

- Protocol: HTTP/1.1 + JSON
- Bind: `127.0.0.1`
- Default port: `18771`

## Response Envelope

```json
{
  "Ok": true,
  "Command": "get_state",
  "TimestampUtc": "2026-03-27T00:00:00.0000000Z",
  "Data": {},
  "Error": null
}
```

```json
{
  "Ok": false,
  "Command": null,
  "TimestampUtc": "2026-03-27T00:00:00.0000000Z",
  "Data": null,
  "Error": {
    "Type": "PLAYER_DEAD",
    "Message": "Normal gameplay commands are rejected while dead or during respawn."
  }
}
```

## Endpoints

- `GET /api/ping`
- `GET /api/get_version`
- `GET /api/get_capabilities`
- `GET /api/get_state`
- `GET /api/get_player_position`
- `GET /api/get_player_rotation`
- `GET /api/get_logs_tail?lines=50`
- `GET /api/get_look_target`
- `GET /api/get_interaction_context`
- `GET /api/query_resource_candidates`
- `GET /api/query_interactables_in_radius`
- `GET /api/query_entities_in_radius`
- `GET /api/get_environment_summary`
- `GET /api/get_biome_info`
- `GET /api/get_terrain_summary`
- `POST /api/command`

## POST /api/command Body

```json
{
  "Command": "move_forward_start",
  "Arguments": {}
}
```

`Command` may be either a state query command such as `get_state`, an observation query such as `get_look_target`, or an action command such as `primary_action_start`.

## Observation Query Arguments

### `query_resource_candidates`

```json
{
  "Command": "query_resource_candidates",
  "Arguments": {
    "x": -250.0,
    "y": 46.0,
    "z": 900.0,
    "radius": 12.0,
    "max_results": 8,
    "include_surface_only": true,
    "include_exposed_only": true,
    "candidate_categories": ["surface_resource_node", "loot_container"],
    "likely_resource_types": ["stone", "wood", "loot"],
    "min_confidence": 0.35,
    "sort_by": "distance"
  }
}
```

### `query_interactables_in_radius`

```json
{
  "Command": "query_interactables_in_radius",
  "Arguments": {
    "x": -250.0,
    "y": 46.0,
    "z": 900.0,
    "radius": 10.0,
    "max_results": 10,
    "include_blocks": true,
    "include_entities": true,
    "include_loot": true,
    "include_doors": true,
    "include_vehicles": true,
    "include_npcs": true,
    "include_traders": true,
    "include_locked": true
  }
}
```

### `query_entities_in_radius`

```json
{
  "Command": "query_entities_in_radius",
  "Arguments": {
    "x": -250.0,
    "y": 46.0,
    "z": 900.0,
    "radius": 24.0,
    "max_results": 12,
    "include_hostile": true,
    "include_npc": true,
    "include_animals": true,
    "include_neutral": true,
    "include_dead": false
  }
}
```

## `get_capabilities`

Phase 4 capabilities include:

- `Phase`
- `ActiveBackend`
- `AvailableBackends`
- `Commands`
- `Actions`
- `Respawn`
- `Features`

Phase 4 observation commands are reported in `Commands`:

- `get_look_target`
- `get_interaction_context`
- `query_resource_candidates`
- `query_interactables_in_radius`
- `query_entities_in_radius`
- `get_environment_summary`
- `get_biome_info`
- `get_terrain_summary`

`Features.phase4_observation_queries` reports whether the observation pipeline is available.

## `get_state`

Phase 4 extends `get_state` with observation summaries while keeping the Phase 3 death/respawn fields.

### `player`

- `Alive`
- `IsDead`
- `DeathScreenVisible`
- `RespawnAvailable`
- `RespawnCooldownSeconds`
- `LastDeathTime`
- `LastDeathPosition`
- `BedrollSpawnAvailable`
- `NearestSpawnOptionSummary`
- `RespawnInProgress`
- `JustRespawned`
- `HoldingLight`

### `ui`

- `InventoryOpen`
- `MapOpen`
- `QuestLogOpen`
- `ConsoleOpen`
- `PauseMenuOpen`
- `DeathScreenOpen`
- `RespawnScreenOpen`
- `RespawnConfirmationOpen`

### `resource_observation`

- `PlayerPosition`
- `PlayerRotation`
- `Biome`
- `LookTarget`
- `InteractionContext`
- `Availability`

### `nearby_resource_candidates_summary`

- `Count`
- `TopCandidates`

### `nearby_interactables_summary`

- `Count`
- `TopInteractables`

### `nearby_entities_summary`

- `HostileCount`
- `NpcCount`
- `NearestHostileDistance`
- `TopEntities`

## Respawn Rules

- While alive, `respawn_*` commands are rejected with `INVALID_RESPAWN_STATE`.
- While dead or while respawning, normal gameplay commands are rejected with `PLAYER_DEAD`.
- If a requested respawn option is not currently offered by the game, the bridge returns `RESPAWN_NOT_AVAILABLE`.
- On death, all held input is released from both the internal and OS backends.
- After respawn completes, the input state machine is reset to neutral before normal commands are allowed again.

## Error Codes

- `PLAYER_DEAD`
- `RESPAWN_NOT_AVAILABLE`
- `INVALID_RESPAWN_STATE`
- `RESPAWN_WAIT_TIMEOUT`
