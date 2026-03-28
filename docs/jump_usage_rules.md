# Jump Usage Rules (Phase 4.5)

## Principle
Jumping is forbidden by default.

Phase 4.5 navigation is walk-first. Jump is allowed only as a last-resort bypass during obstacle recovery and only when the current obstacle is judged jumpable with explicit rules.

## Required Conditions
All of the following must be true before a jump is allowed:
1. there is a real forward obstacle
2. left and right recovery paths have already failed
3. obstacle height is known
4. obstacle height is not greater than `max_jumpable_obstacle_height`
5. required forward space before jump is available
6. required landing clearance is available
7. there is no fall hazard ahead
8. jump has not already been attempted for the current target

If any of these conditions is unknown, the result is `do not jump`.

## Required Parameters
- `max_jumpable_obstacle_height`
- `required_landing_clearance`
- `required_forward_space_before_jump`
- `jump_forward_press_delay_ms`
- `jump_max_attempts_per_target`

## Execution Rules
When a jump is approved:
1. face the target direction
2. start forward movement
3. wait `jump_forward_press_delay_ms`
4. execute exactly one jump input
5. continue only long enough to clear the obstacle
6. stop and re-evaluate

## Forbidden Patterns
- jump by itself
- jump in place
- repeated jump spam
- jump for exploration speed
- jump on unknown terrain
