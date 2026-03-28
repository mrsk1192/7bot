from __future__ import annotations

import json
import uuid
from dataclasses import replace

from sevendays_bridge.bases import BaseBounds, BaseDefinition
from sevendays_bridge.commanding import (
    AgentCommand,
    CommandActionType,
    CommandInterruptPolicy,
    CommandPriority,
    CommandRetryPolicy,
)
from sevendays_bridge.gui import AgentCommandView, AgentControlCallbacks, AgentStatusViewModel, BaseViewModel

from .form_models import BaseFormData, CommandFormData, ScanSettingsFormData

from .agent_controller import AgentController


class AgentGuiRuntimeAdapter:
    """Bridges GUI form DTOs to runtime/controller DTOs without putting logic in the GUI."""

    def __init__(self, controller: AgentController):
        self.controller = controller

    def build_callbacks(self) -> AgentControlCallbacks:
        return AgentControlCallbacks(
            refresh_status=self.build_status_view_model,
            start_agent=self.controller.start_agent,
            stop_agent=self.controller.stop_agent,
            reset_agent=self.controller.reset_agent,
            submit_command_form=self.submit_command_form,
            delete_selected_command=self.delete_selected_command,
            move_selected_up=self.move_selected_up,
            move_selected_down=self.move_selected_down,
            pause_selected_command=self.pause_selected_command,
            resume_selected_command=self.resume_selected_command,
            cancel_selected_command=self.cancel_selected_command,
            interrupt_selected_command=self.interrupt_selected_command,
            submit_base_form=self.submit_base_form,
            start_build_for_selected_base=self.start_build_for_selected_base,
            read_scan_settings=self.read_scan_settings,
            submit_scan_settings_form=self.submit_scan_settings_form,
        )

    def build_status_view_model(self, snapshot=None) -> AgentStatusViewModel:
        raw = self.controller.get_raw_status(snapshot)
        return AgentStatusViewModel(
            current_action=raw["current_action"],
            current_target=raw["current_target"],
            interrupt_reason=raw["interrupt_reason"],
            health=raw["health"],
            water=raw["water"],
            hunger=raw["hunger"],
            stamina=raw["stamina"],
            debuffs=raw["debuffs"],
            carried_weight=raw["carried_weight"],
            equipment_state=raw["equipment_state"],
            agent_state=raw["agent_state"],
            connection_state=raw["connection_state"],
            player_position=raw["player_position"],
            biome=raw["biome"],
            look_target=raw["look_target"],
            nearby_resource_count=raw["nearby_resource_count"],
            nearby_interactable_count=raw["nearby_interactable_count"],
            nearby_entity_count=raw["nearby_entity_count"],
            last_error=raw["last_error"],
            command_queue=[
                AgentCommandView(
                    command_id=command["command_id"],
                    action_type=command["action_type"],
                    status=command["status"],
                    priority=command["priority"],
                    summary=command["summary"],
                )
                for command in raw["commands"]
            ],
            bases=[
                BaseViewModel(
                    base_id=base["base_id"],
                    base_name=base["base_name"],
                    build_plan_id=base["build_plan_id"],
                )
                for base in raw["bases"]
            ],
            logs=raw["logs"],
        )

    def submit_command_form(self, form: CommandFormData, selected_command_id: str | None) -> None:
        action_type = CommandActionType(form.action_type)
        priority = self._parse_priority(form.priority)
        interruptible = CommandInterruptPolicy(form.interruptible)
        retry_policy = CommandRetryPolicy(max_attempts=int(form.retry_max_attempts))
        metadata = self._parse_json(form.metadata_json)
        position = self._parse_position(form.target_x, form.target_y, form.target_z)

        if selected_command_id:
            self.controller.edit_command(
                selected_command_id,
                action_type=action_type,
                target_position=position,
                target_entity=form.target_entity or None,
                target_resource_type=form.target_resource_type or None,
                target_area=form.target_area or None,
                base_id=form.base_id or None,
                priority=priority,
                interruptible=interruptible,
                retry_policy=retry_policy,
                timeout_seconds=float(form.timeout_seconds),
                metadata=metadata,
            )
            return

        self.controller.queue_command(
            AgentCommand.new(
                action_type,
                target_position=position,
                target_entity=form.target_entity or None,
                target_resource_type=form.target_resource_type or None,
                target_area=form.target_area or None,
                base_id=form.base_id or None,
                priority=priority,
                interruptible=interruptible,
                retry_policy=retry_policy,
                timeout_seconds=float(form.timeout_seconds),
                metadata=metadata,
            )
        )

    def submit_base_form(self, form: BaseFormData, selected_base_id: str | None) -> None:
        base_id = selected_base_id or form.base_id or f"base-{uuid.uuid4()}"
        anchor = self._parse_position(form.anchor_x, form.anchor_y, form.anchor_z)
        bounds = BaseBounds(
            min_x=float(form.min_x or 0),
            min_y=float(form.min_y or 0),
            min_z=float(form.min_z or 0),
            max_x=float(form.max_x or 0),
            max_y=float(form.max_y or 0),
            max_z=float(form.max_z or 0),
        )
        self.controller.upsert_base(
            BaseDefinition(
                base_id=base_id,
                base_name=form.base_name or base_id,
                anchor_position=anchor,
                bounds=bounds,
                safety_score=float(form.safety_score or 0.5),
                build_plan_id=form.build_plan_id or None,
                build_area=bounds,
                defense_area=bounds,
            )
        )

    def delete_selected_command(self, command_id: str | None) -> None:
        if command_id:
            self.controller.delete_command(command_id)

    def move_selected_up(self, command_id: str | None) -> None:
        self._move_selected(command_id, delta=-1)

    def move_selected_down(self, command_id: str | None) -> None:
        self._move_selected(command_id, delta=1)

    def pause_selected_command(self, command_id: str | None) -> None:
        if command_id:
            self.controller.pause_command(command_id)

    def resume_selected_command(self, command_id: str | None) -> None:
        if command_id:
            self.controller.resume_command(command_id)

    def cancel_selected_command(self, command_id: str | None) -> None:
        if command_id:
            self.controller.cancel_command(command_id)

    def interrupt_selected_command(self, command_id: str | None) -> None:
        if command_id:
            self.controller.interrupt_command(command_id, reason="gui_interrupt")

    def start_build_for_selected_base(self, base_id: str | None) -> None:
        if base_id:
            self.controller.queue_build_for_base(base_id)

    def read_scan_settings(self) -> ScanSettingsFormData:
        config = self.controller.get_navigation_config()
        return ScanSettingsFormData(
            yaw_scan_min_deg=str(config.yaw_scan_min_deg),
            yaw_scan_max_deg=str(config.yaw_scan_max_deg),
            yaw_scan_step_deg=str(config.yaw_scan_step_deg),
            pitch_scan_min_deg=str(config.pitch_scan_min_deg),
            pitch_scan_max_deg=str(config.pitch_scan_max_deg),
            pitch_scan_step_deg=str(config.pitch_scan_step_deg),
            scan_settle_delay_ms=str(config.scan_settle_delay_ms),
            near_ground_priority_enabled=bool(config.near_ground_priority_enabled),
            near_ground_distance_threshold=str(config.near_ground_distance_threshold),
        )

    def submit_scan_settings_form(self, form: ScanSettingsFormData) -> None:
        current = self.controller.get_navigation_config()
        config = replace(
            current,
            yaw_scan_min_deg=int(form.yaw_scan_min_deg),
            yaw_scan_max_deg=int(form.yaw_scan_max_deg),
            yaw_scan_step_deg=int(form.yaw_scan_step_deg),
            pitch_scan_min_deg=int(form.pitch_scan_min_deg),
            pitch_scan_max_deg=int(form.pitch_scan_max_deg),
            pitch_scan_step_deg=int(form.pitch_scan_step_deg),
            scan_settle_delay_ms=int(form.scan_settle_delay_ms),
            near_ground_priority_enabled=bool(form.near_ground_priority_enabled),
            near_ground_distance_threshold=float(form.near_ground_distance_threshold),
        )
        self.controller.apply_navigation_config(config)

    def _move_selected(self, command_id: str | None, delta: int) -> None:
        if not command_id:
            return
        commands = self.controller.command_queue.list_commands()
        for index, command in enumerate(commands):
            if command.command_id == command_id:
                self.controller.move_command(command_id, index + delta)
                return

    @staticmethod
    def _parse_position(x: str, y: str, z: str):
        if x == "" and y == "" and z == "":
            return None

        class Position:
            def __init__(self, px: float, py: float, pz: float):
                self.x = px
                self.y = py
                self.z = pz

        return Position(float(x or 0), float(y or 0), float(z or 0))

    @staticmethod
    def _parse_json(raw: str):
        if not raw:
            return {}
        return json.loads(raw)

    @staticmethod
    def _parse_priority(raw: str) -> CommandPriority:
        value = int(raw)
        try:
            return CommandPriority(value)
        except ValueError:
            if value >= int(CommandPriority.URGENT):
                return CommandPriority.URGENT
            if value >= int(CommandPriority.HIGH):
                return CommandPriority.HIGH
            if value >= int(CommandPriority.NORMAL):
                return CommandPriority.NORMAL
            return CommandPriority.LOW
