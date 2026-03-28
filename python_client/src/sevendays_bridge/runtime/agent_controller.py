from __future__ import annotations

import time
import threading
import uuid
from dataclasses import dataclass
from typing import Callable, Dict, Optional

from sevendays_bridge.bases import BaseDefinition, BaseRegistry
from sevendays_bridge.build import BuildExecutor, BuildPlanRegistry, BuildProgress
from sevendays_bridge.commanding import AgentCommand, CommandActionType, CommandInterruptPolicy, CommandQueue, CommandStatus
from sevendays_bridge.decision import ActionSelector
from sevendays_bridge.exceptions import BridgeApiError, BridgeConnectionError, BridgeProtocolError
from sevendays_bridge.movement import NavigationConfig, TargetApproach
from sevendays_bridge.priority import PriorityDecision, PriorityMonitor, PrioritySeverity, PrioritySnapshot
from sevendays_bridge.search import ResultMemory, SearchLoop


@dataclass(frozen=True)
class AgentTickResult:
    tick_state: str
    current_command_id: Optional[str]
    current_command_status: Optional[str]
    priority_reason: str
    detail: str


class AgentController:
    """Top-level controller that keeps GUI, queue, and AI execution separated."""

    def __init__(
        self,
        client,
        *,
        config: Optional[NavigationConfig] = None,
        command_queue: Optional[CommandQueue] = None,
        result_memory: Optional[ResultMemory] = None,
        priority_monitor: Optional[PriorityMonitor] = None,
        base_registry: Optional[BaseRegistry] = None,
        build_plan_registry: Optional[BuildPlanRegistry] = None,
        state_changed_callback: Optional[Callable[[], None]] = None,
    ) -> None:
        self.client = client
        self.config = config or NavigationConfig()
        self.command_queue = command_queue or CommandQueue()
        self.result_memory = result_memory or ResultMemory()
        self.priority_monitor = priority_monitor or PriorityMonitor()
        self.base_registry = base_registry or BaseRegistry()
        self.build_plan_registry = build_plan_registry or BuildPlanRegistry()
        self.search_loop = None
        self.approach = None
        self.action_selector = None
        self.build_executor = None
        self.build_progress: Dict[str, BuildProgress] = {}
        self.logs: list[str] = []
        self.last_interrupt_reason: str = ""
        self._agent_running = False
        self._connection_state = "disconnected"
        self._last_connection_error = "bridge_not_checked"
        self._last_connection_log_signature = ""
        self._lock = threading.RLock()
        self._state_changed_callback = state_changed_callback
        self._state_change_listeners: list[Callable[[], None]] = []
        self._cached_state = None
        self._cached_environment_summary = None
        self._rebuild_navigation_dependencies()

    def queue_command(self, command: AgentCommand) -> AgentCommand:
        with self._lock:
            queued = self.command_queue.enqueue(command)
            self.logs.append(f"command_queued:{queued.command_id}:{queued.action_type.value}")
            self._notify_state_changed()
            return queued

    def upsert_base(self, base: BaseDefinition) -> BaseDefinition:
        with self._lock:
            stored = self.base_registry.add_or_update(base)
            self.logs.append(f"base_upserted:{stored.base_id}:{stored.base_name}")
            self._notify_state_changed()
            return stored

    def edit_command(self, command_id: str, **changes) -> AgentCommand:
        with self._lock:
            updated = self.command_queue.edit(command_id, **changes)
            self.logs.append(f"command_edited:{command_id}")
            self._notify_state_changed()
            return updated

    def delete_command(self, command_id: str) -> None:
        with self._lock:
            self.command_queue.remove(command_id)
            self.logs.append(f"command_deleted:{command_id}")
            self._notify_state_changed()

    def pause_command(self, command_id: str) -> AgentCommand:
        with self._lock:
            paused = self.command_queue.pause(command_id)
            self._safe_stop_all_locked()
            self.logs.append(f"command_paused:{command_id}")
            self._notify_state_changed()
            return paused

    def resume_command(self, command_id: str) -> AgentCommand:
        with self._lock:
            resumed = self.command_queue.resume(command_id)
            self.logs.append(f"command_resumed:{command_id}")
            self._notify_state_changed()
            return resumed

    def cancel_command(self, command_id: str) -> AgentCommand:
        with self._lock:
            cancelled = self.command_queue.cancel(command_id)
            self._safe_stop_all_locked()
            self.logs.append(f"command_cancelled:{command_id}")
            self._notify_state_changed()
            return cancelled

    def interrupt_command(self, command_id: str, reason: str = "manual_interrupt") -> AgentCommand:
        with self._lock:
            interrupted = self.command_queue.interrupt(command_id, resume_token=f"manual:{command_id}")
            self._safe_stop_all_locked()
            self.last_interrupt_reason = reason
            self.logs.append(f"command_interrupted:{command_id}:{reason}")
            self._notify_state_changed()
            return interrupted

    def move_command(self, command_id: str, new_index: int) -> None:
        with self._lock:
            self.command_queue.reorder(command_id, new_index)
            self.logs.append(f"command_reordered:{command_id}:{new_index}")
            self._notify_state_changed()

    def queue_build_for_base(self, base_id: str) -> AgentCommand:
        with self._lock:
            command = AgentCommand.new(
                CommandActionType.BUILD,
                base_id=base_id,
                metadata={"source": "gui_build_request"},
            )
            return self.queue_command(command)

    def start_agent(self) -> None:
        with self._lock:
            self._agent_running = True
            self.logs.append("agent_started")
            self._notify_state_changed()

    def stop_agent(self) -> None:
        with self._lock:
            self._agent_running = False
            self._safe_stop_all_locked()
            self.logs.append("agent_stopped")
            self._notify_state_changed()

    def reset_agent(self) -> None:
        with self._lock:
            self._agent_running = False
            self._safe_stop_all_locked()
            self.command_queue.replace_all([])
            self.build_progress.clear()
            self.last_interrupt_reason = ""
            self.logs.append("agent_reset")
            self._notify_state_changed()

    def is_agent_running(self) -> bool:
        with self._lock:
            return self._agent_running

    def get_navigation_config(self) -> NavigationConfig:
        with self._lock:
            return self.config

    def apply_navigation_config(self, config: NavigationConfig) -> NavigationConfig:
        errors = config.validate_scan_settings()
        if errors:
            raise ValueError("; ".join(errors))
        with self._lock:
            self.config = config
            self._rebuild_navigation_dependencies()
            self.logs.append("navigation_config_updated")
            self._notify_state_changed()
            return self.config

    def add_state_change_listener(self, listener: Callable[[], None]) -> None:
        with self._lock:
            self._state_change_listeners.append(listener)

    def remove_state_change_listener(self, listener: Callable[[], None]) -> None:
        with self._lock:
            self._state_change_listeners = [registered for registered in self._state_change_listeners if registered is not listener]

    def get_raw_status(self, snapshot: Optional[PrioritySnapshot] = None) -> dict:
        """Return raw runtime status without any GUI view-model dependency."""
        with self._lock:
            if self._cached_state is None or self._cached_environment_summary is None:
                self._refresh_observation_cache_locked()
            state = self._cached_state
            environment_summary = self._cached_environment_summary
            player = getattr(state, "player", None)
            current = self.command_queue.current()
            command_list = [
                {
                    "command_id": command.command_id,
                    "action_type": command.action_type.value,
                    "status": command.status.value,
                    "priority": int(command.priority),
                    "summary": self._command_summary(command),
                }
                for command in self.command_queue.list_commands()
            ]
            base_list = [
                {
                    "base_id": base.base_id,
                    "base_name": base.base_name,
                    "build_plan_id": base.build_plan_id or "",
                }
                for base in self.base_registry.list_bases()
            ]
            decisions = []
            if state is not None and environment_summary is not None:
                decisions = self.priority_monitor.evaluate(state, environment_summary=environment_summary, snapshot=snapshot)
            debuff_text = ",".join((snapshot.debuffs if snapshot is not None else [])) if snapshot is not None else "unknown"
            equipment_text = "ok" if snapshot is None else (
                "missing_or_broken"
                if (
                    not snapshot.equipment_status.has_required_tool
                    or snapshot.equipment_status.selected_tool_broken
                    or snapshot.equipment_status.missing_required_armor
                )
                else "ok"
            )
            weight_text = "unknown"
            if snapshot is not None:
                weight_text = f"{snapshot.inventory_status.carried_weight_ratio:.2f}"
            position_text = "unknown"
            biome_text = "unknown"
            look_target_text = "Unknown"
            nearby_resource_count = "0"
            nearby_interactable_count = "0"
            nearby_entity_count = "0"
            if player is not None and getattr(player, "position", None) is not None:
                position = player.position
                position_text = f"({position.x:.1f}, {position.y:.1f}, {position.z:.1f})"
            if state is not None:
                resource_observation = getattr(state, "resource_observation", None)
                if resource_observation is not None:
                    biome_text = getattr(resource_observation, "biome", biome_text) or biome_text
                    look_target = getattr(resource_observation, "look_target", None)
                    if look_target is not None:
                        look_target_text = f"{getattr(look_target, 'target_kind', 'none')}:{getattr(look_target, 'target_name', 'Unknown')}"
                resource_summary = getattr(state, "nearby_resource_candidates_summary", None)
                if resource_summary is not None:
                    nearby_resource_count = str(getattr(resource_summary, "count", 0))
                interactable_summary = getattr(state, "nearby_interactables_summary", None)
                if interactable_summary is not None:
                    nearby_interactable_count = str(getattr(interactable_summary, "count", 0))
                entity_summary = getattr(state, "nearby_entities_summary", None)
                if entity_summary is not None:
                    top_entities = getattr(entity_summary, "top_entities", []) or []
                    nearby_entity_count = str(len(top_entities))
            return {
                "current_action": "idle" if current is None else current.action_type.value,
                "current_target": self._command_summary(current) if current is not None else "none",
                "interrupt_reason": self.last_interrupt_reason or ("none" if not decisions else decisions[0].reason),
                "health": self._fraction_text(getattr(player, "hp", None), getattr(player, "max_hp", None)),
                "water": self._value_text(getattr(player, "water", None)),
                "hunger": self._value_text(getattr(player, "food", None)),
                "stamina": self._fraction_text(getattr(player, "stamina", None), getattr(player, "max_stamina", None)),
                "debuffs": debuff_text,
                "carried_weight": weight_text,
                "equipment_state": equipment_text,
                "agent_state": "running" if self._agent_running else "stopped",
                "connection_state": self._connection_state,
                "player_position": position_text,
                "biome": biome_text,
                "look_target": look_target_text,
                "nearby_resource_count": nearby_resource_count,
                "nearby_interactable_count": nearby_interactable_count,
                "nearby_entity_count": nearby_entity_count,
                "last_error": self._last_connection_error or "none",
                "commands": command_list,
                "bases": base_list,
                "logs": self.logs[-50:],
            }

    def tick(self, snapshot: Optional[PrioritySnapshot] = None) -> AgentTickResult:
        with self._lock:
            if not self._agent_running:
                return AgentTickResult("agent_stopped", None, None, "", "agent_not_running")

            state, environment_summary = self._refresh_observation_cache_locked()
            if state is None or environment_summary is None:
                return AgentTickResult("bridge_unavailable", None, None, "", self._last_connection_error)

            priority = self.priority_monitor.highest_priority(state, environment_summary=environment_summary, snapshot=snapshot)
            current = self.command_queue.current()

            if priority is not None and self._should_interrupt(current, priority):
                self._interrupt_for_priority(current, priority)
                return AgentTickResult(
                    tick_state="priority_interrupt",
                    current_command_id=None if current is None else current.command_id,
                    current_command_status=None if current is None else CommandStatus.INTERRUPTED.value,
                    priority_reason=priority.reason,
                    detail=priority.recovery_hint,
                )

            command = self.command_queue.start_next()
            if command is None:
                return AgentTickResult("idle", None, None, "", "no_command")

            outcome = self._execute_command(command)
            updated = self.command_queue.get(command.command_id)
            self._notify_state_changed()
            return AgentTickResult(
                tick_state=outcome,
                current_command_id=command.command_id,
                current_command_status=None if updated is None else updated.status.value,
                priority_reason="",
                detail=command.action_type.value,
            )

    def refresh_observation_cache(self) -> None:
        with self._lock:
            self._refresh_observation_cache_locked()
            self._notify_state_changed()

    def has_pending_commands(self) -> bool:
        with self._lock:
            return any(
                command.status in {CommandStatus.QUEUED, CommandStatus.RUNNING, CommandStatus.INTERRUPTED}
                for command in self.command_queue.list_commands()
            )

    def _should_interrupt(self, command: Optional[AgentCommand], decision: PriorityDecision) -> bool:
        if not decision.should_interrupt_now:
            return False
        if command is None:
            return True
        if command.interruptible == CommandInterruptPolicy.INTERRUPTIBLE:
            return True
        if command.interruptible == CommandInterruptPolicy.INTERRUPTIBLE_ONLY_WHEN_CRITICAL:
            return decision.severity == PrioritySeverity.CRITICAL
        return False

    def _interrupt_for_priority(self, command: Optional[AgentCommand], decision: PriorityDecision) -> None:
        self._safe_stop_all_locked()
        self.last_interrupt_reason = decision.reason
        self.logs.append(f"priority_interrupt:{decision.action_kind.value}:{decision.reason}")
        if command is not None:
            resume_token = f"{command.command_id}:{int(time.monotonic())}"
            self.command_queue.force_interrupt_current(resume_token=resume_token)
        self._notify_state_changed()

    def _execute_command(self, command: AgentCommand) -> str:
        if command.action_type in {CommandActionType.MOVE_TO_POSITION, CommandActionType.MOVE_AVOIDING_HOSTILES}:
            return self._run_move_to_position(command)
        if command.action_type in {
            CommandActionType.SEARCH_RESOURCE,
            CommandActionType.GATHER_RESOURCE,
            CommandActionType.SCAN_AREA,
            CommandActionType.INVESTIGATE_AREA,
            CommandActionType.COLLECT_ITEMS,
        }:
            return self._run_search_like(command)
        if command.action_type in {CommandActionType.RETURN_TO_BASE, CommandActionType.STORE_ITEMS}:
            return self._run_return_to_base(command)
        if command.action_type == CommandActionType.PATROL:
            if command.target_position is not None:
                return self._run_move_to_position(command)
            return self._run_search_like(command)
        if command.action_type in {CommandActionType.BUILD, CommandActionType.REPAIR}:
            return self._run_build(command)
        if command.action_type == CommandActionType.WAIT:
            time.sleep(min(command.timeout_seconds, 0.25))
            self.command_queue.complete(command.command_id)
            self.logs.append(f"command_completed:{command.command_id}:{command.action_type.value}")
            return "completed_wait"

        self.command_queue.fail(command.command_id, "unsupported_action_type")
        self.logs.append(f"command_failed:{command.command_id}:unsupported_action_type")
        self._safe_stop_all_locked()
        return "failed_unsupported"

    def _run_move_to_position(self, command: AgentCommand) -> str:
        if command.target_position is None:
            self.command_queue.fail(command.command_id, "target_position_missing")
            self.logs.append(f"command_failed:{command.command_id}:target_position_missing")
            self._safe_stop_all_locked()
            return "failed_move_target_missing"

        result = self.approach.approach_with_details(command.target_position, "CommandTarget", "terrain")
        if result.status == "unreachable":
            self.command_queue.fail(command.command_id, result.reason)
            self.logs.append(f"command_failed:{command.command_id}:{result.reason}")
            self.result_memory.mark_unreachable(f"command:{command.command_id}")
            self._safe_stop_all_locked()
            return "failed_move_unreachable"

        self.command_queue.complete(command.command_id)
        self.logs.append(f"command_completed:{command.command_id}:{command.action_type.value}")
        return "completed_move"

    def _run_search_like(self, command: AgentCommand) -> str:
        result = self.search_loop.run_cycle()
        if result.final_state == "FAILED":
            elapsed = time.monotonic() - command.created_at_monotonic
            if result.failure_reason == "no_target_after_explore" and elapsed < command.timeout_seconds:
                self.command_queue.edit(command.command_id, status=CommandStatus.QUEUED)
                self.logs.append(f"command_waiting_for_target:{command.command_id}:{command.action_type.value}")
                self._safe_stop_all_locked()
                return "search_waiting_for_target"
            updated = self.command_queue.get(command.command_id)
            attempts = 0 if updated is None else updated.attempt_count
            if attempts + 1 < command.retry_policy.max_attempts:
                self.command_queue.edit(command.command_id, attempt_count=attempts + 1, status=CommandStatus.QUEUED)
                self.logs.append(f"command_retrying:{command.command_id}:{result.failure_reason or 'search_failed'}")
                self._safe_stop_all_locked()
                return "retry_search"
            self.command_queue.fail(command.command_id, result.failure_reason or "search_failed")
            self.logs.append(f"command_failed:{command.command_id}:{result.failure_reason or 'search_failed'}")
            self._safe_stop_all_locked()
            return "failed_search"

        self.command_queue.complete(command.command_id)
        self.logs.append(f"command_completed:{command.command_id}:{command.action_type.value}")
        return "completed_search"

    def _run_return_to_base(self, command: AgentCommand) -> str:
        if not command.base_id:
            self.command_queue.fail(command.command_id, "base_id_missing")
            self.logs.append(f"command_failed:{command.command_id}:base_id_missing")
            self._safe_stop_all_locked()
            return "failed_return_no_base"

        base = self.base_registry.get(command.base_id)
        if base is None:
            self.command_queue.fail(command.command_id, "base_not_found")
            self.logs.append(f"command_failed:{command.command_id}:base_not_found")
            self._safe_stop_all_locked()
            return "failed_return_unknown_base"

        target = base.access_points[0] if base.access_points else base.anchor_position
        result = self.approach.approach_with_details(target, base.base_name, "interactable")
        if result.status == "unreachable":
            self.command_queue.fail(command.command_id, result.reason)
            self.logs.append(f"command_failed:{command.command_id}:{result.reason}")
            self._safe_stop_all_locked()
            return "failed_return_unreachable"

        self.command_queue.complete(command.command_id)
        self.logs.append(f"command_completed:{command.command_id}:{command.action_type.value}")
        return "completed_return_to_base"

    def _run_build(self, command: AgentCommand) -> str:
        base_id = command.base_id
        if not base_id:
            self.command_queue.fail(command.command_id, "build_requires_base_id")
            self.logs.append(f"command_failed:{command.command_id}:build_requires_base_id")
            self._safe_stop_all_locked()
            return "failed_build_no_base"

        base = self.base_registry.get(base_id)
        if base is None or not base.build_plan_id:
            self.command_queue.fail(command.command_id, "build_plan_missing")
            self.logs.append(f"command_failed:{command.command_id}:build_plan_missing")
            self._safe_stop_all_locked()
            return "failed_build_no_plan"

        plan = self.build_plan_registry.get(base.build_plan_id)
        if plan is None:
            self.command_queue.fail(command.command_id, "unknown_build_plan")
            self.logs.append(f"command_failed:{command.command_id}:unknown_build_plan")
            self._safe_stop_all_locked()
            return "failed_build_unknown_plan"

        progress = self.build_progress.setdefault(plan.build_plan_id, BuildProgress(build_plan_id=plan.build_plan_id))
        result = self.build_executor.execute_next_step(plan, progress)
        if result.status == "failed":
            self.command_queue.fail(command.command_id, result.reason)
            self.logs.append(f"command_failed:{command.command_id}:{result.reason}")
            self._safe_stop_all_locked()
            return "failed_build_step"
        if result.status == "completed":
            self.command_queue.complete(command.command_id)
            self.logs.append(f"command_completed:{command.command_id}:{command.action_type.value}")
            return "completed_build_plan"
        if progress.current_step(plan) is None:
            self.command_queue.complete(command.command_id)
            self.logs.append(f"command_completed:{command.command_id}:{command.action_type.value}")
            return "completed_build_plan"
        self.command_queue.edit(command.command_id, status=CommandStatus.QUEUED)
        return "build_step_completed"

    @staticmethod
    def _fraction_text(numerator, denominator) -> str:
        if numerator is None or denominator is None:
            return "unknown"
        return f"{numerator:.0f}/{denominator:.0f}"

    @staticmethod
    def _value_text(value) -> str:
        if value is None:
            return "unknown"
        return f"{value:.0f}"

    @staticmethod
    def _command_summary(command: Optional[AgentCommand]) -> str:
        if command is None:
            return "none"
        if command.target_position is not None:
            x = getattr(command.target_position, "x", 0.0)
            y = getattr(command.target_position, "y", 0.0)
            z = getattr(command.target_position, "z", 0.0)
            return f"{command.action_type.value}@({x:.1f},{y:.1f},{z:.1f})"
        if command.base_id:
            return f"{command.action_type.value}:{command.base_id}"
        if command.target_resource_type:
            return f"{command.action_type.value}:{command.target_resource_type}"
        return command.action_type.value

    def _notify_state_changed(self) -> None:
        callback = self._state_changed_callback
        if callback is not None:
            callback()
        for listener in list(self._state_change_listeners):
            try:
                listener()
            except Exception:
                pass

    def _rebuild_navigation_dependencies(self) -> None:
        self.search_loop = SearchLoop(self.client, config=self.config, result_memory=self.result_memory)
        self.approach = TargetApproach(self.client, self.config)
        self.action_selector = ActionSelector(
            loot_stop_distance=self.config.approach_stop_distance_loot,
            resource_stop_distance=self.config.approach_stop_distance_resource,
            entity_stop_distance=self.config.approach_stop_distance_entity,
        )
        self.build_executor = BuildExecutor(self.client, approach=self.approach)

    def _refresh_observation_cache_locked(self):
        try:
            state = self.client.get_state()
            environment_summary = self.client.get_environment_summary()
            self._cached_state = state
            self._cached_environment_summary = environment_summary
            self._connection_state = "connected"
            self._last_connection_error = ""
            self._last_connection_log_signature = ""
            return state, environment_summary
        except (BridgeConnectionError, BridgeProtocolError, BridgeApiError) as exc:
            self._cached_state = None
            self._cached_environment_summary = None
            self._record_connection_error(exc)
            return None, None

    def _record_connection_error(self, exc: Exception) -> None:
        self._connection_state = "disconnected"
        self._last_connection_error = f"{type(exc).__name__}:{exc}"
        signature = self._last_connection_error
        if signature != self._last_connection_log_signature:
            self.logs.append(f"bridge_unavailable:{signature}")
            self._last_connection_log_signature = signature

    def _safe_stop_all_locked(self) -> None:
        try:
            self.client.stop_all()
        except (BridgeConnectionError, BridgeProtocolError, BridgeApiError) as exc:
            self._record_connection_error(exc)
