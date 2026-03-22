# Python Client

Phase 2 の Python クライアントです。`127.0.0.1:18771` に HTTP + JSON で接続します。

## セットアップ

```powershell
cd python_client
python -m pip install -e .
```

## examples

```powershell
cd python_client
python .\examples\smoke_test.py
python .\examples\phase2_actions_smoke_test.py
```

## 高水準 API

- `press`
- `release`
- `tap`
- `look_delta`
- `look_to`
- `select_hotbar_slot`
- `stop_all`
- `move_forward`
- `sprint`
- `crouch`
- `jump`
- `attack_primary`
- `aim`
- `reload`
- `interact`
- `open_inventory`
- `open_map`
- `toggle_flashlight`
