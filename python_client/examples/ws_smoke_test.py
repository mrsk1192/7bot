"""WebSocket smoke test — verifies the bridge WS push server is alive and pinging."""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
if str(SRC) not in sys.path:
    sys.path.insert(0, str(SRC))

from sevendays_bridge import SevenDaysBridgeClient  # noqa: E402
from sevendays_bridge.ws_subscriber import WebSocketSubscriber  # noqa: E402


def main() -> int:
    # 1. Basic HTTP sanity check.
    client = SevenDaysBridgeClient()
    ping = client.ping()
    if ping.get("message") != "pong":
        raise SystemExit("HTTP ping failed")
    print("HTTP ping: OK")

    # 2. WebSocket connection + ping event.
    sub = WebSocketSubscriber()
    sub.connect()
    print("WebSocket connected: OK")

    try:
        event = sub.wait_for_event("ping", timeout_ms=8000)
    except Exception as exc:
        raise SystemExit(f"Did not receive WebSocket ping within 8s: {exc}")

    print(f"WebSocket ping event received: {event}")

    # 3. use_websocket integration on the client.
    ws_client = SevenDaysBridgeClient(use_websocket=True)
    state = ws_client.get_state()
    print(f"get_state via ws_client: player_available={state.player is not None}")

    sub.close()
    print("All WebSocket smoke tests passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
