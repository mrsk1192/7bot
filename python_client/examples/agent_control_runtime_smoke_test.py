from __future__ import annotations

import sys
import tkinter as tk
from dataclasses import dataclass
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
if str(SRC) not in sys.path:
    sys.path.insert(0, str(SRC))

from sevendays_bridge.bases import BaseBounds, BaseDefinition, BaseRegistry
from sevendays_bridge.build import BuildPlan, BuildPlanRegistry, BuildStep, BuildStepType
from sevendays_bridge.commanding import AgentCommand, CommandActionType, CommandInterruptPolicy, CommandPriority, CommandQueue
from sevendays_bridge.exceptions import BridgeConnectionError
from sevendays_bridge.gui import AgentCommandView, AgentControlPanel, AgentStatusViewModel, BaseViewModel
from sevendays_bridge.movement import NavigationConfig
from sevendays_bridge.priority import EquipmentStatus, InventoryStatus, PriorityActionKind, PriorityMonitor, PrioritySeverity, PrioritySnapshot
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
        self.hotbar_calls = []
        self.state = SimpleNamespace(
            player=SimpleNamespace(hp=100.0, max_hp=100.0, stamina=80.0, max_stamina=100.0, food=80.0, water=80.0, is_dead=False),
            nearby_entities_summary=SimpleNamespace(hostile_count=0, nearest_hostile_distance=None),
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

    def select_hotbar_slot(self, slot):
        self.hotbar_calls.append(slot)
        return SimpleNamespace(accepted=True)

    def attack_primary(self, duration_ms=250):
        self.attack_calls += 1
        return SimpleNamespace(accepted=True, duration_ms=duration_ms)


class FakeApproach:
    def __init__(self):
        self.calls = []
        self.heading = SimpleNamespace(align_view_to_position=lambda *args, **kwargs: True)
        self.recovery = SimpleNamespace(execute_recovery_full=lambda **kwargs: SimpleNamespace(status="unreachable", reason="blocked", jump_attempted=False))

    def approach_with_details(self, target_position, target_name, target_kind):
        self.calls.append((target_position, target_name, target_kind))
        return SimpleNamespace(status="success", reason="ok", recovery_attempts=0, alignment_attempts=0, final_distance=1.5)


class FakeSearchLoop:
    def __init__(self):
        self.calls = 0

    def run_cycle(self):
        self.calls += 1
        return SimpleNamespace(final_state="VERIFY_RESULT", failure_reason="", verification_ok=True)


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


def ensure(condition, message):
    if not condition:
        raise AssertionError(message)


def test_command_queue_operations():
    queue = CommandQueue()
    low = AgentCommand.new(CommandActionType.WAIT, priority=CommandPriority.LOW)
    high = AgentCommand.new(CommandActionType.WAIT, priority=CommandPriority.HIGH)
    queue.enqueue(low)
    queue.enqueue(high)
    ensure(queue.list_commands()[0].command_id == high.command_id, "higher priority commands should be ordered first")
    running = queue.start_next()
    ensure(running.command_id == high.command_id, "start_next should start the highest priority queued command")
    queue.force_interrupt_current(resume_token="resume:1")
    ensure(queue.get(high.command_id).status.value == "interrupted", "current command should become interrupted")


def test_priority_monitor_interrupt():
    client = FakeClient()
    client.state.player.hp = 10.0
    decisions = PriorityMonitor().evaluate(
        client.get_state(),
        environment_summary=client.get_environment_summary(),
        snapshot=PrioritySnapshot(),
    )
    ensure(decisions[0].action_kind == PriorityActionKind.RECOVER_HEALTH, "low hp should produce health recovery")
    ensure(decisions[0].severity == PrioritySeverity.WARNING, "low hp should be warning severity")


def test_base_registry_and_build_plan():
    registry = BaseRegistry()
    base = BaseDefinition(
        base_id="base-alpha",
        base_name="Alpha",
        anchor_position=Vec3(0, 0, 0),
        bounds=BaseBounds(-5, 0, -5, 5, 5, 5),
        safety_score=0.8,
        build_plan_id="starter-plan",
    )
    registry.add_or_update(base)
    ensure(registry.get("base-alpha").base_name == "Alpha", "base registry should return stored base")

    plans = BuildPlanRegistry()
    plans.add_or_update(
        BuildPlan(
            build_plan_id="starter-plan",
            name="Starter Base",
            description="simple starter",
            steps=[BuildStep(step_id="floor-1", step_type=BuildStepType.FLOOR_FOUNDATION, target_position=Vec3(1, 0, 1), preferred_hotbar_slot=2)],
        )
    )
    ensure(plans.get("starter-plan").steps[0].step_id == "floor-1", "build plan registry should store steps")


def test_agent_controller_move_and_build():
    client = FakeClient()
    controller = AgentController(client)
    controller.approach = FakeApproach()
    controller.search_loop = FakeSearchLoop()
    controller.build_executor.approach = controller.approach

    base = BaseDefinition(
        base_id="base-alpha",
        base_name="Alpha",
        anchor_position=Vec3(0, 0, 0),
        bounds=BaseBounds(-5, 0, -5, 5, 5, 5),
        safety_score=0.9,
        access_points=[Vec3(0, 0, 1)],
        build_plan_id="starter-plan",
    )
    controller.base_registry.add_or_update(base)
    controller.build_plan_registry.add_or_update(
        BuildPlan(
            build_plan_id="starter-plan",
            name="Starter Base",
            description="simple starter",
            steps=[BuildStep(step_id="floor-1", step_type=BuildStepType.FLOOR_FOUNDATION, target_position=Vec3(1, 0, 1), preferred_hotbar_slot=2)],
        )
    )

    move = AgentCommand.new(CommandActionType.MOVE_TO_POSITION, target_position=Vec3(4, 0, 4))
    build = AgentCommand.new(CommandActionType.BUILD, base_id="base-alpha")
    controller.command_queue.enqueue(move)
    controller.command_queue.enqueue(build)
    controller.start_agent()

    first = controller.tick()
    second = controller.tick()
    ensure(first.tick_state == "completed_move", "move command should complete")
    ensure(second.tick_state in {"build_step_completed", "completed_build_plan"}, "build command should execute a step")
    ensure(client.attack_calls == 1, "build execution should use primary action for deterministic placement")
    ensure(client.hotbar_calls == [2], "build step should select the preferred hotbar slot")


def test_agent_controller_priority_interrupt():
    client = FakeClient()
    controller = AgentController(client)
    controller.approach = FakeApproach()
    controller.search_loop = FakeSearchLoop()
    command = AgentCommand.new(
        CommandActionType.SEARCH_RESOURCE,
        interruptible=CommandInterruptPolicy.INTERRUPTIBLE_ONLY_WHEN_CRITICAL,
    )
    controller.command_queue.enqueue(command)
    controller.start_agent()
    result = controller.tick(
        snapshot=PrioritySnapshot(
            continuous_damage=True,
            equipment_status=EquipmentStatus(),
            inventory_status=InventoryStatus(),
        )
    )
    ensure(result.tick_state == "priority_interrupt", "critical priority should interrupt running work")
    ensure(client.stop_calls >= 1, "interrupt should stop all input")


def test_gui_view_models_shape():
    model = AgentStatusViewModel(
        current_action="search_resource",
        current_target="tree",
        interrupt_reason="",
        health="90/100",
        water="80",
        hunger="70",
        stamina="60/100",
        debuffs="none",
        carried_weight="20/100",
        equipment_state="ok",
        command_queue=[AgentCommandView("1", "move_to_position", "queued", 50, "move to x=1")],
        bases=[BaseViewModel("base-alpha", "Alpha", "starter-plan")],
        logs=["started"],
    )
    ensure(model.command_queue[0].action_type == "move_to_position", "GUI view model should expose queued commands")


def test_gui_automation_flow():
    client = FakeClient()
    controller = AgentController(client)
    controller.approach = FakeApproach()
    controller.search_loop = FakeSearchLoop()

    root = tk.Tk()
    root.withdraw()
    panel = AgentControlPanel(AgentGuiRuntimeAdapter(controller).build_callbacks(), root=root)
    panel.invoke_runtime_action("start")
    ensure(controller.is_agent_running(), "GUI start should enable the agent runtime")

    panel.set_command_form_values(
        action_type="move_to_position",
        target_x="10",
        target_y="0",
        target_z="20",
        priority="100",
        interruptible="interruptible",
        timeout_seconds="15",
        retry_max_attempts="3",
        metadata_json="{\"tag\": \"gui\"}",
    )
    panel.submit_command_form()
    panel.refresh()
    snapshot = panel.snapshot()
    ensure(len(snapshot.status.command_queue) == 1, "GUI submit should enqueue one command")
    command_id = snapshot.status.command_queue[0].command_id

    panel.select_command(command_id)
    panel.invoke_command_action("pause")
    panel.refresh()
    ensure(controller.command_queue.get(command_id).status.value == "paused", "GUI pause should update command state")

    panel.set_base_form_values(
        base_id="base-gui",
        base_name="GUI Base",
        anchor_x="1",
        anchor_y="0",
        anchor_z="1",
        min_x="-2",
        min_y="0",
        min_z="-2",
        max_x="2",
        max_y="3",
        max_z="2",
        safety_score="0.7",
        build_plan_id="starter-plan",
    )
    controller.build_plan_registry.add_or_update(
        BuildPlan(
            build_plan_id="starter-plan",
            name="Starter Base",
            description="simple starter",
            steps=[BuildStep(step_id="floor-1", step_type=BuildStepType.FLOOR_FOUNDATION, target_position=Vec3(1, 0, 1), preferred_hotbar_slot=2)],
        )
    )
    panel.submit_base_form()
    panel.refresh()
    ensure(controller.base_registry.get("base-gui") is not None, "GUI base submit should upsert a base")
    panel.select_base("base-gui")
    panel.invoke_base_action("start_build")
    panel.refresh()
    ensure(any(command.action_type == CommandActionType.BUILD for command in controller.command_queue.list_commands()), "GUI build action should enqueue build command")

    panel.set_scan_settings_form_values(
        yaw_scan_min_deg="-60",
        yaw_scan_max_deg="60",
        yaw_scan_step_deg="15",
        pitch_scan_min_deg="-30",
        pitch_scan_max_deg="0",
        pitch_scan_step_deg="10",
        scan_settle_delay_ms="75",
        near_ground_priority_enabled=False,
        near_ground_distance_threshold="4.0",
    )
    panel.submit_scan_settings_form()
    updated_config = controller.get_navigation_config()
    ensure(updated_config.generate_yaw_scan_angles() == [-60, -45, -30, -15, 0, 15, 30, 45, 60], "GUI scan settings should update yaw generation")
    ensure(updated_config.generate_pitch_scan_angles() == [0, -10, -20, -30], "GUI scan settings should update pitch generation")
    panel.invoke_runtime_action("stop")
    ensure(not controller.is_agent_running(), "GUI stop should disable the agent runtime")
    root.destroy()


def test_disconnected_bridge_does_not_break_gui():
    controller = AgentController(DisconnectedClient())

    root = tk.Tk()
    root.withdraw()
    panel = AgentControlPanel(AgentGuiRuntimeAdapter(controller).build_callbacks(), root=root)

    panel.refresh()
    snapshot = panel.snapshot()
    ensure(snapshot.status.connection_state == "disconnected", "disconnected bridge should be reflected in GUI status")

    panel.invoke_runtime_action("start")
    tick_result = controller.tick()
    ensure(tick_result.tick_state == "bridge_unavailable", "disconnected bridge should not crash runtime ticks")

    command = AgentCommand.new(CommandActionType.WAIT)
    controller.queue_command(command)
    panel.select_command(command.command_id)
    panel.invoke_command_action("pause")
    panel.invoke_command_action("cancel")
    panel.invoke_runtime_action("stop")
    ensure(not controller.is_agent_running(), "GUI stop should remain safe when the bridge is unavailable")
    ensure(any("bridge_unavailable:" in entry for entry in controller.logs), "disconnected errors should be logged once")
    root.destroy()


def test_navigation_config_generation_and_session_restore():
    config = NavigationConfig()
    ensure(config.generate_yaw_scan_angles() == list(range(-90, 95, 5)), "default yaw generation should be -90..90 step 5")
    ensure(config.generate_pitch_scan_angles() == [0, -15, -30, -45], "default pitch generation should scan downward from zero")

    custom = NavigationConfig(
        yaw_scan_min_deg=-60,
        yaw_scan_max_deg=60,
        yaw_scan_step_deg=15,
        pitch_scan_min_deg=-30,
        pitch_scan_max_deg=0,
        pitch_scan_step_deg=10,
    )
    ensure(custom.generate_yaw_scan_angles() == [-60, -45, -30, -15, 0, 15, 30, 45, 60], "custom yaw generation must honor config")
    ensure(custom.generate_pitch_scan_angles() == [0, -10, -20, -30], "custom pitch generation must honor config")

    with TemporaryDirectory() as temp_dir:
        store = SessionStore(Path(temp_dir) / "agent_gui_session.json")
        controller = AgentController(FakeClient())
        controller.apply_navigation_config(custom)
        store.save(controller.command_queue, controller.base_registry, navigation_config=controller.get_navigation_config(), last_interrupt_reason="none")

        restored_queue = CommandQueue()
        restored_bases = BaseRegistry()
        loaded = store.load_into(restored_queue, restored_bases)
        restored_config = loaded["navigation_config"]
        ensure(restored_config.generate_yaw_scan_angles() == custom.generate_yaw_scan_angles(), "session restore should recover saved yaw config")
        ensure(restored_config.generate_pitch_scan_angles() == custom.generate_pitch_scan_angles(), "session restore should recover saved pitch config")


def test_navigation_config_validation():
    errors = NavigationConfig(
        yaw_scan_min_deg=-90,
        yaw_scan_max_deg=90,
        yaw_scan_step_deg=0,
        pitch_scan_min_deg=-45,
        pitch_scan_max_deg=0,
        pitch_scan_step_deg=15,
    ).validate_scan_settings()
    ensure(any("yaw_scan_step_deg" in error for error in errors), "invalid yaw step should be rejected")


def main():
    test_command_queue_operations()
    test_priority_monitor_interrupt()
    test_base_registry_and_build_plan()
    test_agent_controller_move_and_build()
    test_agent_controller_priority_interrupt()
    test_gui_view_models_shape()
    test_gui_automation_flow()
    test_disconnected_bridge_does_not_break_gui()
    test_navigation_config_generation_and_session_restore()
    test_navigation_config_validation()
    print("Agent control runtime smoke test passed.")


if __name__ == "__main__":
    main()
