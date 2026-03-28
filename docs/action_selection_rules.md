# Action Selection Rules

## Inputs
- classified target
- target distance
- can_interact
- action kind
- hostile flag

## Outputs
- action plan kind
- command name
- required approach flag
- required alignment flag
- stop distance

## Rules
- `loot_candidate`
  - command: `use_interact`
  - requires approach when out of loot stop distance or when `can_interact=false`
  - requires alignment
- `resource_candidate`
  - command: `primary_action`
  - requires approach when out of resource stop distance
  - requires alignment
- `interactable_candidate`
  - command: `use_interact`
  - requires approach when out of interact distance or when `can_interact=false`
  - requires alignment
- `hostile_candidate`
  - command: `avoid_hostile`
  - stops gathering and exploration
- `npc_candidate`
  - no repeated talk loop in Phase 5
  - observation only unless a future phase expands it

## Failure Handling
- if action verification fails, transition to `RECOVER`
- if recovery cannot restore progress, transition to `FAILED` and call `stop_all_input`
