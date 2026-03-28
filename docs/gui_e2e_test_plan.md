# GUI E2E Test Plan

## Principle
- Every automated GUI test must operate through widgets.
- Tests use `AgentGuiAutomationDriver` to click buttons, edit entries, select combobox items, and select tree rows.
- Tests must not call controller actions directly as the test step itself.

## Automated GUI Scenarios
1. Start / stop runtime and verify status labels
2. Add a move command from the command form
3. Pause / resume / interrupt / cancel the selected command
4. Add a base and enqueue a build command
5. Save scan settings and verify they affect runtime config
6. Submit invalid scan settings and verify GUI error label
7. Reset agent and verify queue is cleared
8. Disconnected bridge state remains visible without GUI crash

## Live GUI Scenario
- Script: `python_client/examples/live_gui_integration_test.py`
- Flow:
  - launch or attach to the game
  - wait until GUI shows `接続済み`
  - click `エージェント開始`
  - save valid scan settings
  - submit invalid scan settings and verify visible error
  - enqueue a `待機` command
  - wait until command status becomes `完了`
  - click `エージェント停止`

## Evidence
- GUI label snapshots
- Queue row snapshots
- Generated JSON report under `artifacts/`
