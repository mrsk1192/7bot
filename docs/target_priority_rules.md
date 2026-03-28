# Target Priority Rules

## Candidate Categories
- `loot_candidate`
- `resource_candidate`
- `interactable_candidate`
- `hostile_candidate`
- `npc_candidate`
- `ignore_candidate`

## Base Priority
- `loot_candidate`: +100
- `resource_candidate`: +80
- `interactable_candidate`: +60
- `npc_candidate`: +30
- `hostile_candidate`: -200
- `ignore_candidate`: -500

## Additional Scoring
- `can_interact`: +30
- `distance`: subtract 1 point per meter
- `candidate_confidence`: `confidence * 20`
- `line_of_sight_clear=true`: +10
- repeated target penalty: -40
- unreachable penalty: -200
- hostile nearby penalty for non-hostile gather targets: -25

## Selection Rules
1. can-interact loot/container in view or query
2. near resource candidate
3. can-interact interactable
4. mid-distance resource
5. npc candidate
6. hostile candidate is not a gather target and instead diverts to avoidance

## Failure Handling
- recently processed targets receive repeat penalty
- unreachable targets are skipped during cooldown
- empty loot and failed mine/interact results are remembered to prevent loops
