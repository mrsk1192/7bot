# Systematic Exploration Rules (Phase 4.5+)

## Grid Rules
- The search space is divided into `10m x 10m` cells.
- Cell size is configurable through `cell_size_meters`.
- Each cell has one explicit state:
  - `unknown`
  - `scheduled`
  - `visited`
  - `scanned`
  - `candidate_found`
  - `blocked`
  - `unreachable`

## Frontier Selection Rules
When choosing the next cell, the selector uses fixed priority:
1. forward adjacent cell
2. left-forward adjacent cell
3. right-forward adjacent cell
4. left adjacent cell
5. right adjacent cell
6. backward adjacent cell
7. nearest remaining `unknown` cell

No random ordering is allowed.

## Local Search First
Before grid expansion, the explorer performs a local query pass in a 20 to 30 meter neighborhood.

The local pass checks:
- `query_interactables_in_radius`
- `query_resource_candidates`
- `query_entities_in_radius`

If a candidate is found, grid expansion stops immediately.

## Sector Scan Rules
If local query does not yield a candidate, the current cell is scanned with a yaw-first, pitch-second order generated from configuration.

### Yaw Order
Yaw runs from `yaw_scan_min_deg` to `yaw_scan_max_deg` in exact `yaw_scan_step_deg` increments.

Default example:
- `yaw_scan_min_deg = -90`
- `yaw_scan_max_deg = 90`
- `yaw_scan_step_deg = 5`
- generated yaw order: `-90, -85, -80, ... , 85, 90`

The scan must not reorder this generated list, sort it, or switch back to alternating left/right traversal.

### Pitch Order
For every single yaw angle, pitch is generated from configuration but always executed from `0` downward.

Default example:
- `pitch_scan_min_deg = -45`
- `pitch_scan_max_deg = 0`
- `pitch_scan_step_deg = 15`
- generated pitch order: `0, -15, -30, -45`

Even if the GUI stores a `pitch_scan_max_deg`, the default exploration scan does not use upward scan angles. Pitch is always processed after yaw is set, and all generated pitch values must be checked before moving to the next yaw.

### Per-View Queries
At every single yaw/pitch view, the scan must call:
- `get_look_target`
- `get_interaction_context`
- `query_resource_candidates(radius=10)`
- `query_interactables_in_radius(radius=10)`
- `query_entities_in_radius(radius=10)`

### Settle Rule
After every yaw/pitch view change, the explorer waits `scan_settle_delay_ms` before running the observation queries. This prevents querying while the view is still moving.

### Near-Ground Priority Rule
The downward pitch sweep exists to reduce missed loot, resources, and interactables near the player's feet.
- Near-ground candidates found within `near_ground_distance_threshold` are promoted for follow-up processing when `near_ground_priority_enabled=true`.
- A single `pitch=0` miss is not enough to conclude that no nearby candidate exists.
- If a yaw direction contains nearby interactables or resource candidates, the downward pitch values for that yaw are still processed in order.

### Early Stop Rule
The scan may stop early only after the current yaw has finished all of its pitch checks, and only for one of these reasons:
1. a `can_interact=true` loot or container target was confirmed
2. a high-confidence resource candidate was confirmed
3. a near hostile was detected and the higher-level controller wants to stop exploration

The scan remains deterministic and must not revert to coarse 45-degree steps.

### Performance Trade-Off
- Smaller yaw and pitch steps increase exploration accuracy.
- Smaller steps also increase view changes and query calls.
- The default settings use `yaw=5 degrees` and `pitch=15 degrees` as a balance between near-ground detection and runtime cost.

## Candidate Priority Rules
Candidates are ranked in this order:
1. `can_interact=true` loot or container
2. near resource candidate
3. `can_interact=true` interactable
4. medium-distance resource
5. lower-confidence candidate

## Cell State Progression
- A new frontier cell becomes `scheduled`
- If approached successfully, it becomes `visited`
- After the yaw/pitch scan completes, it becomes `scanned`
- If a useful target is found inside the cell, it becomes `candidate_found`
- If approach fails, it becomes `unreachable`

Scanned cells are not re-prioritized ahead of unknown cells.
