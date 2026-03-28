# Movement Approach Flow (Phase 4.5)

## Principles
- Navigation is walk-first.
- Sprint is not used for normal approach or exploration.
- Crouch is not used.
- Jump is forbidden by default and only evaluated inside obstacle recovery.
- `stop_all_input` must always be able to return the client to a neutral state.

## Input Set
- `move_forward_start / stop`
- `look_delta`
- `stop_all_input`
- `jump` only when `JumpDecision` approves it
- `move_left / move_right` may be used only as short recovery helpers

## Stop Distance Rules
- `loot / container / interactable / vehicle / npc / trader`: 2.0 to 2.8 m, default 2.4 m
- `resource / block / terrain`: 2.2 to 3.2 m, default 2.7 m
- `entity / enemy`: default 2.5 m

## Yaw Difference Rules
The controller computes target yaw from player position and target position, then normalizes the yaw difference to `-180 .. +180`.

- `abs(yaw_diff) > 60`
  - turn in place only
  - do not move forward
  - repeat `look_delta` until the heading converges
- `20 <= abs(yaw_diff) <= 60`
  - walk pulse for 0.15 to 0.25 seconds
  - stop
  - correct yaw
  - repeat
- `5 <= abs(yaw_diff) < 20`
  - keep walking
  - apply small `look_delta` corrections
- `abs(yaw_diff) < 5`
  - walking has priority
  - only minimal yaw correction is applied

## Stagnation Rules
- If approach has been active for at least 2 seconds and progress is under 0.5 m while target distance has not shrunk enough, run obstacle recovery.
- If heading correction alone continues for at least 3 seconds without convergence, stop, recalculate, and enter recovery.

## Final Alignment
After stop distance is reached:
1. stop all movement
2. align yaw
3. align pitch if needed
4. wait 0.05 to 0.1 seconds
5. call `get_look_target`
6. verify that target name or target kind matches the intended target
7. retry up to 20 times

If alignment still fails:
- perform a micro forward pulse or micro backward pulse
- retry alignment
- if repeated attempts still fail, fall back to obstacle recovery
