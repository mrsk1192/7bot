# GUI Settings

## Scan Settings Panel
The GUI exposes a dedicated scan settings panel so exploration behavior can be tuned without editing source code.

Fields shown in the panel:
- `Yaw scan min (deg)`
- `Yaw scan max (deg)`
- `Yaw scan step (deg)`
- `Pitch scan min (deg)`
- `Pitch scan max (deg)`
- `Pitch scan step (deg)`
- `Scan settle delay (ms)`
- `Near-ground priority enabled`
- `Near-ground distance threshold`

## Runtime Meaning
- Yaw is scanned from `min` to `max` in one direction.
- Pitch is scanned from `0` downward.
- Smaller steps improve detection quality but increase the number of view changes and queries.
- Larger near-ground thresholds make the explorer favor candidates near the player's feet more aggressively.

## Default Example
- `yaw_scan_min_deg = -90`
- `yaw_scan_max_deg = 90`
- `yaw_scan_step_deg = 5`
- `pitch_scan_min_deg = -45`
- `pitch_scan_max_deg = 0`
- `pitch_scan_step_deg = 15`

Default runtime order:
- yaw = `-90, -85, -80, ... , 90`
- for each yaw, pitch = `0, -15, -30, -45`

## Save and Restore
- Press `設定保存` to validate and apply the values.
- Valid settings are written to the session file and restored on the next GUI launch.
- Invalid settings stay only in the form and are not saved.

## Error Handling
- If the values are invalid, the GUI shows the reason directly under the settings form.
- The last valid runtime configuration remains active until a valid save succeeds.
