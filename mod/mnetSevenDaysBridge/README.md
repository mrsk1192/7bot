# mnetSevenDaysBridge Mod

Phase 2 では、localhost 限定の制御ブリッジに包括的な操作 IF と主要な入力注入を追加しました。

## 採用方式

- 通信は `127.0.0.1` 限定の HTTP + JSON
- エントリーポイントは `IModApi.InitMod(Mod)`
- 入力適用は Unity メインスレッド上の `BridgeRuntimeBehaviour.Update()`

## 主な Phase 2 API

- `GET /api/ping`
- `GET /api/get_version`
- `GET /api/get_capabilities`
- `GET /api/get_state`
- `GET /api/get_player_position`
- `GET /api/get_player_rotation`
- `GET /api/get_logs_tail?lines=50`
- `POST /api/command`

`POST /api/command` には `move_forward_start` や `stop_all_input` などの action 名をそのまま渡します。

## ビルド

```powershell
.\tools\build_mod.ps1 -GameDir "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die"
```

## 配置

```powershell
.\tools\copy_to_game_mods.ps1 -GameDir "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die"
```
