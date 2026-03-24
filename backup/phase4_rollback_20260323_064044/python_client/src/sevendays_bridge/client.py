from __future__ import annotations

import json
import logging
import time
from logging.handlers import RotatingFileHandler
from pathlib import Path
from typing import Any, Dict, Optional
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

from . import commands
from .exceptions import BridgeApiError, BridgeConnectionError, BridgeProtocolError
from .models import (
    BiomeInfo,
    BridgeEnvelope,
    BridgeState,
    CapabilitySet,
    EntityQueryResult,
    EnvironmentSummary,
    InputActionResult,
    InteractionContextObservation,
    InteractableQueryResult,
    LookTargetObservation,
    LogsTailResult,
    PlayerPositionResult,
    PlayerRotationResult,
    ResourceCandidateQueryResult,
    TerrainSummary,
    VersionInfo,
)


class SevenDaysBridgeClient:
    def __init__(
        self,
        host: str = "127.0.0.1",
        port: int = 18771,
        timeout_seconds: float = 5.0,
        log_file: Optional[Path] = None,
    ) -> None:
        if host != "127.0.0.1":
            raise ValueError("Phase 2 client only allows 127.0.0.1.")
        if port <= 0 or port > 65535:
            raise ValueError("port must be in the range 1-65535")

        self.host = host
        self.port = port
        self.timeout_seconds = timeout_seconds
        self.base_url = f"http://{host}:{port}"
        self.logger = _build_logger(log_file)
        self.logger.info("Client initialized. base_url=%s timeout=%s", self.base_url, self.timeout_seconds)

    def ping(self) -> Dict[str, Any]:
        return self._request("GET", "/api/ping").data

    def get_version(self) -> VersionInfo:
        return VersionInfo.from_dict(self._request("GET", "/api/get_version").data)

    def get_capabilities(self) -> CapabilitySet:
        return CapabilitySet.from_dict(self._request("GET", "/api/get_capabilities").data)

    def get_state(self) -> BridgeState:
        return BridgeState.from_dict(self._request("GET", "/api/get_state").data)

    def get_player_position(self) -> PlayerPositionResult:
        return PlayerPositionResult.from_dict(self._request("GET", "/api/get_player_position").data)

    def get_player_rotation(self) -> PlayerRotationResult:
        return PlayerRotationResult.from_dict(self._request("GET", "/api/get_player_rotation").data)

    def get_logs_tail(self, lines: int = 50) -> LogsTailResult:
        query = urlencode({"lines": lines})
        return LogsTailResult.from_dict(self._request("GET", f"/api/get_logs_tail?{query}").data)

    def get_look_target(self) -> LookTargetObservation:
        return LookTargetObservation.from_dict(self._request("GET", "/api/get_look_target").data)

    def get_interaction_context(self) -> InteractionContextObservation:
        return InteractionContextObservation.from_dict(self._request("GET", "/api/get_interaction_context").data)

    def query_resource_candidates(self, **arguments: Any) -> ResourceCandidateQueryResult:
        query = urlencode(arguments, doseq=True)
        path = "/api/query_resource_candidates"
        if query:
            path = f"{path}?{query}"
        return ResourceCandidateQueryResult.from_dict(self._request("GET", path).data)

    def query_interactables_in_radius(self, **arguments: Any) -> InteractableQueryResult:
        query = urlencode(arguments, doseq=True)
        path = "/api/query_interactables_in_radius"
        if query:
            path = f"{path}?{query}"
        return InteractableQueryResult.from_dict(self._request("GET", path).data)

    def query_entities_in_radius(self, **arguments: Any) -> EntityQueryResult:
        query = urlencode(arguments, doseq=True)
        path = "/api/query_entities_in_radius"
        if query:
            path = f"{path}?{query}"
        return EntityQueryResult.from_dict(self._request("GET", path).data)

    def get_environment_summary(self) -> EnvironmentSummary:
        return EnvironmentSummary.from_dict(self._request("GET", "/api/get_environment_summary").data)

    def get_biome_info(self) -> BiomeInfo:
        return BiomeInfo.from_dict(self._request("GET", "/api/get_biome_info").data)

    def get_terrain_summary(self) -> TerrainSummary:
        return TerrainSummary.from_dict(self._request("GET", "/api/get_terrain_summary").data)

    def command(self, command: str, arguments: Optional[Dict[str, Any]] = None) -> BridgeEnvelope:
        return self._request("POST", "/api/command", {"Command": command, "Arguments": arguments or {}})

    def run_action(self, action: str, **arguments: Any) -> InputActionResult:
        envelope = self.command(action, arguments)
        return InputActionResult.from_dict(envelope.data)

    def press(self, name: str) -> InputActionResult:
        return self.run_action(commands.resolve_press(name))

    def release(self, name: str) -> InputActionResult:
        return self.run_action(commands.resolve_release(name))

    def tap(self, name: str, hold_ms: int = 100) -> InputActionResult:
        if name in commands.TAP_ACTIONS:
            return self.run_action(commands.resolve_tap(name))

        press_result = self.press(name)
        time.sleep(max(0, hold_ms) / 1000.0)
        release_result = self.release(name)
        return release_result if release_result.accepted else press_result

    def look_delta(self, dx: float, dy: float) -> InputActionResult:
        return self.run_action("look_delta", dx=dx, dy=dy)

    def look_to(self, yaw: float, pitch: float) -> InputActionResult:
        return self.run_action("look_to", yaw=yaw, pitch=pitch)

    def select_hotbar_slot(self, slot: int) -> InputActionResult:
        return self.run_action("select_hotbar_slot", slot=slot)

    def stop_all(self) -> InputActionResult:
        return self.run_action("stop_all_input")

    def move_forward(self, active: bool = True) -> InputActionResult:
        return self.press("move_forward") if active else self.release("move_forward")

    def sprint(self, active: bool = True) -> InputActionResult:
        return self.press("sprint") if active else self.release("sprint")

    def crouch(self, active: Optional[bool] = None) -> InputActionResult:
        if active is None:
            return self.run_action("crouch_toggle")
        return self.press("crouch") if active else self.release("crouch")

    def jump(self) -> InputActionResult:
        return self.tap("jump")

    def attack_primary(self, duration_ms: int = 300, active: Optional[bool] = None) -> InputActionResult:
        if active is None:
            return self.tap("attack_primary", hold_ms=duration_ms)
        return self.press("primary_action") if active else self.release("primary_action")

    def aim(self, active: bool = True) -> InputActionResult:
        return self.press("aim") if active else self.release("aim")

    def reload(self) -> InputActionResult:
        return self.tap("reload")

    def interact(self, hold_ms: Optional[int] = None) -> InputActionResult:
        if hold_ms is None:
            return self.tap("interact")
        press_result = self.press("hold_interact")
        time.sleep(max(0, hold_ms) / 1000.0)
        release_result = self.release("hold_interact")
        return release_result if release_result.accepted else press_result

    def hold_interact(self, active: bool = True) -> InputActionResult:
        return self.press("hold_interact") if active else self.release("hold_interact")

    def open_inventory(self) -> InputActionResult:
        return self.run_action("toggle_inventory")

    def open_map(self) -> InputActionResult:
        return self.run_action("toggle_map")

    def open_quest_log(self) -> InputActionResult:
        return self.run_action("toggle_quest_log")

    def hotbar_next(self) -> InputActionResult:
        return self.tap("hotbar_next")

    def hotbar_prev(self) -> InputActionResult:
        return self.tap("hotbar_prev")

    def escape_menu(self) -> InputActionResult:
        return self.run_action("escape_menu")

    def confirm(self) -> InputActionResult:
        return self.tap("confirm")

    def cancel(self) -> InputActionResult:
        return self.run_action("cancel")

    def toggle_flashlight(self) -> InputActionResult:
        return self.run_action("toggle_flashlight")

    def console_toggle(self) -> InputActionResult:
        return self.run_action("console_toggle")

    def respawn_default(self) -> InputActionResult:
        return self._respawn_action_with_retry("respawn_select_default")

    def respawn_bedroll(self) -> InputActionResult:
        return self._respawn_action_with_retry("respawn_at_bedroll")

    def respawn_near_bedroll(self) -> InputActionResult:
        return self._respawn_action_with_retry("respawn_near_bedroll")

    def respawn_random(self) -> InputActionResult:
        return self._respawn_action_with_retry("respawn_at_random")

    def respawn_confirm(self) -> InputActionResult:
        return self._respawn_action_with_retry("respawn_confirm")

    def respawn_cancel(self) -> InputActionResult:
        return self.run_action("respawn_cancel")

    def _respawn_action_with_retry(self, action: str, max_retries: int = 300) -> "InputActionResult":
        from .exceptions import BridgeApiError, BridgeConnectionError
        for attempt in range(max_retries + 1):
            try:
                return self.run_action(action)
            except BridgeApiError as exc:
                if exc.error_type == "RESPAWN_NOT_AVAILABLE" and attempt < max_retries:
                    time.sleep(1.0)
                    continue
                raise
            except BridgeConnectionError:
                if attempt < 5:
                    time.sleep(2.0)
                    continue
                raise
        raise RuntimeError("Respawn retry exhausted")

    def wait_for_respawn_screen(self, timeout_ms: int = 15000) -> InputActionResult:
        return self.run_action("wait_for_respawn_screen", timeout_ms=timeout_ms)

    def wait_until_respawned(self, timeout_ms: int = 30000) -> InputActionResult:
        return self.run_action("wait_for_respawn_complete", timeout_ms=timeout_ms)

    def _request(
        self,
        method: str,
        path: str,
        body: Optional[Dict[str, Any]] = None,
    ) -> BridgeEnvelope:
        url = self.base_url + path
        payload = None
        headers = {"Accept": "application/json"}
        effective_timeout = self.timeout_seconds
        if body is not None:
            payload = json.dumps(body).encode("utf-8")
            headers["Content-Type"] = "application/json"
            if method == "POST" and "Arguments" in body and "timeout_ms" in body["Arguments"]:
                effective_timeout = max(self.timeout_seconds, (body["Arguments"]["timeout_ms"] / 1000.0) + 2.0)

        self.logger.info("Request %s %s with timeout %.1f", method, url, effective_timeout)
        request = Request(url=url, data=payload, method=method, headers=headers)

        try:
            with urlopen(request, timeout=effective_timeout) as response:
                raw = response.read().decode("utf-8")
        except HTTPError as exc:
            raw = exc.read().decode("utf-8", errors="replace")
            self.logger.exception("HTTP error from bridge")
            try:
                decoded = json.loads(raw)
                envelope = BridgeEnvelope.from_dict(decoded)
                error = envelope.error or {}
                raise BridgeApiError(
                    error_type=str(error.get("Type", f"http_{exc.code}")),
                    message=str(error.get("Message", raw)),
                ) from exc
            except json.JSONDecodeError:
                raise BridgeConnectionError(f"HTTP {exc.code} calling {url}: {raw}") from exc
        except URLError as exc:
            self.logger.exception("Connection error from bridge")
            raise BridgeConnectionError(f"Could not connect to {url}: {exc}") from exc

        self.logger.info("Response %s", raw)
        try:
            decoded = json.loads(raw)
        except json.JSONDecodeError as exc:
            raise BridgeProtocolError(f"Bridge response was not valid JSON: {raw}") from exc

        envelope = BridgeEnvelope.from_dict(decoded)
        if not envelope.ok:
            error = envelope.error or {}
            raise BridgeApiError(
                error_type=str(error.get("Type", "unknown_error")),
                message=str(error.get("Message", "unknown bridge error")),
            )

        return envelope


def _build_logger(log_file: Optional[Path]) -> logging.Logger:
    logger = logging.getLogger("sevendays_bridge")
    if logger.handlers:
        return logger

    logger.setLevel(logging.INFO)
    log_path = log_file or Path(__file__).resolve().parents[3] / "logs" / "python_client.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)
    handler = RotatingFileHandler(log_path, maxBytes=1_000_000, backupCount=3, encoding="utf-8")
    handler.setFormatter(logging.Formatter("%(asctime)s [%(levelname)s] %(message)s"))
    logger.addHandler(handler)
    logger.propagate = False
    return logger
