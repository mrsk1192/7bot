from __future__ import annotations

import math
import time
from typing import Dict, Iterator, Tuple

from .search_state import CellRecord, SearchState


class SearchGrid:
    """Cell manager for deterministic 10m x 10m exploration."""

    def __init__(self, cell_size: float = 10.0):
        self.cell_size = cell_size
        self.cells: Dict[Tuple[int, int], CellRecord] = {}

    def get_cell_coords(self, x: float, z: float) -> Tuple[int, int]:
        return (math.floor(x / self.cell_size), math.floor(z / self.cell_size))

    def get_cell_center(self, cx: int, cz: int) -> Tuple[float, float]:
        return ((cx * self.cell_size) + (self.cell_size / 2.0), (cz * self.cell_size) + (self.cell_size / 2.0))

    def get_record(self, cx: int, cz: int) -> CellRecord:
        coords = (cx, cz)
        if coords not in self.cells:
            self.cells[coords] = CellRecord(coords=coords)
        return self.cells[coords]

    def get_state(self, cx: int, cz: int) -> SearchState:
        return self.get_record(cx, cz).state

    def set_state(self, cx: int, cz: int, state: SearchState) -> None:
        record = self.get_record(cx, cz)
        record.state = state
        record.last_updated_monotonic = time.monotonic()
        if state == SearchState.SCANNED:
            record.last_scan_monotonic = record.last_updated_monotonic
        if state == SearchState.VISITED:
            record.visit_count += 1

    def mark_candidate_found(self, cx: int, cz: int) -> None:
        self.set_state(cx, cz, SearchState.CANDIDATE_FOUND)

    def ensure_adjacent_cells(self, cx: int, cz: int) -> None:
        for dx, dz in ((0, 1), (-1, 1), (1, 1), (-1, 0), (1, 0), (0, -1), (-1, -1), (1, -1)):
            self.get_record(cx + dx, cz + dz)

    def iter_cells_by_state(self, state: SearchState) -> Iterator[CellRecord]:
        for record in self.cells.values():
            if record.state == state:
                yield record

