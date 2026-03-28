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


def ensure(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(message)


def main() -> int:
    client = SevenDaysBridgeClient()

    state = client.get_state()
    look_target = client.get_look_target()
    interaction_context = client.get_interaction_context()
    resource_candidates = client.query_resource_candidates(
        radius=10,
        max_results=5,
        include_surface_only=True,
        include_exposed_only=False,
        min_confidence=0.1,
        bogus_filter="ignored",
    )
    interactables = client.query_interactables_in_radius(
        radius=10,
        max_results=8,
        include_blocks=True,
        include_entities=True,
        include_loot=True,
        include_doors=True,
        include_vehicles=True,
        include_npcs=True,
        include_traders=True,
        include_locked=True,
        unsupported_filter="ignored",
    )
    entities = client.query_entities_in_radius(
        radius=24,
        max_results=10,
        include_hostile=True,
        include_npc=True,
        include_animals=True,
        include_neutral=True,
        include_dead=False,
        bogus="ignored",
    )
    environment_summary = client.get_environment_summary()
    biome_info = client.get_biome_info()
    terrain_summary = client.get_terrain_summary()

    dump("state", state)
    dump("look_target", look_target)
    dump("interaction_context", interaction_context)
    dump("resource_candidates", resource_candidates)
    dump("interactables", interactables)
    dump("entities", entities)
    dump("environment_summary", environment_summary)
    dump("biome_info", biome_info)
    dump("terrain_summary", terrain_summary)

    ensure(state.resource_observation is not None, "get_state.resource_observation is missing")
    ensure(state.nearby_resource_candidates_summary is not None, "get_state.nearby_resource_candidates_summary is missing")
    ensure(state.nearby_interactables_summary is not None, "get_state.nearby_interactables_summary is missing")
    ensure(state.nearby_entities_summary is not None, "get_state.nearby_entities_summary is missing")

    ensure(look_target.target_kind != "", "get_look_target.target_kind must not be empty")
    ensure(look_target.interaction_prompt_text != "", "get_look_target.interaction_prompt_text must not be empty")
    ensure(look_target.position is not None, "get_look_target.position must be present")

    ensure(interaction_context.suggested_action_kind != "", "interaction_context.suggested_action_kind must not be empty")
    ensure(interaction_context.prompt_text != "", "interaction_context.prompt_text must not be empty")
    ensure(interaction_context.target_kind != "", "interaction_context.target_kind must not be empty")

    ensure(resource_candidates.count >= 0, "resource query count must be non-negative")
    ensure(resource_candidates.max_results == 5, "resource query max_results should round-trip")
    ensure(resource_candidates.note != "", "resource query note must not be empty")

    ensure(interactables.count >= 0, "interactable query count must be non-negative")
    ensure(interactables.max_results == 8, "interactable query max_results should round-trip")
    ensure(interactables.note != "", "interactable query note must not be empty")

    ensure(entities.count >= 0, "entity query count must be non-negative")
    ensure(entities.max_results == 10, "entity query max_results should round-trip")
    ensure(entities.note != "", "entity query note must not be empty")

    ensure(environment_summary.current_biome != "", "environment_summary.current_biome must not be empty")
    ensure(environment_summary.foot_block_name != "", "environment_summary.foot_block_name must not be empty")
    ensure(biome_info.current_biome != "", "biome_info.current_biome must not be empty")
    ensure(biome_info.hazard_hint != "", "biome_info.hazard_hint must not be empty")
    ensure(terrain_summary.foot_block_name != "", "terrain_summary.foot_block_name must not be empty")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
