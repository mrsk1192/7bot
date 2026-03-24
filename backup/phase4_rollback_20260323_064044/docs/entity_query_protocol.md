# Entity Query Protocol

## Input

- `GET /api/query_entities_in_radius`
- `POST /api/command` with `Command: "query_entities_in_radius"`

Supported parameters:

- `x`, `y`, `z` or `center`
- `radius`
- `max_results`
- `include_hostile`
- `include_npc`
- `include_animals`
- `include_neutral`
- `include_dead`

## Output

- `Center`
- `Radius`
- `MaxResults`
- `Count`
- `IgnoredFilters`
- `Entities[]`
  - `EntityId`
  - `EntityName`
  - `EntityClass`
  - `Kind`
  - `Position`
  - `Distance`
  - `Alive`
  - `Hostile`
  - `CanInteract`
  - `CurrentTargetingPlayer`
  - `LineOfSightClear`
  - `Note`

## Rules

- Entities are collected from the game world bounds query.
- Hostile classification prioritizes zombies, bandits, and hostile animals.
- Traders and NPCs are separated from general neutral entities where possible.
- `include_dead=false` removes dead entities after classification.

## Unknown Conditions

- `CurrentTargetingPlayer` can be `unknown` when the entity does not expose its current target.
- `LineOfSightClear` can be `unknown` if the linecast fails or the player is unavailable.
- `Kind` remains `unknown` only when the entity name and class do not match any known heuristic bucket.

## Known Limitations

- Entity classification is heuristic for heavily modded NPC/enemy types.
- Vehicle-like entities exposed as blocks instead of entities will not appear here; use the interactable query for those.
