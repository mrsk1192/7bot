from __future__ import annotations

import time
from typing import Optional

from .navigation_config import NavigationConfig


class LocomotionController:
    """Walking-only locomotion controller with explicit pressed-state tracking."""

    def __init__(self, client, config: Optional[NavigationConfig] = None):
        self._client = client
        self.config = config or NavigationConfig()
        self._pressed = {
            "move_forward": False,
            "move_back": False,
            "move_left": False,
            "move_right": False,
        }

    def _press(self, name: str) -> None:
        if not self._pressed.get(name, False):
            self._client.press(name)
            self._pressed[name] = True

    def _release(self, name: str) -> None:
        if self._pressed.get(name, False):
            self._client.release(name)
            self._pressed[name] = False

    def start_walk(self) -> None:
        self._release("move_back")
        self._press("move_forward")

    def stop(self) -> None:
        self._release("move_forward")
        self._release("move_back")
        self._release("move_left")
        self._release("move_right")

    def pulse_walk(self, duration_sec: Optional[float] = None) -> None:
        duration = self.config.movement_pulse_turn_and_move_seconds if duration_sec is None else duration_sec
        self.start_walk()
        time.sleep(duration)
        self.stop()

    def emergency_stop(self) -> None:
        self._client.stop_all()
        for key in list(self._pressed):
            self._pressed[key] = False

    def move_backward_pulse(self, duration_sec: float = 0.4) -> None:
        self.stop()
        self._press("move_back")
        time.sleep(duration_sec)
        self._release("move_back")

    def pulse_left(self, duration_sec: float = 0.2) -> None:
        self.stop()
        self._press("move_left")
        time.sleep(duration_sec)
        self._release("move_left")

    def pulse_right(self, duration_sec: float = 0.2) -> None:
        self.stop()
        self._press("move_right")
        time.sleep(duration_sec)
        self._release("move_right")

    def forward_jump(self, delay_ms: Optional[int] = None) -> None:
        delay = self.config.jump_forward_press_delay_ms if delay_ms is None else delay_ms
        self.start_walk()
        time.sleep(delay / 1000.0)
        self._client.jump()
        time.sleep(0.35)
        self.stop()
