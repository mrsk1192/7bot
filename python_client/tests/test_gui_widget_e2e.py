from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace

import pytest
import tkinter as tk

from sevendays_bridge.bases import BaseRegistry
from sevendays_bridge.build import BuildPlan, BuildStep, BuildStepType
from sevendays_bridge.commanding import CommandActionType, CommandQueue
from sevendays_bridge.exceptions import BridgeConnectionError
from sevendays_bridge.gui import AgentControlPanel, AgentGuiAutomationDriver
from sevendays_bridge.runtime import AgentController, AgentGuiRuntimeAdapter, SessionStore


@dataclass
class Vec3:
    x: float
    y: float
    z: float


class FakeClient:
    def __init__(self):
        self.stop_calls = 0
        self.attack_calls = 0
        self.hotbar_calls: list[int] = []
        self.state = SimpleNamespace(
            player=SimpleNamespace(
                hp=95.0,
                max_hp=100.0,
                stamina=70.0,
                max_stamina=100.0,
                food=60.0,
                water=80.0,
                is_dead=False,
                position=Vec3(12.0, 43.0, -8.0),
            ),
            resource_observation=SimpleNamespace(
                biome="pine_forest",
                look_target=SimpleNamespace(target_kind="loot", target_name="cntBirdnest"),
            ),
            nearby_resource_candidates_summary=SimpleNamespace(count=3),
            nearby_interactables_summary=SimpleNamespace(count=2),
            nearby_entities_summary=SimpleNamespace(hostile_count=0, nearest_hostile_distance=None, top_entities=[1, 2]),
        )
        self.environment = SimpleNamespace(fall_hazard_ahead_hint=False)

    def get_state(self):
        return self.state

    def get_environment_summary(self):
        return self.environment

    def get_terrain_summary(self):
        return SimpleNamespace(height_span=0.3)

    def get_biome_info(self):
        return SimpleNamespace(current_biome="pine_forest")

    def stop_all(self):
        self.stop_calls += 1
        return SimpleNamespace(accepted=True)

    def select_hotbar_slot(self, slot: int):
        self.hotbar_calls.append(slot)
        return SimpleNamespace(accepted=True)

    def attack_primary(self, duration_ms: int = 250):
        self.attack_calls += 1
        return SimpleNamespace(accepted=True)


class DisconnectedClient:
    def __init__(self):
        self.stop_calls = 0

    def get_state(self):
        raise BridgeConnectionError("bridge offline")

    def get_environment_summary(self):
        raise BridgeConnectionError("bridge offline")

    def get_terrain_summary(self):
        raise BridgeConnectionError("bridge offline")

    def get_biome_info(self):
        raise BridgeConnectionError("bridge offline")

    def stop_all(self):
        self.stop_calls += 1
        raise BridgeConnectionError("bridge offline")


class FakeApproach:
    def __init__(self):
        self.heading = SimpleNamespace(align_view_to_position=lambda *args, **kwargs: True)
        self.recovery = SimpleNamespace(
            execute_recovery_full=lambda **kwargs: SimpleNamespace(status="unreachable", reason="blocked", jump_attempted=False)
        )

    def approach_with_details(self, target_position, target_name, target_kind):
        return SimpleNamespace(status="success", reason="ok", recovery_attempts=0, alignment_attempts=0, final_distance=1.0)


class FakeSearchLoop:
    def __init__(self, final_state: str = "VERIFY_RESULT", failure_reason: str = ""):
        self.final_state = final_state
        self.failure_reason = failure_reason

    def run_cycle(self):
        return SimpleNamespace(final_state=self.final_state, failure_reason=self.failure_reason, verification_ok=True)


@pytest.fixture(scope="module")
def gui_root():
    root = tk.Tk()
    root.withdraw()
    yield root
    root.destroy()


def build_panel(client, gui_root):
    controller = AgentController(client)
    controller.approach = FakeApproach()
    controller.search_loop = FakeSearchLoop()
    controller.build_executor.approach = controller.approach
    controller.build_plan_registry.add_or_update(
        BuildPlan(
            build_plan_id="starter-plan",
            name="Starter Base",
            description="simple starter",
            steps=[
                BuildStep(
                    step_id="floor-1",
                    step_type=BuildStepType.FLOOR_FOUNDATION,
                    target_position=Vec3(1, 0, 1),
                    preferred_hotbar_slot=2,
                )
            ],
        )
    )
    window = tk.Toplevel(gui_root)
    window.withdraw()
    panel = AgentControlPanel(AgentGuiRuntimeAdapter(controller).build_callbacks(), root=window)
    driver = AgentGuiAutomationDriver(panel)
    panel.refresh()
    return controller, panel, driver


def teardown_panel(panel):
    panel.root.update_idletasks()
    panel.root.destroy()


def test_gui_widget_start_stop_and_status_fields(gui_root):
    controller, panel, driver = build_panel(FakeClient(), gui_root)
    try:
        assert driver.label_text("status_label:connection_state") == "接続済み"
        assert driver.label_text("status_label:player_position") == "(12.0, 43.0, -8.0)"
        assert driver.label_text("status_label:biome") == "pine_forest"
        assert driver.label_text("status_label:look_target") == "loot:cntBirdnest"
        driver.click("button:start_agent")
        assert controller.is_agent_running() is True
        assert driver.label_text("status_label:agent_state") == "実行中"
        driver.click("button:stop_agent")
        assert controller.is_agent_running() is False
        assert driver.label_text("status_label:agent_state") == "停止中"
    finally:
        teardown_panel(panel)


def test_gui_widget_command_queue_flow(gui_root):
    controller, panel, driver = build_panel(FakeClient(), gui_root)
    try:
        driver.click("button:start_agent")
        driver.set_combobox("command_form:action_type", "指定地点へ移動")
        driver.set_entry("command_form:target_x", "10")
        driver.set_entry("command_form:target_y", "0")
        driver.set_entry("command_form:target_z", "20")
        driver.set_entry("command_form:priority", "100")
        driver.set_combobox("command_form:interruptible", "中断可")
        driver.set_entry("command_form:timeout_seconds", "30")
        driver.click("button:command_submit")
        rows = driver.tree_rows("tree:commands")
        assert len(rows) == 1
        command_id = rows[0][0]
        driver.select_tree_item("tree:commands", command_id)
        driver.click("button:command_pause")
        assert controller.command_queue.get(command_id).status.value == "paused"
        driver.click("button:command_resume")
        assert controller.command_queue.get(command_id).status.value == "queued"
        driver.click("button:command_interrupt")
        assert controller.command_queue.get(command_id).status.value == "interrupted"
        driver.click("button:command_cancel")
        assert controller.command_queue.get(command_id).status.value == "cancelled"
    finally:
        teardown_panel(panel)


def test_gui_widget_base_and_build_flow(gui_root):
    controller, panel, driver = build_panel(FakeClient(), gui_root)
    try:
        driver.set_entry("base_form:base_id", "base-alpha")
        driver.set_entry("base_form:base_name", "Alpha")
        driver.set_entry("base_form:anchor_x", "1")
        driver.set_entry("base_form:anchor_y", "0")
        driver.set_entry("base_form:anchor_z", "1")
        driver.set_entry("base_form:min_x", "-2")
        driver.set_entry("base_form:min_y", "0")
        driver.set_entry("base_form:min_z", "-2")
        driver.set_entry("base_form:max_x", "2")
        driver.set_entry("base_form:max_y", "3")
        driver.set_entry("base_form:max_z", "2")
        driver.set_entry("base_form:safety_score", "0.7")
        driver.set_entry("base_form:build_plan_id", "starter-plan")
        driver.click("button:base_submit")
        rows = driver.tree_rows("tree:bases")
        assert rows[0][0] == "base-alpha"
        driver.select_tree_item("tree:bases", "base-alpha")
        driver.click("button:base_start_build")
        assert any(command.action_type == CommandActionType.BUILD for command in controller.command_queue.list_commands())
    finally:
        teardown_panel(panel)


def test_gui_widget_scan_settings_validation_and_persistence(gui_root):
    client = FakeClient()
    with TemporaryDirectory() as temp_dir:
        session_path = Path(temp_dir) / "agent_gui_session.json"
        controller, panel, driver = build_panel(client, gui_root)
        try:
            store = SessionStore(session_path)
            driver.set_entry("scan_form:yaw_scan_min_deg", "-60")
            driver.set_entry("scan_form:yaw_scan_max_deg", "60")
            driver.set_entry("scan_form:yaw_scan_step_deg", "15")
            driver.set_entry("scan_form:pitch_scan_min_deg", "-30")
            driver.set_entry("scan_form:pitch_scan_max_deg", "0")
            driver.set_entry("scan_form:pitch_scan_step_deg", "10")
            driver.set_entry("scan_form:scan_settle_delay_ms", "75")
            driver.set_checkbutton("scan_form:near_ground_priority_enabled", False)
            driver.set_entry("scan_form:near_ground_distance_threshold", "4.0")
            driver.click("button:scan_submit")
            assert controller.get_navigation_config().generate_yaw_scan_angles() == [-60, -45, -30, -15, 0, 15, 30, 45, 60]
            assert controller.get_navigation_config().generate_pitch_scan_angles() == [0, -10, -20, -30]

            store.save(
                controller.command_queue,
                controller.base_registry,
                navigation_config=controller.get_navigation_config(),
                last_interrupt_reason=controller.last_interrupt_reason,
            )

            restored_queue = CommandQueue()
            restored_bases = BaseRegistry()
            loaded = store.load_into(restored_queue, restored_bases)
            restored = loaded["navigation_config"]
            assert restored.generate_yaw_scan_angles() == [-60, -45, -30, -15, 0, 15, 30, 45, 60]
            assert restored.generate_pitch_scan_angles() == [0, -10, -20, -30]

            driver.set_entry("scan_form:yaw_scan_step_deg", "0")
            driver.click("button:scan_submit")
            assert "yaw_scan_step_deg" in driver.label_text("label:scan_error")
            assert controller.get_navigation_config().yaw_scan_step_deg == 15
        finally:
            teardown_panel(panel)


def test_gui_widget_reset_and_disconnected_error_display(gui_root):
    controller, panel, driver = build_panel(DisconnectedClient(), gui_root)
    try:
        panel.refresh()
        assert driver.label_text("status_label:connection_state") == "未接続"
        driver.click("button:start_agent")
        result = controller.tick()
        panel.refresh()
        assert result.tick_state == "bridge_unavailable"
        assert "BridgeConnectionError" in driver.label_text("status_label:last_error")
        driver.set_combobox("command_form:action_type", "待機")
        driver.click("button:command_submit")
        assert len(driver.tree_rows("tree:commands")) == 1
        driver.click("button:reset_agent")
        assert len(driver.tree_rows("tree:commands")) == 0
        assert controller.is_agent_running() is False
    finally:
        teardown_panel(panel)
