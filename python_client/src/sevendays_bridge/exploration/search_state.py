from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Optional, Tuple


class SearchState(str, Enum):
    UNKNOWN = "unknown"
    SCHEDULED = "scheduled"
    VISITED = "visited"
    SCANNED = "scanned"
    CANDIDATE_FOUND = "candidate_found"
    BLOCKED = "blocked"
    UNREACHABLE = "unreachable"


@dataclass
class CellRecord:
    coords: Tuple[int, int]
    state: SearchState = SearchState.UNKNOWN
    last_updated_monotonic: float = 0.0
    visit_count: int = 0
    last_scan_monotonic: Optional[float] = None

