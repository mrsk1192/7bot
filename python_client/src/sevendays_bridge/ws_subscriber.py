"""Synchronous WebSocket event subscriber for the 7DTD bridge.

Connects to the WebSocket push server (default port 18772) and provides
two usage models:
  1. Blocking wait:   subscriber.wait_for_event("death", timeout_ms=30000)
  2. Callback-based: subscriber.subscribe("respawn_complete", my_callback)

Example::

    sub = WebSocketSubscriber()
    sub.connect()
    event = sub.wait_for_event("respawn_complete", timeout_ms=30_000)
    sub.close()
"""
from __future__ import annotations

import json
import queue
import threading
from typing import Callable, Dict, Optional

import websocket  # websocket-client package


class WebSocketSubscriberError(Exception):
    """Raised when the WebSocket subscriber encounters an unrecoverable error."""


class WebSocketSubscriber:
    """Synchronous WebSocket client for the 7DTD bridge push server.

    Thread-safe: `wait_for_event` can be called from any thread while the
    background receive loop runs.
    """

    def __init__(self) -> None:
        self._ws: Optional[websocket.WebSocketApp] = None
        self._thread: Optional[threading.Thread] = None
        self._lock = threading.Lock()
        self._callbacks: Dict[str, list[Callable]] = {}
        self._event_queue: queue.Queue = queue.Queue()
        self._connected = threading.Event()
        self._error: Optional[Exception] = None

    # ------------------------------------------------------------------
    # Connection lifecycle
    # ------------------------------------------------------------------

    def connect(self, host: str = "127.0.0.1", port: int = 18772) -> None:
        """Open the WebSocket connection and start the background receive loop."""
        url = f"ws://{host}:{port}/"
        self._ws = websocket.WebSocketApp(
            url,
            on_open=self._on_open,
            on_message=self._on_message,
            on_error=self._on_error,
            on_close=self._on_close,
        )
        self._thread = threading.Thread(
            target=self._ws.run_forever,
            daemon=True,
            name="SevenDaysBridge.WebSocketSubscriber",
        )
        self._thread.start()

        if not self._connected.wait(timeout=5.0):
            raise WebSocketSubscriberError(
                f"Timed out waiting for WebSocket connection to {url}"
            )

    def close(self) -> None:
        """Close the WebSocket connection."""
        if self._ws:
            self._ws.close()

    def is_connected(self) -> bool:
        return self._connected.is_set()

    # ------------------------------------------------------------------
    # Event waiting and subscribing
    # ------------------------------------------------------------------

    def wait_for_event(
        self,
        event_type: str,
        timeout_ms: int = 30_000,
        raise_on_timeout: bool = True,
    ) -> Optional[dict]:
        """Block until an event of the given type arrives or the timeout elapses.

        Returns the event dict, or None if `raise_on_timeout=False` and
        the timeout was reached.
        """
        deadline = timeout_ms / 1000.0
        start = threading.Event()
        start.set()

        import time
        end_time = time.monotonic() + deadline

        while time.monotonic() < end_time:
            remaining = end_time - time.monotonic()
            try:
                event = self._event_queue.get(timeout=max(0.0, remaining))
            except queue.Empty:
                break

            if event.get("Type") == event_type:
                return event

            # Put non-matching events back for other waiters (best effort).
            self._event_queue.put(event)

        if raise_on_timeout:
            raise WebSocketSubscriberError(
                f"Timed out waiting for WebSocket event '{event_type}' "
                f"after {timeout_ms}ms"
            )
        return None

    def subscribe(self, event_type: str, callback: Callable[[dict], None]) -> None:
        """Register a callback invoked on the receive thread for each matching event."""
        with self._lock:
            self._callbacks.setdefault(event_type, []).append(callback)

    def unsubscribe(self, event_type: str, callback: Callable[[dict], None]) -> None:
        with self._lock:
            callbacks = self._callbacks.get(event_type, [])
            if callback in callbacks:
                callbacks.remove(callback)

    # ------------------------------------------------------------------
    # Internal WebSocketApp callbacks
    # ------------------------------------------------------------------

    def _on_open(self, ws: websocket.WebSocketApp) -> None:
        self._connected.set()

    def _on_message(self, ws: websocket.WebSocketApp, message: str) -> None:
        try:
            event = json.loads(message)
        except json.JSONDecodeError:
            return

        event_type = event.get("Type", "")

        # Deliver to registered callbacks.
        with self._lock:
            for cb in list(self._callbacks.get(event_type, [])):
                try:
                    cb(event)
                except Exception:
                    pass

        # Put into queue for blocking waiters (skip high-frequency state_update).
        if event_type != "state_update":
            self._event_queue.put(event)

    def _on_error(self, ws: websocket.WebSocketApp, error: Exception) -> None:
        self._error = error
        self._connected.set()  # unblock connect() if still waiting

    def _on_close(
        self,
        ws: websocket.WebSocketApp,
        close_status_code: Optional[int],
        close_msg: Optional[str],
    ) -> None:
        self._connected.clear()
