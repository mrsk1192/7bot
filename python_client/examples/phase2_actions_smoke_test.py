from __future__ import annotations

import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
if str(SRC) not in sys.path:
    sys.path.insert(0, str(SRC))

from sevendays_bridge import BridgeApiError, SevenDaysBridgeClient  # noqa: E402


def try_action(label: str, func) -> None:
    print(f"== {label} ==")
    try:
        result = func()
    except BridgeApiError as exc:
        print(f"SKIPPED/FAILED: {exc}")
        return
    print(result)


def main() -> int:
    client = SevenDaysBridgeClient()
    capabilities = client.get_capabilities()
    print(f"active_backend={capabilities.active_backend}")

    try_action("stop_all_input (pre)", client.stop_all)

    try_action("forward start", lambda: client.move_forward(True))
    time.sleep(1.0)
    try_action("forward stop", lambda: client.move_forward(False))

    try_action("forward start", lambda: client.move_forward(True))
    try_action("right start", lambda: client.press("move_right"))
    time.sleep(1.0)
    try_action("stop_all_input", client.stop_all)

    try_action("sprint start", lambda: client.sprint(True))
    time.sleep(0.3)
    try_action("sprint stop", lambda: client.sprint(False))

    try_action("crouch on", lambda: client.crouch(True))
    time.sleep(0.3)
    try_action("crouch off", lambda: client.crouch(False))

    try_action("look_delta", lambda: client.look_delta(25.0, -5.0))
    time.sleep(0.2)

    try_action("primary_action 300ms", lambda: client.attack_primary(duration_ms=300))
    time.sleep(0.2)

    try_action("reload", client.reload)
    time.sleep(0.2)

    try_action("interact", client.interact)
    time.sleep(0.2)

    try_action("hotbar 1", lambda: client.select_hotbar_slot(1))
    time.sleep(0.2)
    try_action("hotbar 2", lambda: client.select_hotbar_slot(2))
    time.sleep(0.2)
    try_action("hotbar 1", lambda: client.select_hotbar_slot(1))
    time.sleep(0.2)

    try_action("inventory toggle", client.open_inventory)
    time.sleep(0.5)
    try_action("inventory toggle", client.open_inventory)
    time.sleep(0.2)

    try_action("map toggle", client.open_map)
    time.sleep(0.2)

    try_action("flashlight toggle", client.toggle_flashlight)
    time.sleep(0.2)

    try_action("stop_all_input (post)", client.stop_all)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
