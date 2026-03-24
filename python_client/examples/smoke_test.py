from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
if str(SRC) not in sys.path:
    sys.path.insert(0, str(SRC))

from sevendays_bridge import SevenDaysBridgeClient  # noqa: E402


def main() -> int:
    client = SevenDaysBridgeClient()

    ping = client.ping()
    version = client.get_version()
    state = client.get_state()
    position = client.get_player_position()
    rotation = client.get_player_rotation()
    capabilities = client.get_capabilities()
    logs = client.get_logs_tail(20)

    print("== ping ==")
    print(json.dumps(ping, indent=2))
    print("== version ==")
    print(version)
    print("== state ==")
    print(state)
    print("== position ==")
    print(position)
    print("== rotation ==")
    print(rotation)
    print("== capabilities ==")
    print(capabilities)
    print("== logs tail ==")
    for line in logs.lines:
        print(line)

    if ping.get("message") != "pong":
        raise SystemExit("ping failed: expected pong")
    if version.bridge_version != "0.5.0.0":
        raise SystemExit(f"unexpected bridge version: {version.bridge_version}")
    if capabilities.phase != "phase4":
        raise SystemExit(f"unexpected capabilities phase: {capabilities.phase}")
    if position.available and state.player and state.player.position is None:
        raise SystemExit("state.position should be populated when position endpoint reports available")
    if "stop_all_input" not in capabilities.actions:
        raise SystemExit("stop_all_input is missing from capabilities.actions")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
