# Configuration

## Scan Settings
Exploration scan settings are configurable from the GUI and persisted across restarts.

### Yaw
- `yaw_scan_min_deg`
- `yaw_scan_max_deg`
- `yaw_scan_step_deg`

Yaw is always generated from `min` to `max` in ascending order.

Default values:
- `yaw_scan_min_deg = -90`
- `yaw_scan_max_deg = 90`
- `yaw_scan_step_deg = 5`

Default generated yaw order:
- `-90, -85, -80, ... , 85, 90`

### Pitch
- `pitch_scan_min_deg`
- `pitch_scan_max_deg`
- `pitch_scan_step_deg`

Pitch is always executed from `0` downward. The GUI stores min/max/step, but the runtime scan order is generated as:
- `0`
- `-step`
- `-2 * step`
- ...
- `pitch_scan_min_deg`

Default values:
- `pitch_scan_min_deg = -45`
- `pitch_scan_max_deg = 0`
- `pitch_scan_step_deg = 15`

Default generated pitch order:
- `0, -15, -30, -45`

### Additional Scan Controls
- `scan_settle_delay_ms`
- `near_ground_priority_enabled`
- `near_ground_distance_threshold`

These values control how long the scanner waits after moving the camera and how strongly near-ground candidates are prioritized.

## Validation Rules

### Yaw
- `yaw_scan_step_deg > 0`
- `yaw_scan_min_deg < yaw_scan_max_deg`
- `yaw_scan_min_deg >= -180`
- `yaw_scan_max_deg <= 180`

### Pitch
- `pitch_scan_step_deg > 0`
- `pitch_scan_min_deg <= 0`
- `pitch_scan_max_deg >= 0`
- `pitch_scan_min_deg >= -89`
- `pitch_scan_max_deg <= 89`

### Invalid Values
- Invalid values are rejected in the GUI.
- They are not silently clamped.
- They are not saved.
- The GUI shows the validation error directly.

## Persistence
- Scan settings are stored in the GUI session file.
- Missing settings are filled with defaults on load.
- Older session files remain compatible because missing fields are defaulted.
