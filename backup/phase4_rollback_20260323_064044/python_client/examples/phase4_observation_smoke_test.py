from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
if str(SRC) not in sys.path:
    sys.path.insert(0, str(SRC))

from sevendays_bridge import SevenDaysBridgeClient  # noqa: E402


def dump(label: str, value) -> None:
    print(f"== {label} ==")
    if hasattr(value, "__dict__"):
        print(json.dumps(value, default=lambda obj: obj.__dict__, indent=2))
    else:
        print(value)


def main() -> int:
    client = SevenDaysBridgeClient()

    dump("look_target", client.get_look_target())
    dump("interaction_context", client.get_interaction_context())
    dump("resource_candidates", client.query_resource_candidates(radius=10, max_results=5, include_surface_only=True))
    dump("interactables", client.query_interactables_in_radius(radius=8, max_results=5))
    dump("entities", client.query_entities_in_radius(radius=16, max_results=8))
    dump("environment_summary", client.get_environment_summary())
    dump("biome_info", client.get_biome_info())
    dump("terrain_summary", client.get_terrain_summary())

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
