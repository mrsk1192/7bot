from __future__ import annotations

import json
from dataclasses import asdict
from pathlib import Path
from typing import Any, Dict, List, Optional

from sevendays_bridge.bases import BaseBounds, BaseDefinition, BaseRegistry
from sevendays_bridge.commanding import (
    AgentCommand,
    CommandActionType,
    CommandCompletionCondition,
    CommandFailCondition,
    CommandInterruptPolicy,
    CommandPriority,
    CommandQueue,
    CommandRetryPolicy,
    CommandStatus,
)
from sevendays_bridge.movement import NavigationConfig


class SessionStore:
    """Persists GUI/runtime session state so commands survive GUI restarts."""

    def __init__(self, path: Path):
        self.path = Path(path)

    def save(
        self,
        command_queue: CommandQueue,
        base_registry: BaseRegistry,
        *,
        navigation_config: Optional[NavigationConfig] = None,
        last_interrupt_reason: str = "",
    ) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        payload = {
            "commands": [self._serialize_command(command) for command in command_queue.list_commands()],
            "bases": [self._serialize_base(base) for base in base_registry.list_bases()],
            "navigation_config": (navigation_config or NavigationConfig()).to_dict(),
            "last_interrupt_reason": last_interrupt_reason,
        }
        self.path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    def load_into(self, command_queue: CommandQueue, base_registry: BaseRegistry) -> Dict[str, Any]:
        if not self.path.exists():
            return {"commands_loaded": 0, "bases_loaded": 0, "last_interrupt_reason": ""}

        payload = json.loads(self.path.read_text(encoding="utf-8"))
        commands = [self._deserialize_command(item) for item in payload.get("commands", [])]
        bases = [self._deserialize_base(item) for item in payload.get("bases", [])]

        command_queue.replace_all(commands)
        for base in bases:
            base_registry.add_or_update(base)

        return {
            "commands_loaded": len(commands),
            "bases_loaded": len(bases),
            "navigation_config": NavigationConfig.from_mapping(payload.get("navigation_config")),
            "last_interrupt_reason": str(payload.get("last_interrupt_reason", "")),
        }

    @staticmethod
    def _serialize_position(position: Any) -> Optional[Dict[str, float]]:
        if position is None:
            return None
        if hasattr(position, "x") and hasattr(position, "y") and hasattr(position, "z"):
            return {
                "x": float(position.x),
                "y": float(position.y),
                "z": float(position.z),
            }
        return None

    @staticmethod
    def _deserialize_position(payload: Optional[Dict[str, float]]) -> Any:
        if payload is None:
            return None

        class Position:
            def __init__(self, x: float, y: float, z: float):
                self.x = x
                self.y = y
                self.z = z

        return Position(float(payload["x"]), float(payload["y"]), float(payload["z"]))

    def _serialize_command(self, command: AgentCommand) -> Dict[str, Any]:
        return {
            "command_id": command.command_id,
            "action_type": command.action_type.value,
            "target_position": self._serialize_position(command.target_position),
            "target_entity": command.target_entity,
            "target_resource_type": command.target_resource_type,
            "target_area": command.target_area,
            "base_id": command.base_id,
            "priority": int(command.priority),
            "interruptible": command.interruptible.value,
            "retry_policy": asdict(command.retry_policy),
            "timeout_seconds": command.timeout_seconds,
            "completion_condition": asdict(command.completion_condition),
            "fail_condition": asdict(command.fail_condition),
            "metadata": dict(command.metadata),
            "status": command.status.value,
            "created_at_monotonic": command.created_at_monotonic,
            "updated_at_monotonic": command.updated_at_monotonic,
            "attempt_count": command.attempt_count,
            "resume_token": command.resume_token,
            "last_error": command.last_error,
        }

    def _deserialize_command(self, payload: Dict[str, Any]) -> AgentCommand:
        restored_status = CommandStatus(payload.get("status", CommandStatus.QUEUED.value))
        if restored_status == CommandStatus.RUNNING:
            # Running commands cannot be resumed safely after GUI shutdown.
            restored_status = CommandStatus.PAUSED

        priority_value = int(payload.get("priority", int(CommandPriority.NORMAL)))
        try:
            priority = CommandPriority(priority_value)
        except ValueError:
            if priority_value >= int(CommandPriority.URGENT):
                priority = CommandPriority.URGENT
            elif priority_value >= int(CommandPriority.HIGH):
                priority = CommandPriority.HIGH
            elif priority_value >= int(CommandPriority.NORMAL):
                priority = CommandPriority.NORMAL
            else:
                priority = CommandPriority.LOW

        return AgentCommand(
            command_id=str(payload["command_id"]),
            action_type=CommandActionType(payload["action_type"]),
            target_position=self._deserialize_position(payload.get("target_position")),
            target_entity=payload.get("target_entity"),
            target_resource_type=payload.get("target_resource_type"),
            target_area=payload.get("target_area"),
            base_id=payload.get("base_id"),
            priority=priority,
            interruptible=CommandInterruptPolicy(payload.get("interruptible", CommandInterruptPolicy.INTERRUPTIBLE.value)),
            retry_policy=CommandRetryPolicy(**payload.get("retry_policy", {})),
            timeout_seconds=float(payload.get("timeout_seconds", 60.0)),
            completion_condition=CommandCompletionCondition(**payload.get("completion_condition", {"kind": "command_finished"})),
            fail_condition=CommandFailCondition(**payload.get("fail_condition", {"kind": "timeout"})),
            metadata=dict(payload.get("metadata", {})),
            status=restored_status,
            created_at_monotonic=float(payload.get("created_at_monotonic", 0.0)),
            updated_at_monotonic=float(payload.get("updated_at_monotonic", 0.0)),
            attempt_count=int(payload.get("attempt_count", 0)),
            resume_token=payload.get("resume_token"),
            last_error=str(payload.get("last_error", "")),
        )

    @staticmethod
    def _serialize_bounds(bounds: Optional[BaseBounds]) -> Optional[Dict[str, float]]:
        if bounds is None:
            return None
        return asdict(bounds)

    def _serialize_base(self, base: BaseDefinition) -> Dict[str, Any]:
        return {
            "base_id": base.base_id,
            "base_name": base.base_name,
            "anchor_position": self._serialize_position(base.anchor_position),
            "bounds": self._serialize_bounds(base.bounds),
            "safety_score": base.safety_score,
            "access_points": [self._serialize_position(item) for item in base.access_points],
            "storage_points": [self._serialize_position(item) for item in base.storage_points],
            "crafting_points": [self._serialize_position(item) for item in base.crafting_points],
            "rest_points": [self._serialize_position(item) for item in base.rest_points],
            "build_area": self._serialize_bounds(base.build_area),
            "defense_area": self._serialize_bounds(base.defense_area),
            "home_marker_priority": base.home_marker_priority,
            "build_plan_id": base.build_plan_id,
        }

    @staticmethod
    def _deserialize_bounds(payload: Optional[Dict[str, float]]) -> Optional[BaseBounds]:
        if payload is None:
            return None
        return BaseBounds(**payload)

    def _deserialize_base(self, payload: Dict[str, Any]) -> BaseDefinition:
        return BaseDefinition(
            base_id=str(payload["base_id"]),
            base_name=str(payload.get("base_name", payload["base_id"])),
            anchor_position=self._deserialize_position(payload.get("anchor_position")),
            bounds=self._deserialize_bounds(payload.get("bounds")) or BaseBounds(0, 0, 0, 0, 0, 0),
            safety_score=float(payload.get("safety_score", 0.5)),
            access_points=[self._deserialize_position(item) for item in payload.get("access_points", []) if item is not None],
            storage_points=[self._deserialize_position(item) for item in payload.get("storage_points", []) if item is not None],
            crafting_points=[self._deserialize_position(item) for item in payload.get("crafting_points", []) if item is not None],
            rest_points=[self._deserialize_position(item) for item in payload.get("rest_points", []) if item is not None],
            build_area=self._deserialize_bounds(payload.get("build_area")),
            defense_area=self._deserialize_bounds(payload.get("defense_area")),
            home_marker_priority=int(payload.get("home_marker_priority", 100)),
            build_plan_id=payload.get("build_plan_id"),
        )
