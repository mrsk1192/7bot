from __future__ import annotations

import tkinter as tk
from dataclasses import dataclass
from tkinter import ttk
from typing import Callable, Optional

from .gui_models import (
    AgentStatusViewModel,
    BaseFormData,
    CommandFormData,
    PanelSnapshot,
    ScanSettingsFormData,
)


@dataclass(frozen=True)
class AgentControlCallbacks:
    refresh_status: Callable[[], AgentStatusViewModel]
    start_agent: Callable[[], None]
    stop_agent: Callable[[], None]
    submit_command_form: Callable[[CommandFormData, Optional[str]], None]
    delete_selected_command: Callable[[Optional[str]], None]
    move_selected_up: Callable[[Optional[str]], None]
    move_selected_down: Callable[[Optional[str]], None]
    pause_selected_command: Callable[[Optional[str]], None]
    resume_selected_command: Callable[[Optional[str]], None]
    cancel_selected_command: Callable[[Optional[str]], None]
    interrupt_selected_command: Callable[[Optional[str]], None]
    submit_base_form: Callable[[BaseFormData, Optional[str]], None]
    start_build_for_selected_base: Callable[[Optional[str]], None]
    read_scan_settings: Callable[[], ScanSettingsFormData]
    submit_scan_settings_form: Callable[[ScanSettingsFormData], None]


class ToolTip:
    def __init__(self, widget, text: str):
        self.widget = widget
        self.text = text
        self.tip_window = None
        self.widget.bind("<Enter>", self._show, add="+")
        self.widget.bind("<Leave>", self._hide, add="+")

    def _show(self, _event=None) -> None:
        if self.tip_window is not None or not self.text:
            return
        x = self.widget.winfo_rootx() + 18
        y = self.widget.winfo_rooty() + self.widget.winfo_height() + 6
        self.tip_window = tk.Toplevel(self.widget)
        self.tip_window.wm_overrideredirect(True)
        self.tip_window.wm_geometry(f"+{x}+{y}")
        label = tk.Label(
            self.tip_window,
            text=self.text,
            justify="left",
            background="#fff9d9",
            relief="solid",
            borderwidth=1,
            padx=8,
            pady=6,
            wraplength=380,
        )
        label.pack()

    def _hide(self, _event=None) -> None:
        if self.tip_window is not None:
            self.tip_window.destroy()
            self.tip_window = None


class AgentControlPanel:
    """Automation-friendly Japanese GUI for command, base, scan, and runtime control."""

    STATUS_FIELD_LABELS = {
        "agent_state": "エージェント",
        "connection_state": "接続状態",
        "current_action": "現在行動",
        "current_target": "現在目標",
        "interrupt_reason": "割り込み理由",
        "health": "体力",
        "water": "水分",
        "hunger": "空腹",
        "stamina": "スタミナ",
        "debuffs": "デバフ",
        "carried_weight": "所持重量",
        "equipment_state": "装備状態",
    }

    ACTION_LABELS = {
        "move_to_position": "指定地点へ移動",
        "search_resource": "資源探索",
        "gather_resource": "資源採集",
        "move_avoiding_hostiles": "敵回避付き移動",
        "return_to_base": "拠点へ帰還",
        "store_items": "アイテム収納",
        "collect_items": "アイテム回収",
        "craft": "クラフト",
        "build": "建築",
        "repair": "修理",
        "patrol": "巡回",
        "wait": "待機",
        "follow": "追従",
        "scan_area": "周辺スキャン",
        "investigate_area": "指定エリア調査",
        "idle": "待機中",
        "none": "なし",
    }

    STATUS_LABELS = {
        "queued": "待機中",
        "running": "実行中",
        "paused": "一時停止",
        "interrupted": "割り込み済み",
        "completed": "完了",
        "failed": "失敗",
        "cancelled": "キャンセル済み",
        "none": "なし",
    }

    INTERRUPT_POLICY_LABELS = {
        "interruptible": "中断可",
        "non_interruptible": "中断不可",
        "interruptible_only_when_critical": "危険時のみ中断可",
    }

    FIELD_TOOLTIPS = {
        "yaw_scan_min_deg": "yaw はこの最小角度から最大角度まで片方向に走査されます。-90 なら左 90 度側から探索を始めます。",
        "yaw_scan_max_deg": "yaw 走査の終端角度です。途中で順序は並び替えられません。",
        "yaw_scan_step_deg": "yaw の刻み幅です。小さくすると見落としは減りますが、視点変更と query が増えて負荷は上がります。",
        "pitch_scan_min_deg": "pitch は常に 0 度から下方向へ走査され、この最小角度まで確認します。",
        "pitch_scan_max_deg": "設定として保持しますが、既定挙動では上方向には走査せず 0 度を上限に使います。",
        "pitch_scan_step_deg": "pitch の刻み幅です。15 なら 0, -15, -30, -45 の順に視点を下げます。",
        "scan_settle_delay_ms": "視点変更後に query を打つまでの待機時間です。短すぎると未収束のまま観測します。",
        "near_ground_priority_enabled": "有効にすると足元付近で見つけた loot / resource / interactable を優先候補にします。",
        "near_ground_distance_threshold": "足元優先として扱う近距離のしきい値です。",
    }

    def __init__(self, callbacks: AgentControlCallbacks, root: Optional[tk.Tk] = None) -> None:
        self.callbacks = callbacks
        self.root = root or tk.Tk()
        self.root.title("7DTD AI エージェント操作パネル")
        self._tooltips: list[ToolTip] = []
        self._status_labels: dict[str, ttk.Label] = {}
        self._command_vars: dict[str, tk.Variable] = {}
        self._base_vars: dict[str, tk.Variable] = {}
        self._scan_vars: dict[str, tk.Variable] = {}
        self._scan_error_var = tk.StringVar(value="")
        self._command_tree: ttk.Treeview | None = None
        self._base_tree: ttk.Treeview | None = None
        self._log_text: tk.Text | None = None
        self._build_layout()
        self.load_scan_settings_form()

    def _build_layout(self) -> None:
        container = ttk.Frame(self.root, padding=8)
        container.grid(sticky="nsew")
        self.root.columnconfigure(0, weight=1)
        self.root.rowconfigure(0, weight=1)
        container.columnconfigure(0, weight=3)
        container.columnconfigure(1, weight=2)
        container.rowconfigure(1, weight=1)
        container.rowconfigure(3, weight=1)

        self._build_status_frame(container)
        self._build_command_frame(container)
        self._build_base_frame(container)
        self._build_log_frame(container)

        side_panel = ttk.Frame(container)
        side_panel.grid(row=0, column=1, rowspan=4, sticky="nsew", padx=(8, 0))
        side_panel.columnconfigure(0, weight=1)
        self._build_command_form(side_panel, row=0)
        self._build_base_form(side_panel, row=1)
        self._build_scan_settings_form(side_panel, row=2)

    def _build_status_frame(self, parent) -> None:
        frame = ttk.LabelFrame(parent, text="エージェント状態", padding=8)
        frame.grid(row=0, column=0, sticky="nsew")
        frame.columnconfigure(1, weight=1)
        row = 0
        for key, label_text in self.STATUS_FIELD_LABELS.items():
            ttk.Label(frame, text=label_text).grid(row=row, column=0, sticky="w")
            value = ttk.Label(frame, text="-")
            value.grid(row=row, column=1, sticky="w")
            self._status_labels[key] = value
            row += 1
        ttk.Button(frame, text="エージェント開始", command=self.start_agent).grid(row=row, column=0, sticky="ew", pady=(6, 0))
        ttk.Button(frame, text="エージェント停止", command=self.stop_agent).grid(row=row, column=1, sticky="ew", pady=(6, 0))

    def _build_command_frame(self, parent) -> None:
        frame = ttk.LabelFrame(parent, text="コマンドキュー", padding=8)
        frame.grid(row=1, column=0, sticky="nsew", pady=(8, 0))
        frame.columnconfigure(0, weight=1)
        frame.rowconfigure(0, weight=1)

        self._command_tree = ttk.Treeview(
            frame,
            columns=("action", "status", "priority", "summary"),
            show="headings",
            height=8,
        )
        self._command_tree.heading("action", text="行動")
        self._command_tree.heading("status", text="状態")
        self._command_tree.heading("priority", text="優先度")
        self._command_tree.heading("summary", text="概要")
        self._command_tree.column("action", width=120, stretch=True)
        self._command_tree.column("status", width=100, stretch=True)
        self._command_tree.column("priority", width=80, stretch=False)
        self._command_tree.column("summary", width=240, stretch=True)
        self._command_tree.grid(row=0, column=0, columnspan=5, sticky="nsew")

        buttons = [
            ("追加 / 更新", self.submit_command_form),
            ("削除", lambda: self.callbacks.delete_selected_command(self.selected_command_id())),
            ("上へ", lambda: self.callbacks.move_selected_up(self.selected_command_id())),
            ("下へ", lambda: self.callbacks.move_selected_down(self.selected_command_id())),
            ("強制中断", lambda: self.callbacks.interrupt_selected_command(self.selected_command_id())),
            ("一時停止", lambda: self.callbacks.pause_selected_command(self.selected_command_id())),
            ("再開", lambda: self.callbacks.resume_selected_command(self.selected_command_id())),
            ("キャンセル", lambda: self.callbacks.cancel_selected_command(self.selected_command_id())),
            ("選択解除", self.clear_command_selection),
        ]
        for index, (label, command) in enumerate(buttons):
            ttk.Button(frame, text=label, command=command).grid(
                row=1 + (index // 5),
                column=index % 5,
                sticky="ew",
                pady=(6 if index < 5 else 2, 0),
            )

    def _build_base_frame(self, parent) -> None:
        frame = ttk.LabelFrame(parent, text="拠点", padding=8)
        frame.grid(row=2, column=0, sticky="nsew", pady=(8, 0))
        frame.columnconfigure(0, weight=1)
        frame.rowconfigure(0, weight=1)

        self._base_tree = ttk.Treeview(frame, columns=("name", "plan"), show="headings", height=4)
        self._base_tree.heading("name", text="拠点名")
        self._base_tree.heading("plan", text="建設計画")
        self._base_tree.column("name", width=180, stretch=True)
        self._base_tree.column("plan", width=140, stretch=True)
        self._base_tree.grid(row=0, column=0, columnspan=3, sticky="nsew")

        buttons = [
            ("拠点追加 / 更新", self.submit_base_form),
            ("建設開始", lambda: self.callbacks.start_build_for_selected_base(self.selected_base_id())),
            ("選択解除", self.clear_base_selection),
        ]
        for index, (label, command) in enumerate(buttons):
            ttk.Button(frame, text=label, command=command).grid(row=1, column=index, sticky="ew", pady=(6, 0))

    def _build_log_frame(self, parent) -> None:
        frame = ttk.LabelFrame(parent, text="ログ", padding=8)
        frame.grid(row=3, column=0, sticky="nsew", pady=(8, 0))
        frame.rowconfigure(0, weight=1)
        frame.columnconfigure(0, weight=1)
        self._log_text = tk.Text(frame, height=10, width=100)
        self._log_text.grid(row=0, column=0, sticky="nsew")

    def _build_command_form(self, parent, row: int) -> None:
        frame = ttk.LabelFrame(parent, text="コマンド入力", padding=8)
        frame.grid(row=row, column=0, sticky="nsew")
        frame.columnconfigure(1, weight=1)
        specs = [
            ("action_type", "行動種別", "search_resource"),
            ("target_x", "目標 X", ""),
            ("target_y", "目標 Y", ""),
            ("target_z", "目標 Z", ""),
            ("target_resource_type", "資源種別", ""),
            ("target_entity", "対象 Entity", ""),
            ("target_area", "対象エリア", ""),
            ("base_id", "拠点 ID", ""),
            ("priority", "優先度", "50"),
            ("interruptible", "中断ポリシー", "interruptible"),
            ("timeout_seconds", "タイムアウト(秒)", "60"),
            ("retry_max_attempts", "再試行回数", "2"),
            ("metadata_json", "metadata JSON", "{}"),
        ]
        action_values = [self.ACTION_LABELS[key] for key in self.ACTION_LABELS if key not in {"idle", "none"}]
        interrupt_values = [self.INTERRUPT_POLICY_LABELS[key] for key in self.INTERRUPT_POLICY_LABELS]
        for index, (key, label, default) in enumerate(specs):
            ttk.Label(frame, text=label).grid(row=index, column=0, sticky="w")
            display_default = default
            if key == "action_type":
                display_default = self._internal_to_display_action(default)
            elif key == "interruptible":
                display_default = self._internal_to_display_interrupt(default)
            var = tk.StringVar(value=display_default)
            self._command_vars[key] = var
            if key == "action_type":
                widget = ttk.Combobox(frame, textvariable=var, values=action_values, state="readonly")
            elif key == "interruptible":
                widget = ttk.Combobox(frame, textvariable=var, values=interrupt_values, state="readonly")
            else:
                widget = ttk.Entry(frame, textvariable=var)
            widget.grid(row=index, column=1, sticky="ew")

    def _build_base_form(self, parent, row: int) -> None:
        frame = ttk.LabelFrame(parent, text="拠点入力", padding=8)
        frame.grid(row=row, column=0, sticky="nsew", pady=(8, 0))
        frame.columnconfigure(1, weight=1)
        specs = [
            ("base_id", "拠点 ID", ""),
            ("base_name", "拠点名", ""),
            ("anchor_x", "中心 X", ""),
            ("anchor_y", "中心 Y", ""),
            ("anchor_z", "中心 Z", ""),
            ("min_x", "範囲 Min X", ""),
            ("min_y", "範囲 Min Y", ""),
            ("min_z", "範囲 Min Z", ""),
            ("max_x", "範囲 Max X", ""),
            ("max_y", "範囲 Max Y", ""),
            ("max_z", "範囲 Max Z", ""),
            ("safety_score", "安全度", "0.5"),
            ("build_plan_id", "建設計画 ID", ""),
        ]
        for index, (key, label, default) in enumerate(specs):
            ttk.Label(frame, text=label).grid(row=index, column=0, sticky="w")
            var = tk.StringVar(value=default)
            self._base_vars[key] = var
            ttk.Entry(frame, textvariable=var).grid(row=index, column=1, sticky="ew")

    def _build_scan_settings_form(self, parent, row: int) -> None:
        frame = ttk.LabelFrame(parent, text="スキャン設定", padding=8)
        frame.grid(row=row, column=0, sticky="nsew", pady=(8, 0))
        frame.columnconfigure(1, weight=1)

        description = ttk.Label(
            frame,
            text=(
                "yaw は min から max へ片方向に走査されます。"
                " pitch は 0 度から下方向へ走査されます。"
                " 細かくしすぎると探索精度は上がりますが、処理回数も増えます。"
            ),
            wraplength=360,
            justify="left",
        )
        description.grid(row=0, column=0, columnspan=2, sticky="w", pady=(0, 6))

        specs = [
            ("yaw_scan_min_deg", "Yaw scan min (deg)", "-90"),
            ("yaw_scan_max_deg", "Yaw scan max (deg)", "90"),
            ("yaw_scan_step_deg", "Yaw scan step (deg)", "5"),
            ("pitch_scan_min_deg", "Pitch scan min (deg)", "-45"),
            ("pitch_scan_max_deg", "Pitch scan max (deg)", "0"),
            ("pitch_scan_step_deg", "Pitch scan step (deg)", "15"),
            ("scan_settle_delay_ms", "Scan settle delay (ms)", "60"),
            ("near_ground_distance_threshold", "Near-ground distance threshold", "5.0"),
        ]
        current_row = 1
        for key, label, default in specs:
            label_widget = ttk.Label(frame, text=label)
            label_widget.grid(row=current_row, column=0, sticky="w")
            self._attach_tooltip(label_widget, self.FIELD_TOOLTIPS.get(key, ""))
            var = tk.StringVar(value=default)
            self._scan_vars[key] = var
            entry = ttk.Entry(frame, textvariable=var)
            entry.grid(row=current_row, column=1, sticky="ew")
            self._attach_tooltip(entry, self.FIELD_TOOLTIPS.get(key, ""))
            current_row += 1

        bool_label = ttk.Label(frame, text="Near-ground priority enabled")
        bool_label.grid(row=current_row, column=0, sticky="w")
        self._attach_tooltip(bool_label, self.FIELD_TOOLTIPS["near_ground_priority_enabled"])
        bool_var = tk.BooleanVar(value=True)
        self._scan_vars["near_ground_priority_enabled"] = bool_var
        check = ttk.Checkbutton(frame, variable=bool_var)
        check.grid(row=current_row, column=1, sticky="w")
        self._attach_tooltip(check, self.FIELD_TOOLTIPS["near_ground_priority_enabled"])
        current_row += 1

        ttk.Button(frame, text="設定保存", command=self.submit_scan_settings_form).grid(row=current_row, column=0, sticky="ew", pady=(6, 0))
        ttk.Button(frame, text="既定値を再読込", command=self.load_scan_settings_form).grid(row=current_row, column=1, sticky="ew", pady=(6, 0))
        current_row += 1
        ttk.Label(frame, textvariable=self._scan_error_var, foreground="#b00020", wraplength=360, justify="left").grid(
            row=current_row,
            column=0,
            columnspan=2,
            sticky="w",
            pady=(6, 0),
        )

    def selected_command_id(self) -> Optional[str]:
        selection = self._command_tree.selection() if self._command_tree is not None else ()
        return None if not selection else str(selection[0])

    def selected_base_id(self) -> Optional[str]:
        selection = self._base_tree.selection() if self._base_tree is not None else ()
        return None if not selection else str(selection[0])

    def collect_command_form(self) -> CommandFormData:
        values = {key: var.get() for key, var in self._command_vars.items()}
        values["action_type"] = self._display_to_internal_action(values["action_type"])
        values["interruptible"] = self._display_to_internal_interrupt(values["interruptible"])
        return CommandFormData(**values)

    def collect_base_form(self) -> BaseFormData:
        return BaseFormData(**{key: var.get() for key, var in self._base_vars.items()})

    def collect_scan_settings_form(self) -> ScanSettingsFormData:
        values = {}
        for key, var in self._scan_vars.items():
            values[key] = bool(var.get()) if isinstance(var, tk.BooleanVar) else var.get()
        return ScanSettingsFormData(**values)

    def set_command_form_values(self, **values) -> None:
        for key, value in values.items():
            if key in self._command_vars:
                if key == "action_type":
                    value = self._internal_to_display_action("" if value is None else str(value))
                elif key == "interruptible":
                    value = self._internal_to_display_interrupt("" if value is None else str(value))
                self._command_vars[key].set("" if value is None else str(value))
        self.root.update_idletasks()

    def set_base_form_values(self, **values) -> None:
        for key, value in values.items():
            if key in self._base_vars:
                self._base_vars[key].set("" if value is None else str(value))
        self.root.update_idletasks()

    def set_scan_settings_form_values(self, **values) -> None:
        for key, value in values.items():
            if key in self._scan_vars:
                if isinstance(self._scan_vars[key], tk.BooleanVar):
                    self._scan_vars[key].set(bool(value))
                else:
                    self._scan_vars[key].set("" if value is None else str(value))
        self.root.update_idletasks()

    def load_scan_settings_form(self) -> None:
        settings = self.callbacks.read_scan_settings()
        self.set_scan_settings_form_values(
            yaw_scan_min_deg=settings.yaw_scan_min_deg,
            yaw_scan_max_deg=settings.yaw_scan_max_deg,
            yaw_scan_step_deg=settings.yaw_scan_step_deg,
            pitch_scan_min_deg=settings.pitch_scan_min_deg,
            pitch_scan_max_deg=settings.pitch_scan_max_deg,
            pitch_scan_step_deg=settings.pitch_scan_step_deg,
            scan_settle_delay_ms=settings.scan_settle_delay_ms,
            near_ground_priority_enabled=settings.near_ground_priority_enabled,
            near_ground_distance_threshold=settings.near_ground_distance_threshold,
        )
        self._scan_error_var.set("")

    def submit_command_form(self) -> None:
        self.callbacks.submit_command_form(self.collect_command_form(), self.selected_command_id())
        self.refresh()

    def submit_base_form(self) -> None:
        self.callbacks.submit_base_form(self.collect_base_form(), self.selected_base_id())
        self.refresh()

    def submit_scan_settings_form(self) -> None:
        try:
            self.callbacks.submit_scan_settings_form(self.collect_scan_settings_form())
        except ValueError as exc:
            self._scan_error_var.set(str(exc))
            self.root.update_idletasks()
            return
        self._scan_error_var.set("")
        self.load_scan_settings_form()
        self.refresh()

    def start_agent(self) -> None:
        self.callbacks.start_agent()
        self.refresh()

    def stop_agent(self) -> None:
        self.callbacks.stop_agent()
        self.refresh()

    def select_command(self, command_id: str) -> None:
        if self._command_tree is not None and command_id in self._command_tree.get_children():
            self._command_tree.selection_set((command_id,))
            self._command_tree.focus(command_id)
        self.root.update_idletasks()

    def clear_command_selection(self) -> None:
        if self._command_tree is not None:
            self._command_tree.selection_remove(self._command_tree.selection())
            self._command_tree.focus("")
        self.root.update_idletasks()

    def select_base(self, base_id: str) -> None:
        if self._base_tree is not None and base_id in self._base_tree.get_children():
            self._base_tree.selection_set((base_id,))
            self._base_tree.focus(base_id)
        self.root.update_idletasks()

    def clear_base_selection(self) -> None:
        if self._base_tree is not None:
            self._base_tree.selection_remove(self._base_tree.selection())
            self._base_tree.focus("")
        self.root.update_idletasks()

    def invoke_command_action(self, action_name: str) -> None:
        selected = self.selected_command_id()
        mapping = {
            "delete": lambda: self.callbacks.delete_selected_command(selected),
            "up": lambda: self.callbacks.move_selected_up(selected),
            "down": lambda: self.callbacks.move_selected_down(selected),
            "pause": lambda: self.callbacks.pause_selected_command(selected),
            "resume": lambda: self.callbacks.resume_selected_command(selected),
            "cancel": lambda: self.callbacks.cancel_selected_command(selected),
            "interrupt": lambda: self.callbacks.interrupt_selected_command(selected),
        }
        mapping[action_name]()
        self.refresh()

    def invoke_base_action(self, action_name: str) -> None:
        selected = self.selected_base_id()
        mapping = {
            "start_build": lambda: self.callbacks.start_build_for_selected_base(selected),
            "submit": self.submit_base_form,
        }
        mapping[action_name]()
        self.refresh()

    def invoke_runtime_action(self, action_name: str) -> None:
        mapping = {
            "start": self.start_agent,
            "stop": self.stop_agent,
        }
        mapping[action_name]()

    def refresh(self) -> None:
        status = self.callbacks.refresh_status()
        selected_command_id = self.selected_command_id()
        selected_base_id = self.selected_base_id()

        for key, label in self._status_labels.items():
            label.configure(text=self._translate_status_field_value(key, getattr(status, key)))

        if self._command_tree is not None:
            for item in self._command_tree.get_children():
                self._command_tree.delete(item)
            for command in status.command_queue:
                self._command_tree.insert(
                    "",
                    "end",
                    iid=command.command_id,
                    values=(
                        self._internal_to_display_action(command.action_type),
                        self._translate_status(command.status),
                        command.priority,
                        command.summary,
                    ),
                )
            if selected_command_id:
                self.select_command(selected_command_id)

        if self._base_tree is not None:
            for item in self._base_tree.get_children():
                self._base_tree.delete(item)
            for base in status.bases:
                self._base_tree.insert("", "end", iid=base.base_id, values=(base.base_name, base.build_plan_id))
            if selected_base_id:
                self.select_base(selected_base_id)

        if self._log_text is not None:
            self._log_text.delete("1.0", tk.END)
            self._log_text.insert(tk.END, "\n".join(status.logs))

        self.root.update_idletasks()

    def snapshot(self) -> PanelSnapshot:
        return PanelSnapshot(
            status=self.callbacks.refresh_status(),
            selected_command_id=self.selected_command_id() or "",
            selected_base_id=self.selected_base_id() or "",
            command_form=self.collect_command_form(),
            base_form=self.collect_base_form(),
            scan_settings_form=self.collect_scan_settings_form(),
        )

    def run(self) -> None:
        self.refresh()
        self.root.mainloop()

    def _attach_tooltip(self, widget, text: str) -> None:
        if text:
            self._tooltips.append(ToolTip(widget, text))

    @classmethod
    def _internal_to_display_action(cls, value: str) -> str:
        return cls.ACTION_LABELS.get(value, value)

    @classmethod
    def _display_to_internal_action(cls, value: str) -> str:
        reverse = {display: internal for internal, display in cls.ACTION_LABELS.items()}
        return reverse.get(value, value)

    @classmethod
    def _internal_to_display_interrupt(cls, value: str) -> str:
        return cls.INTERRUPT_POLICY_LABELS.get(value, value)

    @classmethod
    def _display_to_internal_interrupt(cls, value: str) -> str:
        reverse = {display: internal for internal, display in cls.INTERRUPT_POLICY_LABELS.items()}
        return reverse.get(value, value)

    @classmethod
    def _translate_status(cls, value: str) -> str:
        return cls.STATUS_LABELS.get(value, value)

    @classmethod
    def _translate_status_field_value(cls, field_name: str, value: str) -> str:
        if field_name == "current_action":
            return cls._internal_to_display_action(value)
        if field_name == "agent_state":
            if value == "running":
                return "実行中"
            if value == "stopped":
                return "停止中"
        if field_name == "connection_state":
            if value == "connected":
                return "接続中"
            if value == "disconnected":
                return "未接続"
        if field_name == "interrupt_reason" and value == "none":
            return "なし"
        if field_name == "equipment_state":
            if value == "ok":
                return "正常"
            if value == "missing_or_broken":
                return "不足または破損"
        return value
