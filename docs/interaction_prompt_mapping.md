# Interaction Prompt Mapping

## Input

- `GET /api/get_interaction_context`
- `POST /api/command` with `Command: "get_interaction_context"`

## Output

- `HasFocusTarget`
- `CanInteractNow`
- `SuggestedActionKind`
- `PromptText`
- `TargetKind`
- `TargetName`
- `Distance`
- `Source`
- `RequiresPreciseAlignment`
- `RecommendedInteractDistanceMin`
- `RecommendedInteractDistanceMax`
- `Note`

## Rules

- Lootable containers prefer `search` or `loot`.
- Doors prefer `open` or `unlock`.
- NPC and trader targets prefer `talk`.
- Resource-like blocks prefer `mine` or `harvest`.
- Enemy targets prefer `attack`.
- Unknown prompt text falls back to `Unknown` instead of an empty string.

## Unknown Conditions

- `SuggestedActionKind` returns `unknown` only when the focus target exists but no stable action can be inferred.
- Recommended interaction distances can return `unknown` if the target is absent.

## Known Limitations

- The prompt text is reconstructed from internal state and heuristics when the raw XUi prompt text is unavailable.
- `CanInteractNow` is distance-aware but does not guarantee that the game will accept the interaction if additional game-side constraints apply.
