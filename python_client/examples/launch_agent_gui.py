from __future__ import annotations

import sys
import threading
import time
import tkinter as tk
from pathlib import Path

# Resolve the project-local src directory explicitly so the launcher can be
# started from a batch file without relying on the caller's PYTHONPATH.
ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
if str(SRC) not in sys.path:
    sys.path.insert(0, str(SRC))

from sevendays_bridge import SevenDaysBridgeClient
from sevendays_bridge.gui import AgentControlPanel
from sevendays_bridge.runtime import AgentController, AgentGuiRuntimeAdapter, SessionStore


def main() -> int:
    client = SevenDaysBridgeClient()
    store = SessionStore(ROOT / "data" / "agent_gui_session.json")

    controller = AgentController(
        client,
        state_changed_callback=lambda: store.save(
            controller.command_queue,
            controller.base_registry,
            navigation_config=controller.get_navigation_config(),
            last_interrupt_reason=controller.last_interrupt_reason,
        ),
    )
    adapter = AgentGuiRuntimeAdapter(controller)
    loaded = store.load_into(controller.command_queue, controller.base_registry)
    controller.apply_navigation_config(loaded.get("navigation_config", controller.get_navigation_config()))
    controller.last_interrupt_reason = loaded.get("last_interrupt_reason", "")
    if loaded["commands_loaded"] or loaded["bases_loaded"]:
        controller.logs.append(
            f"session_loaded:commands={loaded['commands_loaded']}:bases={loaded['bases_loaded']}"
        )

    root = tk.Tk()
    panel = AgentControlPanel(adapter.build_callbacks(), root=root)
    stop_event = threading.Event()
    worker_thread: threading.Thread | None = None

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
                        wait_seconds = 1.5
                    else:
                        controller.refresh_observation_cache()
                        wait_seconds = 2.0
                else:
                    controller.refresh_observation_cache()
                    wait_seconds = 2.5
            except Exception as exc:  # pragma: no cover - live GUI fallback
                controller.logs.append(f"runtime_tick_error:{type(exc).__name__}:{exc}")
                wait_seconds = 2.0
            stop_event.wait(wait_seconds)

    def refresh_gui() -> None:
        try:
            panel.refresh()
        except Exception as exc:  # pragma: no cover - live GUI fallback
            controller.logs.append(f"panel_refresh_error:{type(exc).__name__}:{exc}")
        if not stop_event.is_set():
            root.after(5000, refresh_gui)

    def shutdown() -> None:
        stop_event.set()
        controller.stop_agent()
        if worker_thread is not None and worker_thread.is_alive():
            worker_thread.join(timeout=2.0)
        store.save(
            controller.command_queue,
            controller.base_registry,
            navigation_config=controller.get_navigation_config(),
            last_interrupt_reason=controller.last_interrupt_reason,
        )
        controller.remove_state_change_listener(on_controller_changed)
        root.destroy()

    # Prime the UI once before the periodic runtime loop starts.
    try:
        controller.refresh_observation_cache()
        panel.refresh()
    except Exception as exc:  # pragma: no cover - live GUI fallback
        controller.logs.append(f"startup_refresh_error:{type(exc).__name__}:{exc}")
    worker_thread = threading.Thread(target=runtime_worker, name="agent-runtime-worker", daemon=True)
    worker_thread.start()
    root.after(5000, refresh_gui)
    root.protocol("WM_DELETE_WINDOW", shutdown)
    panel.run()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
