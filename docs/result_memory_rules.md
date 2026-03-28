# Result Memory Rules

## Stored Results
- unreachable targets
- empty loot targets
- failed interact targets
- failed mine targets
- recently processed targets
- recently visited cells

## Inputs
- target key
- failure type
- cell key
- monotonic timestamp

## Outputs
- repeat penalty
- unreachable penalty
- skip / do not skip target

## Rules
- every processed target is timestamped
- processed targets get a short repeat penalty
- unreachable targets get a strong cooldown penalty
- empty loot is remembered so the same container is not spammed
- failed mine targets are penalized to avoid infinite retry on the same block
- recently visited cells are available for revisit suppression in search policies

## Failure Handling
- when `FAILED` is reached, `stop_all_input` is called first
- the failing target is stored in memory before the next cycle
