from __future__ import annotations

import time
from dataclasses import dataclass, field
from typing import Dict


@dataclass
class ResultMemory:
    unreachable_targets: Dict[str, float] = field(default_factory=dict)
    empty_loot_targets: Dict[str, float] = field(default_factory=dict)
    failed_interact_targets: Dict[str, float] = field(default_factory=dict)
    failed_mine_targets: Dict[str, float] = field(default_factory=dict)
    recently_processed_targets: Dict[str, float] = field(default_factory=dict)
    recently_visited_cells: Dict[str, float] = field(default_factory=dict)

    def mark_processed(self, target_key: str) -> None:
        self.recently_processed_targets[target_key] = time.monotonic()

    def mark_unreachable(self, target_key: str) -> None:
        self.unreachable_targets[target_key] = time.monotonic()
        self.mark_processed(target_key)

    def mark_empty_loot(self, target_key: str) -> None:
        self.empty_loot_targets[target_key] = time.monotonic()
        self.mark_processed(target_key)

    def mark_failed_interact(self, target_key: str) -> None:
        self.failed_interact_targets[target_key] = time.monotonic()
        self.mark_processed(target_key)

    def mark_failed_mine(self, target_key: str) -> None:
        self.failed_mine_targets[target_key] = time.monotonic()
        self.mark_processed(target_key)

    def mark_cell_visited(self, cell_key: str) -> None:
        self.recently_visited_cells[cell_key] = time.monotonic()

    def get_repeat_penalty(self, target_key: str, cooldown_sec: float = 15.0) -> float:
        seen_at = self.recently_processed_targets.get(target_key)
        if seen_at is None:
            return 0.0
        elapsed = time.monotonic() - seen_at
        if elapsed >= cooldown_sec:
            return 0.0
        return 40.0

    def get_unreachable_penalty(self, target_key: str, cooldown_sec: float = 120.0) -> float:
        seen_at = self.unreachable_targets.get(target_key)
        if seen_at is None:
            return 0.0
        elapsed = time.monotonic() - seen_at
        if elapsed >= cooldown_sec:
            return 0.0
        return 200.0

    def should_skip_target(self, target_key: str) -> bool:
        return self.get_unreachable_penalty(target_key) > 0.0
