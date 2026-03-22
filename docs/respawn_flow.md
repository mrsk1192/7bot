# Respawn Flow

## Overview

Phase 3 treats death / respawn as a separate state machine from normal gameplay input.

1. Player dies.
2. `DeathRespawnStateTracker` detects the death transition.
3. The bridge releases all held input from both backends.
4. Normal gameplay commands begin returning `PLAYER_DEAD`.
5. Respawn UI state is tracked from `windowDeathBar` and `XUiC_SpawnSelectionWindow`.
6. A `respawn_*` command is sent.
7. `RespawnController` triggers the requested respawn option.
8. `RespawnInProgress` stays true until the player is alive again.
9. On respawn completion, the input state machine is reset to neutral.
10. Normal gameplay commands are accepted again.

## Command Sequence Examples

### Default respawn

1. `wait_for_respawn_screen`
2. `respawn_select_default`
3. `wait_for_respawn_complete`

### Bedroll respawn

1. `wait_for_respawn_screen`
2. `respawn_at_bedroll`
3. `wait_for_respawn_complete`

## State Expectations

### While dead

- `player.is_dead=true`
- `player.alive=false`
- `player.respawn_in_progress=false`
- `ui.death_screen_open` or `ui.respawn_screen_open` may be true
- normal movement / combat / mining commands are rejected

### After respawn command

- `player.respawn_in_progress=true`
- normal gameplay commands are still rejected

### After respawn completion

- `player.alive=true`
- `player.is_dead=false`
- `player.just_respawned=true` for a short bridge-managed window
- `input_state` is neutral

## Fallback Behavior

- `respawn_confirm` uses the current spawn selection when available.
- If no explicit selection is known, it falls back to the bridge default selection logic.
- If the game is still in respawn cooldown, respawn commands return `RESPAWN_NOT_AVAILABLE`.

## Known Gaps

- Respawn option summaries are best-effort and come from the current spawn method / target data exposed by the game.
- Friend-based and backpack-adjacent respawn flows are not exposed as dedicated bridge commands in this phase.
