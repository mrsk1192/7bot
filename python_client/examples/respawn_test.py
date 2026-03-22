from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
if str(SRC) not in sys.path:
    sys.path.insert(0, str(SRC))

from sevendays_bridge import BridgeApiError, SevenDaysBridgeClient  # noqa: E402


def main() -> int:
    client = SevenDaysBridgeClient(timeout_seconds=10.0)
    state = client.get_state()

    print("== initial state ==")
    print(state)

    if not state.player or not state.player.is_dead:
        raise SystemExit("respawn_test.py expects the player to already be dead.")

    print("== wait_for_respawn_screen ==")
    print(client.wait_for_respawn_screen())

    try:
        print("== respawn_default ==")
        print(client.respawn_default())
    except BridgeApiError as exc:
        print("respawn_default failed, trying explicit confirm fallback:", exc)
        print("== respawn_confirm ==")
        print(client.respawn_confirm())

    print("== wait_until_respawned ==")
    print(client.wait_until_respawned())

    final_state = client.get_state()
    print("== final state ==")
    print(final_state)

    if not final_state.player or not final_state.player.alive:
        raise SystemExit("respawn did not complete successfully.")

    input_state = final_state.input_state
    if (
        input_state.move_forward
        or input_state.move_back
        or input_state.move_left
        or input_state.move_right
        or input_state.sprint
        or input_state.crouch
        or input_state.primary_action
        or input_state.secondary_action
        or input_state.hold_interact
    ):
        raise SystemExit("input state was not neutral after respawn.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
