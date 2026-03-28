# Search State Machine

## States
- `IDLE`
- `OBSERVE`
- `EVALUATE_LOOK_TARGET`
- `QUERY_CANDIDATES`
- `SELECT_TARGET`
- `MOVE_TO_TARGET`
- `ALIGN_VIEW`
- `ACT`
- `VERIFY_RESULT`
- `EXPLORE`
- `RECOVER`
- `AVOID_HOSTILE`
- `FAILED`

## Inputs
- `get_state`
- `get_look_target`
- `get_interaction_context`
- `query_resource_candidates`
- `query_interactables_in_radius`
- `query_entities_in_radius`
- `get_environment_summary`
- `get_biome_info`
- `get_terrain_summary`

## Outputs
- selected target
- action plan
- verification result
- recovery / failure reason

## Transition Rules
- `IDLE -> OBSERVE`
- `OBSERVE -> EVALUATE_LOOK_TARGET`
- `EVALUATE_LOOK_TARGET -> SELECT_TARGET` when immediate look target exists
- `EVALUATE_LOOK_TARGET -> QUERY_CANDIDATES` otherwise
- `QUERY_CANDIDATES -> SELECT_TARGET`
- `SELECT_TARGET -> MOVE_TO_TARGET` when a target exists
- `SELECT_TARGET -> EXPLORE` when no target exists
- `MOVE_TO_TARGET -> ALIGN_VIEW` when approach succeeds
- `MOVE_TO_TARGET -> RECOVER` when target is unreachable
- `ALIGN_VIEW -> ACT` when look target converges
- `ALIGN_VIEW -> RECOVER` when alignment fails
- `ACT -> VERIFY_RESULT`
- `VERIFY_RESULT -> OBSERVE` when action succeeds
- `VERIFY_RESULT -> RECOVER` when action verification fails
- `EXPLORE -> SELECT_TARGET` when a candidate is discovered
- `EXPLORE -> FAILED` when nothing can be found
- `RECOVER -> FAILED` when recovery reports unreachable
- `AVOID_HOSTILE -> OBSERVE` on next loop

## Explore Scan Detail
- `EXPLORE` uses the systematic sector scan before frontier expansion.
- Sector scan walks yaw from `-90` to `+90` in 5-degree steps.
- For each yaw, pitch is processed in this exact order: `0`, `-15`, `-30`, `-45`.
- Downward pitch checks exist specifically to reduce missed loot, interactables, and resources near the player's feet.

## Failure Handling
- `FAILED` always calls `stop_all_input`
- unreachable targets are recorded in result memory
- failed interact / mine targets are penalized for a cooldown
