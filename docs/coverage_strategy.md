# Coverage Strategy

## Objective
- Push statement and branch coverage as high as practical without bypassing the GUI in integration tests.

## Order of Attack
1. Run GUI-origin live integration tests
2. Run GUI widget automation tests
3. Run config/session/error supplement tests
4. Generate coverage reports
5. Review uncovered code and classify it

## Classification of Uncovered Code
- GUI reachable but missing scenario
- GUI validation branch
- Session/config persistence branch
- Disconnected/timeout branch
- Retry/recovery branch
- Live-only branch requiring actual world participation
- Defensive fallback branch that is hard to reproduce safely

## Reporting Units
- GUI
- Agent
- Bridge client
- Search / movement / exploration
- Config
- Error handling / logging

## Tools
- `pytest`
- `coverage --branch`
- `tools/run_python_client_coverage.py`

## Artifacts
- `artifacts/coverage/summary.txt`
- `artifacts/coverage/coverage.xml`
- `artifacts/coverage/coverage.json`
- `artifacts/coverage/html/`
- `artifacts/coverage/module_summary.md`
