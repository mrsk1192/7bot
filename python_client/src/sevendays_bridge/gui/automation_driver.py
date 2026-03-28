from __future__ import annotations

import tkinter as tk
from tkinter import ttk

from .agent_control_panel import AgentControlPanel


class AgentGuiAutomationDriver:
    """Widget-level GUI driver for tests.

    The driver intentionally works through Tk widgets rather than controller or
    adapter methods so E2E tests can exercise real GUI event paths.
    """

    def __init__(self, panel: AgentControlPanel) -> None:
        self.panel = panel

    def click(self, key: str) -> None:
        widget = self.panel.widget(key)
        if hasattr(widget, "invoke"):
            widget.invoke()
        self.flush()

    def set_entry(self, key: str, value: str) -> None:
        widget = self.panel.widget(key)
        if not isinstance(widget, ttk.Entry):
            raise TypeError(f"{key} is not an Entry widget.")
        widget.delete(0, tk.END)
        widget.insert(0, value)
        self.flush()

    def set_combobox(self, key: str, value: str) -> None:
        widget = self.panel.widget(key)
        if not isinstance(widget, ttk.Combobox):
            raise TypeError(f"{key} is not a Combobox widget.")
        widget.set(value)
        widget.event_generate("<<ComboboxSelected>>")
        self.flush()

    def set_checkbutton(self, key: str, value: bool) -> None:
        widget = self.panel.widget(key)
        current = bool(int(widget.instate(("selected",))))
        if current != value and hasattr(widget, "invoke"):
            widget.invoke()
        self.flush()

    def select_tree_item(self, key: str, item_id: str) -> None:
        widget = self.panel.widget(key)
        if not isinstance(widget, ttk.Treeview):
            raise TypeError(f"{key} is not a Treeview widget.")
        widget.selection_set((item_id,))
        widget.focus(item_id)
        widget.event_generate("<<TreeviewSelect>>")
        self.flush()

    def clear_tree_selection(self, key: str) -> None:
        widget = self.panel.widget(key)
        if not isinstance(widget, ttk.Treeview):
            raise TypeError(f"{key} is not a Treeview widget.")
        widget.selection_remove(widget.selection())
        widget.focus("")
        widget.event_generate("<<TreeviewSelect>>")
        self.flush()

    def label_text(self, key: str) -> str:
        widget = self.panel.widget(key)
        return str(widget.cget("text"))

    def text_contents(self, key: str) -> str:
        widget = self.panel.widget(key)
        if not isinstance(widget, tk.Text):
            raise TypeError(f"{key} is not a Text widget.")
        return widget.get("1.0", tk.END).strip()

    def tree_rows(self, key: str) -> list[tuple[str, tuple[object, ...]]]:
        widget = self.panel.widget(key)
        if not isinstance(widget, ttk.Treeview):
            raise TypeError(f"{key} is not a Treeview widget.")
        rows: list[tuple[str, tuple[object, ...]]] = []
        for item_id in widget.get_children():
            rows.append((str(item_id), tuple(widget.item(item_id, "values"))))
        return rows

    def flush(self) -> None:
        self.panel.root.update_idletasks()
        self.panel.root.update()
