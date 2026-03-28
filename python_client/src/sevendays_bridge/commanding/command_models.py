from __future__ import annotations

import time
import uuid
from dataclasses import dataclass, field, replace
from enum import Enum
from typing import Any, Dict, Optional


class CommandActionType(str, Enum):
    MOVE_TO_POSITION = "move_to_position"
    SEARCH_RESOURCE = "search_resource"
    GATHER_RESOURCE = "gather_resource"
    MOVE_AVOIDING_HOSTILES = "move_avoiding_hostiles"
    RETURN_TO_BASE = "return_to_base"
    STORE_ITEMS = "store_items"
    COLLECT_ITEMS = "collect_items"
    CRAFT = "craft"
    BUILD = "build"
    REPAIR = "repair"
    PATROL = "patrol"
    WAIT = "wait"
    FOLLOW = "follow"
    SCAN_AREA = "scan_area"
    INVESTIGATE_AREA = "investigate_area"


class CommandStatus(str, Enum):
    QUEUED = "queued"
    RUNNING = "running"
    PAUSED = "paused"
    INTERRUPTED = "interrupted"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"


class CommandPriority(int, Enum):
    LOW = 10
    NORMAL = 50
    HIGH = 100
    URGENT = 200


class CommandInterruptPolicy(str, Enum):
    INTERRUPTIBLE = "interruptible"
    NON_INTERRUPTIBLE = "non_interruptible"
    INTERRUPTIBLE_ONLY_WHEN_CRITICAL = "interruptible_only_when_critical"


@dataclass(frozen=True)
class CommandRetryPolicy:
    """Retry rules are explicit to avoid hidden retry loops."""

    max_attempts: int = 2
    retry_delay_seconds: float = 1.0
    backoff_multiplier: float = 1.0


@dataclass(frozen=True)
class CommandCompletionCondition:
    kind: str
    value: Any = None


@dataclass(frozen=True)
class CommandFailCondition:
    kind: str
    value: Any = None


@dataclass(frozen=True)
class AgentCommand:
    """GUI/upstream instructions are represented as argument-rich commands."""

    command_id: str
    action_type: CommandActionType
    target_position: Any = None
    target_entity: Any = None
    target_resource_type: Optional[str] = None
    target_area: Any = None
    base_id: Optional[str] = None
    priority: CommandPriority = CommandPriority.NORMAL
    interruptible: CommandInterruptPolicy = CommandInterruptPolicy.INTERRUPTIBLE
    retry_policy: CommandRetryPolicy = field(default_factory=CommandRetryPolicy)
    timeout_seconds: float = 60.0
    completion_condition: CommandCompletionCondition = field(
        default_factory=lambda: CommandCompletionCondition(kind="command_finished")
    )
    fail_condition: CommandFailCondition = field(default_factory=lambda: CommandFailCondition(kind="timeout"))
    metadata: Dict[str, Any] = field(default_factory=dict)
    status: CommandStatus = CommandStatus.QUEUED
    created_at_monotonic: float = field(default_factory=time.monotonic)
    updated_at_monotonic: float = field(default_factory=time.monotonic)
    attempt_count: int = 0
    resume_token: Optional[str] = None
    last_error: str = ""

    @classmethod
    def new(
        cls,
        action_type: CommandActionType,
        *,
        target_position: Any = None,
        target_entity: Any = None,
        target_resource_type: Optional[str] = None,
        target_area: Any = None,
        base_id: Optional[str] = None,
        priority: CommandPriority = CommandPriority.NORMAL,
        interruptible: CommandInterruptPolicy = CommandInterruptPolicy.INTERRUPTIBLE,
        retry_policy: Optional[CommandRetryPolicy] = None,
        timeout_seconds: float = 60.0,
        completion_condition: Optional[CommandCompletionCondition] = None,
        fail_condition: Optional[CommandFailCondition] = None,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> "AgentCommand":
        return cls(
            command_id=str(uuid.uuid4()),
            action_type=action_type,
            target_position=target_position,
            target_entity=target_entity,
            target_resource_type=target_resource_type,
            target_area=target_area,
            base_id=base_id,
            priority=priority,
            interruptible=interruptible,
            retry_policy=retry_policy or CommandRetryPolicy(),
            timeout_seconds=timeout_seconds,
            completion_condition=completion_condition or CommandCompletionCondition(kind="command_finished"),
            fail_condition=fail_condition or CommandFailCondition(kind="timeout"),
            metadata=dict(metadata or {}),
        )

    def with_status(self, status: CommandStatus, *, last_error: str = "") -> "AgentCommand":
        return replace(
            self,
            status=status,
            updated_at_monotonic=time.monotonic(),
            last_error=last_error,
        )

    def increment_attempts(self) -> "AgentCommand":
        return replace(
            self,
            attempt_count=self.attempt_count + 1,
            updated_at_monotonic=time.monotonic(),
        )

    def with_resume_token(self, token: Optional[str]) -> "AgentCommand":
        return replace(
            self,
            resume_token=token,
            updated_at_monotonic=time.monotonic(),
        )
