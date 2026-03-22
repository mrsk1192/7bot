# Supported Actions

`mnetSevenDaysBridge` Phase 3 keeps the existing Phase 2.5 action surface and adds formal death / respawn handling.

## Action Matrix

| Action | Status | Backend | Notes |
|---|---|---|---|
| `move_forward_start/stop` | supported | `internal` | Rejected with `PLAYER_DEAD` while dead or respawning. |
| `move_back_start/stop` | supported | `internal` | Rejected with `PLAYER_DEAD` while dead or respawning. |
| `move_left_start/stop` | supported | `internal` | Rejected with `PLAYER_DEAD` while dead or respawning. |
| `move_right_start/stop` | supported | `internal` | Rejected with `PLAYER_DEAD` while dead or respawning. |
| `jump` | supported | `internal` | One-frame pulse, blocked while dead. |
| `crouch_start/stop/toggle` | supported | `internal` | Bridge-held crouch state is cleared on death and respawn. |
| `sprint_start/stop` | supported | `internal` | Bridge-held sprint state is cleared on death and respawn. |
| `autorun_toggle` | supported | `internal` | Cleared on death and respawn. |
| `look_delta` | supported | `internal` | Blocked while dead. |
| `look_to` | supported | `internal` | Direct absolute camera path. Blocked while dead. |
| `turn_left/right` | supported | `internal` | Blocked while dead. |
| `look_up/down` | supported | `internal` | Blocked while dead. |
| `primary_action_start/stop` | supported | `os` | Window-scoped mouse button injection. Cleared by `stop_all_input` and on death. |
| `secondary_action_start/stop` | supported | `os` | Window-scoped mouse button injection. Cleared by `stop_all_input` and on death. |
| `reload` | supported | `os` | Window-scoped `R` key pulse. Blocked while dead. |
| `use_interact` | supported | `os` | Window-scoped `E` key pulse. Blocked while dead. |
| `hold_interact_start/stop` | supported | `os` | Held `E` state is cleared by `stop_all_input` and on death. |
| `attack_light_tap` | supported | `os` | Short left-click tap. Blocked while dead. |
| `attack_heavy_start/stop` | supported | `os` | Mapped to the alternate/right-click path. Blocked while dead. |
| `aim_start/stop` | supported | `os` | Mapped to the alternate/right-click path. Blocked while dead. |
| `select_hotbar_slot` | supported | `internal` | Uses player inventory methods. Blocked while dead. |
| `hotbar_next/prev` | supported | `internal` | Uses player inventory methods. Blocked while dead. |
| `mouse_wheel_up/down` | supported | `internal` | Routed to hotbar movement. Blocked while dead. |
| `toggle_inventory` | supported | `internal` | UI-only; still rejected while dead to keep respawn flow isolated. |
| `toggle_map` | supported | `internal` | UI-only; still rejected while dead to keep respawn flow isolated. |
| `toggle_quest_log` | supported | `internal` | Opens quest UI but `quest_log_open` detection is still best-effort. |
| `escape_menu` | supported | `internal` | Works in normal play. During respawn use `respawn_cancel` instead. |
| `confirm` | supported | `internal` | Still depends on current/default XUi selection and is not used for respawn. |
| `cancel` | supported | `internal` | Works for topmost bridge-managed windows; `menu_open` may remain true in some UI edge cases. |
| `toggle_flashlight` | supported | `internal` | Known limitation: internal pulse path is still brittle in some worlds. |
| `console_toggle` | supported | `internal` | Uses `GameManager.SetConsoleWindowVisible(...)`. |
| `respawn_select_default` | supported | `internal` | Uses the respawn selection controller and chooses bedroll when available, otherwise random spawn. |
| `respawn_at_bedroll` | supported | `internal` | Returns `RESPAWN_NOT_AVAILABLE` when no bedroll option exists. |
| `respawn_near_bedroll` | supported | `internal` | Returns `RESPAWN_NOT_AVAILABLE` when no near-bedroll option exists. |
| `respawn_at_random` | supported | `internal` | Uses the respawn selection controller. |
| `respawn_confirm` | supported | `internal` | Confirms the current respawn selection or falls back to the default respawn option. |
| `respawn_cancel` | supported | `internal` | Closes the respawn selection UI when open. |
| `wait_for_respawn_screen` | supported | `internal` | Waits on tracked death / respawn UI state. |
| `wait_for_respawn_complete` | supported | `internal` | Waits until the player is alive and respawn is no longer in progress. |
| `stop_all_input` | supported | `hybrid` | Clears all held internal and OS-backed inputs. |
| `reset_all_toggles` | supported | `hybrid` | Clears all held internal and OS-backed inputs. |
| `release_all_held_keys` | supported | `hybrid` | Clears all held internal and OS-backed inputs. |
| `emergency_neutral_state` | supported | `hybrid` | Clears all held internal and OS-backed inputs. |

## Known Limitations

| Item | Status | Notes |
|---|---|---|
| `toggle_quest_log` visibility detection | tracked | The action executes, but `quest_log_open` can still remain `null` depending on which quest windows the game instantiates. |
| `toggle_flashlight` | tracked | The internal flashlight pulse can still hit action-tick timing issues in some sessions. |
| `confirm` | tracked | Confirm depends on a selected/default XUi entry and may return `confirm_unavailable` in sparse menus. |
| `cancel` menu cleanup | tracked | Some UI stacks still leave `menu_open=true` after the topmost window closes. |

## Death / Respawn Rules

- Death or respawn state transition always clears held input from both the internal backend and the OS backend.
- While dead or while `respawn_in_progress=true`, normal gameplay commands are rejected with `PLAYER_DEAD`.
- While alive, respawn commands are rejected with `INVALID_RESPAWN_STATE`.
- `respawn_*` commands return `RESPAWN_NOT_AVAILABLE` when the requested spawn option is not currently offered by the game.
