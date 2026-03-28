from __future__ import annotations

from dataclasses import dataclass, field
from typing import Dict, List

from sevendays_bridge.runtime.form_models import BaseFormData, CommandFormData, ScanSettingsFormData


@dataclass(frozen=True)
class AgentCommandView:
    command_id: str
    action_type: str
    status: str
    priority: int
    summary: str


@dataclass(frozen=True)
class BaseViewModel:
    base_id: str
    base_name: str
    build_plan_id: str


@dataclass(frozen=True)
class AgentStatusViewModel:
    current_action: str
    current_target: str
    interrupt_reason: str
    health: str
    water: str
    hunger: str
    stamina: str
    debuffs: str
    carried_weight: str
    equipment_state: str
    agent_state: str = ""
    connection_state: str = ""
    player_position: str = ""
    biome: str = ""
    look_target: str = ""
    nearby_resource_count: str = ""
    nearby_interactable_count: str = ""
    nearby_entity_count: str = ""
    last_error: str = ""
    command_queue: List[AgentCommandView] = field(default_factory=list)
    bases: List[BaseViewModel] = field(default_factory=list)
    logs: List[str] = field(default_factory=list)


@dataclass(frozen=True)
class PanelSnapshot:
    status: AgentStatusViewModel
    selected_command_id: str
    selected_base_id: str
    command_form: CommandFormData
    base_form: BaseFormData
    scan_settings_form: ScanSettingsFormData
