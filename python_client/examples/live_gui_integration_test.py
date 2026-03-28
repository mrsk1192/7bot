from __future__ import annotations

import argparse
import json
import subprocess
import sys
import threading
import time
import tkinter as tk
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
if str(SRC) not in sys.path:
    sys.path.insert(0, str(SRC))

from sevendays_bridge import SevenDaysBridgeClient
from sevendays_bridge.gui import AgentControlPanel, AgentGuiAutomationDriver
from sevendays_bridge.runtime import AgentController, AgentGuiRuntimeAdapter, SessionStore


GAME_EXE = Path(r"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\7DaysToDie.exe")
GAME_CWD = GAME_EXE.parent
GAME_ARGS = ["-quick-continue", "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720"]


def wait_until(predicate, timeout: float, interval: float = 0.2) -> bool:
    deadline = time.time() + timeout
    while time.time() < deadline:
        if predicate():
            return True
        time.sleep(interval)
    return False


def refresh_and_check(panel, predicate) -> bool:
    panel.refresh()
    return predicate()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--launch-game", action="store_true")
    parser.add_argument("--report", default=str(ROOT / "artifacts" / "live_gui_integration_report.json"))
    parser.add_argument("--connect-timeout", type=float, default=180.0)
    args = parser.parse_args()

    report_path = Path(args.report)
    report_path.parent.mkdir(parents=True, exist_ok=True)

    game_process = None
    if args.launch_game:
        game_process = subprocess.Popen([str(GAME_EXE), *GAME_ARGS], cwd=str(GAME_CWD))

    client = SevenDaysBridgeClient()
    store = SessionStore(ROOT / "data" / "agent_gui_session.json")
    controller = AgentController(client)
    adapter = AgentGuiRuntimeAdapter(controller)

    loaded = store.load_into(controller.command_queue, controller.base_registry)
    if "navigation_config" in loaded:
        controller.apply_navigation_config(loaded["navigation_config"])
    controller.last_interrupt_reason = loaded.get("last_interrupt_reason", "")

    root = tk.Tk()
    panel = AgentControlPanel(adapter.build_callbacks(), root=root)
    driver = AgentGuiAutomationDriver(panel)
    stop_event = threading.Event()
    worker_thread: threading.Thread | None = None
    events: list[dict[str, object]] = []

    def snapshot(tag: str) -> None:
        panel.refresh()
        events.append(
            {
                "tag": tag,
                "connection_state": driver.label_text("status_label:connection_state"),
                "agent_state": driver.label_text("status_label:agent_state"),
                "current_action": driver.label_text("status_label:current_action"),
                "player_position": driver.label_text("status_label:player_position"),
                "look_target": driver.label_text("status_label:look_target"),
                "queue": driver.tree_rows("tree:commands"),
                "scan_error": driver.label_text("label:scan_error"),
            }
        )

    def on_controller_changed() -> None:
        try:
            root.after_idle(panel.refresh)
        except Exception:
            pass

    controller.add_state_change_listener(on_controller_changed)

    def runtime_worker() -> None:
        while not stop_event.is_set():
            try:
                if controller.is_agent_running():
                    if controller.has_pending_commands():
                        controller.tick()
                        wait_seconds = 1.0
                    else:
                        controller.refresh_observation_cache()
                        wait_seconds = 1.5
                else:
                    controller.refresh_observation_cache()
                    wait_seconds = 2.0
            except Exception as exc:
                controller.logs.append(f"runtime_tick_error:{type(exc).__name__}:{exc}")
                wait_seconds = 2.0
            stop_event.wait(wait_seconds)

    error_message = ""
    try:
        panel.refresh()
        worker_thread = threading.Thread(target=runtime_worker, name="live-gui-integration-worker", daemon=True)
        worker_thread.start()

        if not wait_until(
            lambda: refresh_and_check(panel, lambda: driver.label_text("status_label:connection_state") == "接続済み"),
            timeout=args.connect_timeout,
        ):
            snapshot("connection_timeout")
            raise RuntimeError(f"GUI did not reach connected state within {args.connect_timeout:.0f} seconds.")

        snapshot("connected")
        driver.click("button:start_agent")
        snapshot("started")

        driver.set_entry("scan_form:yaw_scan_min_deg", "-60")
        driver.set_entry("scan_form:yaw_scan_max_deg", "60")
        driver.set_entry("scan_form:yaw_scan_step_deg", "15")
        driver.set_entry("scan_form:pitch_scan_min_deg", "-30")
        driver.set_entry("scan_form:pitch_scan_max_deg", "0")
        driver.set_entry("scan_form:pitch_scan_step_deg", "10")
        driver.click("button:scan_submit")
        snapshot("scan_saved")

        driver.set_entry("scan_form:yaw_scan_step_deg", "0")
        driver.click("button:scan_submit")
        snapshot("scan_invalid")
        driver.click("button:scan_reload")

        driver.set_combobox("command_form:action_type", "待機")
        driver.set_entry("command_form:timeout_seconds", "5")
        driver.set_entry("command_form:retry_max_attempts", "1")
        driver.click("button:command_submit")
        snapshot("queued_wait")

        completed = wait_until(
            lambda: refresh_and_check(panel, lambda: any(row[1][1] == "完了" for row in driver.tree_rows("tree:commands"))),
            timeout=20,
        )
        snapshot("wait_completed" if completed else "wait_timeout")
        if not completed:
            raise RuntimeError("GUI wait command did not reach completed state.")

        driver.click("button:stop_agent")
        snapshot("stopped")

        store.save(
            controller.command_queue,
            controller.base_registry,
            navigation_config=controller.get_navigation_config(),
            last_interrupt_reason=controller.last_interrupt_reason,
        )
    except Exception as exc:
        error_message = f"{type(exc).__name__}:{exc}"
        raise
    finally:
        stop_event.set()
        controller.stop_agent()
        controller.remove_state_change_listener(on_controller_changed)
        if worker_thread is not None and worker_thread.is_alive():
            worker_thread.join(timeout=2.0)
        if root.winfo_exists():
            root.destroy()
        if game_process is not None:
            game_process.terminate()
            try:
                game_process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                game_process.kill()
        report = {
            "executed_at": time.strftime("%Y-%m-%d %H:%M:%S"),
            "error": error_message,
            "events": events,
        }
        report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"Live GUI integration report written to: {report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
