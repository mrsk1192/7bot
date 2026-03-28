# Test Execution Guide

## 1. GUI Widget Automation
Run:

```powershell
python -m pytest python_client/tests/test_gui_widget_e2e.py -q
```

What it covers:
- GUI widget operations
- Command queue controls
- Base/build enqueue
- Scan setting validation
- Disconnected bridge rendering

## 2. Example Smoke Scripts
Run:

```powershell
python python_client/examples/gui_only_function_test.py
python python_client/examples/agent_control_runtime_smoke_test.py
```

## 3. Live GUI Integration
If the game should be launched by the test:

```powershell
python python_client/examples/live_gui_integration_test.py --launch-game
```

If the game is already running:

```powershell
python python_client/examples/live_gui_integration_test.py
```

Output:
- JSON report under `artifacts/live_gui_integration_report.json`

## 4. Coverage
Run:

```powershell
python tools/run_python_client_coverage.py
```

Artifacts:
- `artifacts/coverage/summary.txt`
- `artifacts/coverage/module_summary.md`
- `artifacts/coverage/coverage.xml`
- `artifacts/coverage/coverage.json`
- `artifacts/coverage/html/index.html`

## 5. Notes
- GUI-origin tests must use widget operations as the action entry.
- World participation may be prepared outside the GUI if needed, but validation after that should still be performed through the GUI.
