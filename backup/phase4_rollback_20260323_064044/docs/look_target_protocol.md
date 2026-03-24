# Look Target Protocol

## Input

- `GET /api/get_look_target`
- `POST /api/command` with `Command: "get_look_target"`

## Output

- `HasTarget`
- `Source`
- `TargetKind`
- `TargetName`
- `TargetClass`
- `TargetId`
- `EntityId`
- `BlockId`
- `Distance`
- `Position`
- `CanInteract`
- `InteractionPromptText`
- `InteractionActionKind`
- `Hostile`
- `Alive`
- `Locked`
- `Powered`
- `Active`
- `LineOfSightClear`
- `IsResourceCandidate`
- `CandidateCategory`
- `CandidateConfidence`
- `LikelyResourceType`
- `Durability`
- `MaxDurability`
- `Note`

## Rules

- The bridge uses the player look ray first.
- If a nearby entity matches the hit point, the target is classified as entity-derived.
- Otherwise the impacted block is classified using block name, tile entity, and interaction heuristics.
- `InteractionPromptText` is never empty. Unknown values are returned as `Unknown`.
- `TargetKind` prefers the nearest non-unknown category rather than dropping to `unknown` early.

## Unknown Conditions

- `Locked`, `Powered`, `Active`, `LineOfSightClear`, `Durability`, and `MaxDurability` return `unknown` when the game object does not expose the underlying value.
- `TargetKind` returns `none` only when no target is present. It should not be used for ambiguous hits.

## Known Limitations

- Focus classification is heuristic for blocks that do not expose explicit loot/interactable metadata.
- `LineOfSightClear` is physics-based and can return `unknown` if the world physics query fails.
