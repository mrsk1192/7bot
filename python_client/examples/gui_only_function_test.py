from __future__ import annotations

import sys
import tkinter as tk
from dataclasses import dataclass
from pathlib import Path
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
if str(SRC) not in sys.path:
    sys.path.insert(0, str(SRC))

from sevendays_bridge.build import BuildPlan, BuildStep, BuildStepType
from sevendays_bridge.gui import AgentControlPanel
from sevendays_bridge.runtime import AgentController, AgentGuiRuntimeAdapter


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
            player=SimpleNamespace(hp=95.0, max_hp=100.0, stamina=75.0, max_stamina=100.0, food=70.0, water=80.0, is_dead=False),
            nearby_entities_summary=SimpleNamespace(hostile_count=0, nearest_hostile_distance=None),
        )
        self.environment = SimpleNamespace(fall_hazard_ahead_hint=False)

    def get_state(self):
        return self.state

    def get_environment_summary(self):
        return self.environment

    def get_terrain_summary(self):
        return SimpleNamespace(height_span=0.2)

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
        return SimpleNamespace(accepted=True)


class FakeApproach:
    def __init__(self):
        self.heading = SimpleNamespace(align_view_to_position=lambda *args, **kwargs: True)
        self.recovery = SimpleNamespace(
            execute_recovery_full=lambda **kwargs: SimpleNamespace(status="unreachable", reason="blocked", jump_attempted=False)
        )

    def approach_with_details(self, target_position, target_name, target_kind):
        return SimpleNamespace(status="success", reason="ok", recovery_attempts=0, alignment_attempts=0, final_distance=1.0)


class FakeSearchLoop:
    def run_cycle(self):
        return SimpleNamespace(final_state="VERIFY_RESULT", failure_reason="", verification_ok=True)


def ensure(condition, message):
    if not condition:
        raise AssertionError(message)


def main():
    client = FakeClient()
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

    root = tk.Tk()
    root.withdraw()
    panel = AgentControlPanel(AgentGuiRuntimeAdapter(controller).build_callbacks(), root=root)
    panel.invoke_runtime_action("start")
    ensure(controller.is_agent_running(), "GUI start should toggle the runtime on")

    panel.set_command_form_values(
        action_type="move_to_position",
        target_x="10",
        target_y="0",
        target_z="20",
        priority="100",
        interruptible="interruptible",
        timeout_seconds="15",
        retry_max_attempts="3",
        metadata_json="{\"source\": \"gui-test\"}",
    )
    panel.submit_command_form()
    snapshot = panel.snapshot()
    ensure(len(snapshot.status.command_queue) == 1, "GUI should enqueue one command")
    first_command_id = snapshot.status.command_queue[0].command_id

    panel.select_command(first_command_id)
    panel.set_command_form_values(
        action_type="move_to_position",
        target_x="12",
        target_y="0",
        target_z="24",
        priority="200",
        interruptible="interruptible_only_when_critical",
        timeout_seconds="20",
        retry_max_attempts="4",
        metadata_json="{\"source\": \"gui-edit\"}",
    )
    panel.submit_command_form()
    snapshot = panel.snapshot()
    ensure(snapshot.status.command_queue[0].priority == 200, "GUI edit should update priority")

    panel.clear_command_selection()
    panel.set_command_form_values(
        action_type="wait",
        target_x="",
        target_y="",
        target_z="",
        priority="10",
        interruptible="interruptible",
        timeout_seconds="5",
        retry_max_attempts="1",
        metadata_json="{}",
    )
    panel.submit_command_form()
    snapshot = panel.snapshot()
    ensure(len(snapshot.status.command_queue) == 2, "GUI should enqueue second command")
    second_command_id = next(item.command_id for item in snapshot.status.command_queue if item.command_id != first_command_id)
    panel.select_command(second_command_id)
    panel.invoke_command_action("up")
    snapshot = panel.snapshot()
    ensure(snapshot.status.command_queue[0].command_id == second_command_id, "GUI reorder should move the selected command upward")

    panel.select_command(first_command_id)
    panel.invoke_command_action("pause")
    ensure(controller.command_queue.get(first_command_id).status.value == "paused", "GUI pause should set paused")
    panel.invoke_command_action("resume")
    ensure(controller.command_queue.get(first_command_id).status.value == "queued", "GUI resume should return command to queued")
    panel.invoke_command_action("interrupt")
    ensure(controller.command_queue.get(first_command_id).status.value == "interrupted", "GUI interrupt should set interrupted")
    panel.invoke_command_action("cancel")
    ensure(controller.command_queue.get(first_command_id).status.value == "cancelled", "GUI cancel should set cancelled")

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
    panel.submit_base_form()
    snapshot = panel.snapshot()
    ensure(len(snapshot.status.bases) == 1, "GUI should add one base")
    ensure(snapshot.status.bases[0].base_id == "base-gui", "GUI base id should be reflected")

    panel.clear_base_selection()
    panel.select_base("base-gui")
    panel.invoke_base_action("start_build")
    snapshot = panel.snapshot()
    ensure(any(item.action_type == "build" for item in snapshot.status.command_queue), "GUI build action should enqueue build command")

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
    config = controller.get_navigation_config()
    ensure(config.generate_yaw_scan_angles() == [-60, -45, -30, -15, 0, 15, 30, 45, 60], "GUI scan settings should update yaw generation")
    ensure(config.generate_pitch_scan_angles() == [0, -10, -20, -30], "GUI scan settings should update pitch generation")

    panel.set_scan_settings_form_values(yaw_scan_step_deg="0")
    panel.submit_scan_settings_form()
    config_after_invalid = controller.get_navigation_config()
    ensure(config_after_invalid.yaw_scan_step_deg == 15, "invalid GUI scan settings must not overwrite the last valid config")

    snapshot = panel.snapshot()
    ensure(snapshot.status.current_action != "", "GUI status should expose current action field")
    ensure(snapshot.status.health != "", "GUI status should expose health field")
    panel.invoke_runtime_action("stop")
    ensure(not controller.is_agent_running(), "GUI stop should toggle the runtime off")

    root.destroy()
    print("GUI-only functional test passed.")


if __name__ == "__main__":
    main()
