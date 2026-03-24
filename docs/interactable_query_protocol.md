# Interactable Query Protocol

## Input

- `GET /api/query_interactables_in_radius`
- `POST /api/command` with `Command: "query_interactables_in_radius"`

Supported parameters:

- `x`, `y`, `z` or `center`
- `radius`
- `max_results`
- `include_blocks`
- `include_entities`
- `include_loot`
- `include_doors`
- `include_vehicles`
- `include_npcs`
- `include_traders`
- `include_locked`

## Output

- `Center`
- `Radius`
- `MaxResults`
- `Count`
- `IgnoredFilters`
- `Interactables[]`
  - `Kind`
  - `Id`
  - `Name`
  - `Position`
  - `Distance`
  - `CanInteract`
  - `InteractionPromptText`
  - `InteractionActionKind`
  - `Locked`
  - `Powered`
  - `Active`
  - `LineOfSightClear`
  - `Note`

## Rules

- Nearby blocks and entities are evaluated separately and merged into a distance-sorted result list.
- Loot containers, doors, vehicles, NPCs, and traders are preserved as explicit categories when possible.
- `InteractionPromptText` is never empty; it falls back to `Unknown`.

## Unknown Conditions

- `Locked`, `Powered`, `Active`, and `LineOfSightClear` may be `unknown` if the game object does not expose them.
- Some neutral entities can appear as generic interactables when they are not strongly typed by the game.

## Known Limitations

- Quest-log-specific or custom modded interactables depend on their internal block/entity naming.
- The API is intended as a local awareness query, not a full world scan.
