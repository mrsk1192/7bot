# Obstacle Recovery Rules (Phase 4.5)

## Trigger Conditions
Obstacle recovery starts only when deterministic stagnation rules are satisfied.

Recovery is triggered when all of the following hold:
- at least 2 seconds of approach work have elapsed
- player progress is under 0.5 m
- target distance has not shrunk enough

It can also trigger when heading correction alone continues for 3 seconds without convergence.

## Fixed Recovery Sequence
Recovery order is always fixed. No random branch order is allowed.

1. `stop_all_input`
2. backward walk for 0.4 seconds
3. left turn by 20 degrees
4. forward walk for 0.8 seconds
5. `stop_all_input` and realign to target
6. if still blocked, right turn by 20 degrees and forward walk for 0.8 seconds
7. if still blocked, evaluate `JumpDecision`
8. only if `JumpDecision` returns true, execute one forward jump
9. repeat recovery at most 3 times per target
10. if still blocked, mark target as `unreachable`

## Recovery Guarantees
- Recovery always begins with `stop_all_input`
- Left and right recovery order never changes
- Jump is considered only after both left and right recovery have failed
- Jump is attempted at most once per target
