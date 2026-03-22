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
  "TimestampUtc": "2026-03-22T00:00:00.0000000Z",
  "Data": {},
  "Error": null
}
```

```json
{
  "Ok": false,
  "Command": null,
  "TimestampUtc": "2026-03-22T00:00:00.0000000Z",
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
- `POST /api/command`

## POST /api/command Body

```json
{
  "Command": "move_forward_start",
  "Arguments": {}
}
```

`Command` may be either a state query command such as `get_state` or an action command such as `primary_action_start`.

## Action Arguments

### `look_delta`

```json
{
  "Command": "look_delta",
  "Arguments": {
    "dx": 25.0,
    "dy": -5.0
  }
}
```

### `select_hotbar_slot`

```json
{
  "Command": "select_hotbar_slot",
  "Arguments": {
    "slot": 2
  }
}
```

### `wait_for_respawn_screen`

```json
{
  "Command": "wait_for_respawn_screen",
  "Arguments": {
    "timeout_ms": 15000
  }
}
```

### `wait_for_respawn_complete`

```json
{
  "Command": "wait_for_respawn_complete",
  "Arguments": {
    "timeout_ms": 30000
  }
}
```

## `get_capabilities`

Phase 3 capabilities include:

- `Phase`
- `ActiveBackend`
- `AvailableBackends`
- `Commands`
- `Actions`
- `Respawn`
- `Features`

The `Respawn` block reports:

- `respawn_select_default`
- `respawn_at_bedroll`
- `respawn_near_bedroll`
- `respawn_at_random`
- `respawn_confirm`
- `respawn_cancel`
- `wait_for_respawn_screen`
- `wait_for_respawn_complete`
- `respawn_state_detection`

## `get_state`

Phase 3 returns the normal state plus death / respawn fields.

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

### `ui`

- `InventoryOpen`
- `MapOpen`
- `QuestLogOpen`
- `ConsoleOpen`
- `PauseMenuOpen`
- `DeathScreenOpen`
- `RespawnScreenOpen`
- `RespawnConfirmationOpen`

### `input_state`

- `MoveForward`
- `MoveBack`
- `MoveLeft`
- `MoveRight`
- `Sprint`
- `Crouch`
- `PrimaryAction`
- `SecondaryAction`
- `HoldInteract`
- `AutoRun`

While dead or respawning, `input_state` remains visible but normal gameplay commands are rejected.

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
