# Troubleshooting

## Mod does not start

- Confirm `ModInfo.xml` and `mnetSevenDaysBridge.dll` are under `Mods\mnetSevenDaysBridge\`.
- Confirm `Config\bridge_config.json` exists.
- Check the F1 console for `mnetSevenDaysBridge startup requested`.

## Port bind failed

- Confirm nothing else is listening on `127.0.0.1:18771`.
- If needed, change `port` in `bridge_config.json`.

## Python client cannot connect

- Confirm the game is running with EAC disabled.
- Confirm the world has finished loading.
- Check Windows Firewall or security software for localhost HTTP interference.

## Input commands return `player_unavailable`

- The bridge can start before a local player entity exists.
- Join a world and wait until the player is spawned before sending gameplay commands.

## Gameplay commands return `PLAYER_DEAD`

- This is expected while the player is dead or while respawn is in progress.
- Use `get_state` to confirm `player.is_dead`, `player.respawn_in_progress`, and the respawn UI flags.
- Use `wait_for_respawn_screen`, then a `respawn_*` command, then `wait_for_respawn_complete`.

## Respawn commands return `INVALID_RESPAWN_STATE`

- The bridge only accepts respawn commands while dead or while the respawn screen is open.
- Confirm `ui.respawn_screen_open=true` or `player.is_dead=true`.

## Respawn commands return `RESPAWN_NOT_AVAILABLE`

- The requested spawn option is not currently offered by the game.
- Check `player.bedroll_spawn_available` and `player.respawn_cooldown_seconds`.
- If bedroll respawn is unavailable, use `respawn_select_default` or `respawn_at_random`.

## `wait_for_respawn_*` times out

- Confirm the world is still advancing and the player is actually on a death / respawn screen.
- Inspect the bridge log tail to confirm the tracker is seeing UI updates.
- Increase `timeout_ms` for slower worlds or heavily modded environments.

## `toggle_quest_log` opens UI but `quest_log_open` stays `null`

- This is a known best-effort detection gap.
- The quest windows can still open even when `quest_log_open` cannot be observed reliably.

## `toggle_flashlight` still fails

- This remains a known tracked issue from Phase 2.5.
- If the action returns an input-tick error, use the game UI manually for that session and keep the bridge log for later repro.

## `confirm` returns `confirm_unavailable`

- This remains a known tracked issue from Phase 2.5.
- The current menu may not expose a default or selected `XUi` entry that the bridge can safely press.

## `stop_all_input` was sent but movement continues

- Confirm the game was restarted after copying the latest mod files.
- Check the mod log for `backend_apply_failed`.
- Send `emergency_neutral_state`, then verify `get_state.input_state` is neutral.
- If the player died mid-command, confirm the latest build is loaded so the death transition can clear held input automatically.
