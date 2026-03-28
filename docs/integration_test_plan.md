# Integration Test Plan

## Goal
- Verify the real integration path `GUI -> Agent -> Bridge -> Game`.
- Keep GUI-origin tests as the primary integration layer.
- Use automated supplementary tests only for branches that are impractical to reach in live runs.

## Layers
- Layer A: GUI-origin live integration tests
  - Visible GUI
  - Real `SevenDaysBridgeClient`
  - Real `AgentController`
  - Real game process
- Layer B: GUI-origin automated tests
  - Tk widgets are driven through `AgentGuiAutomationDriver`
  - No controller or bridge shortcuts in the test actions
- Layer C: Coverage supplement
  - Config/session restore
  - Disconnected bridge handling
  - Validation and error branches

## Minimum Scenarios
1. GUI startup and status rendering
2. Bridge disconnected handling
3. Agent start / stop / reset
4. Command add / edit / reorder / pause / resume / cancel / interrupt
5. Base add / select / build enqueue
6. Scan setting save / invalid input / reload / restore
7. Live connected wait command completion
8. Logs and error rendering

## Pass Criteria
- GUI does not crash
- Connection state is rendered correctly
- GUI operations change the command queue as expected
- Agent start/stop is reflected in the GUI
- Saved settings survive restart
- Live GUI scenario completes at least one command through the real bridge path

## Failure Recording
- Record the failed scenario name
- Record visible GUI state
- Record logs shown in the panel
- Record whether the failure happened before bridge, inside bridge, or after game input
