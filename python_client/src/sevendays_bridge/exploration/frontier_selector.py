from __future__ import annotations

import math
from typing import Optional, Tuple

from sevendays_bridge.exploration.search_grid import SearchGrid
from sevendays_bridge.exploration.search_state import SearchState
from sevendays_bridge.movement.navigation_config import NavigationConfig


class FrontierSelector:
    """Deterministic frontier selection with forward-biased neighbor priority."""

    def __init__(self, grid: SearchGrid, config: Optional[NavigationConfig] = None):
        self.grid = grid
        self.config = config or NavigationConfig()

    @staticmethod
    def _heading_bucket(yaw_deg: float) -> Tuple[int, int]:
        directions = (
            (0, 1),
            (-1, 1),
            (1, 1),
            (-1, 0),
            (1, 0),
            (0, -1),
            (-1, -1),
            (1, -1),
        )
        best = (0, 1)
        best_dot = -999.0
        yaw_rad = math.radians(yaw_deg)
        fx = math.sin(yaw_rad)
        fz = math.cos(yaw_rad)
        for dx, dz in directions:
            length = math.sqrt((dx * dx) + (dz * dz))
            dot = (dx / length) * fx + (dz / length) * fz
            if dot > best_dot:
                best_dot = dot
                best = (dx, dz)
        return best

    @staticmethod
    def _left_of(dx: int, dz: int) -> Tuple[int, int]:
        return (-dz, dx)

    @staticmethod
    def _right_of(dx: int, dz: int) -> Tuple[int, int]:
        return (dz, -dx)

    def _ordered_neighbor_offsets(self, yaw_deg: float):
        fdx, fdz = self._heading_bucket(yaw_deg)
        left_dx, left_dz = self._left_of(fdx, fdz)
        right_dx, right_dz = self._right_of(fdx, fdz)
        return (
            (fdx, fdz),
            (fdx + left_dx, fdz + left_dz),
            (fdx + right_dx, fdz + right_dz),
            (left_dx, left_dz),
            (right_dx, right_dz),
            (-fdx, -fdz),
        )

    def get_next_cell(self, current_x: float, current_z: float, current_yaw_deg: float) -> Optional[Tuple[int, int]]:
        cx, cz = self.grid.get_cell_coords(current_x, current_z)
        self.grid.ensure_adjacent_cells(cx, cz)

        for dx, dz in self._ordered_neighbor_offsets(current_yaw_deg):
            nx, nz = cx + dx, cz + dz
            if self.grid.get_state(nx, nz) == SearchState.UNKNOWN:
                return (nx, nz)

        nearest = None
        nearest_distance = float("inf")
        for record in self.grid.iter_cells_by_state(SearchState.UNKNOWN):
            rx, rz = record.coords
            distance = ((rx - cx) ** 2) + ((rz - cz) ** 2)
            if distance < nearest_distance:
                nearest_distance = distance
                nearest = record.coords
        return nearest
