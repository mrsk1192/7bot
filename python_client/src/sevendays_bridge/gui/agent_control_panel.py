from __future__ import annotations

import tkinter as tk
from dataclasses import dataclass
from tkinter import ttk
from typing import Callable, Optional

from .gui_models import AgentStatusViewModel, BaseFormData, CommandFormData, PanelSnapshot, ScanSettingsFormData


@dataclass(frozen=True)
class AgentControlCallbacks:
    refresh_status: Callable[[], AgentStatusViewModel]
    start_agent: Callable[[], None]
    stop_agent: Callable[[], None]
    reset_agent: Callable[[], None]
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
    def __init__(self, widget: tk.Widget, text: str) -> None:
        self.widget = widget
        self.text = text
        self.tip_window: tk.Toplevel | None = None
        widget.bind('<Enter>', self._show, add='+')
        widget.bind('<Leave>', self._hide, add='+')

    def _show(self, _event=None) -> None:
        if self.tip_window is not None or not self.text:
            return
        x = self.widget.winfo_rootx() + 18
        y = self.widget.winfo_rooty() + self.widget.winfo_height() + 6
        self.tip_window = tk.Toplevel(self.widget)
        self.tip_window.wm_overrideredirect(True)
        self.tip_window.wm_geometry(f'+{x}+{y}')
        label = tk.Label(
            self.tip_window,
            text=self.text,
            justify='left',
            background='#fff9d9',
            relief='solid',
            borderwidth=1,
            padx=8,
            pady=6,
            wraplength=420,
        )
        label.pack()

    def _hide(self, _event=None) -> None:
        if self.tip_window is not None:
            self.tip_window.destroy()
            self.tip_window = None


class AgentControlPanel:
    STATUS_FIELDS = [
        ('agent_state', 'エージェント状態'),
        ('connection_state', '接続状態'),
        ('current_action', '現在行動'),
        ('current_target', '現在目標'),
        ('interrupt_reason', '割り込み理由'),
        ('health', '体力'),
        ('water', '水分'),
        ('hunger', '空腹'),
        ('stamina', 'スタミナ'),
        ('debuffs', 'デバフ'),
        ('carried_weight', '所持重量'),
        ('equipment_state', '装備状態'),
        ('player_position', '現在位置'),
        ('biome', 'バイオーム'),
        ('look_target', '視線先'),
        ('nearby_resource_count', '周辺資源候補数'),
        ('nearby_interactable_count', '周辺操作候補数'),
        ('nearby_entity_count', '周辺 Entity 数'),
        ('last_error', '最新エラー'),
    ]

    ACTION_LABELS = {
        'move_to_position': '指定地点へ移動',
        'search_resource': '資源探索',
        'gather_resource': '資源採集',
        'move_avoiding_hostiles': '敵回避付き移動',
        'return_to_base': '拠点へ帰還',
        'store_items': 'アイテム収納',
        'collect_items': 'アイテム回収',
        'craft': 'クラフト',
        'build': '建築',
        'repair': '修理',
        'patrol': '巡回',
        'wait': '待機',
        'follow': '追従',
        'scan_area': '周辺スキャン',
        'investigate_area': '指定エリア調査',
        'idle': '待機中',
        'none': 'なし',
    }

    INTERRUPT_POLICY_LABELS = {
        'interruptible': '中断可',
        'non_interruptible': '中断不可',
        'interruptible_only_when_critical': '危険時のみ中断可',
    }

    COMMAND_STATUS_LABELS = {
        'queued': '待機中',
        'running': '実行中',
        'paused': '一時停止',
        'interrupted': '割り込み済み',
        'completed': '完了',
        'failed': '失敗',
        'cancelled': 'キャンセル済み',
    }

    STATUS_VALUE_LABELS = {
        ('agent_state', 'running'): '実行中',
        ('agent_state', 'stopped'): '停止中',
        ('connection_state', 'connected'): '接続済み',
        ('connection_state', 'disconnected'): '未接続',
        ('equipment_state', 'ok'): '正常',
        ('equipment_state', 'missing_or_broken'): '不足または破損',
        ('interrupt_reason', 'none'): 'なし',
    }

    TOOLTIP_TEXT = {
        'agent_state': 'エージェントがコマンド処理を進めているかを表示します。停止中はキューがあっても実行されません。',
        'connection_state': 'GUI から見たブリッジ接続状態です。未接続でも GUI は落ちずに待機します。',
        'current_action': '現在処理中のコマンド種別です。探索、移動、建築、待機などが表示されます。',
        'current_target': 'いま向かっている目標です。座標、拠点 ID、資源種別などの確認に使います。',
        'interrupt_reason': '優先行動や手動中断で現在タスクが止められた理由です。',
        'health': 'プレイヤー体力です。危険時は優先行動が割り込みます。',
        'water': 'プレイヤーの水分値です。',
        'hunger': 'プレイヤーの空腹値です。',
        'stamina': 'プレイヤーの現在スタミナです。',
        'debuffs': '現在把握しているデバフ一覧です。',
        'carried_weight': '所持重量の比率です。収納や帰還判断の補助情報です。',
        'equipment_state': '必要装備の不足や破損があるかを示します。',
        'player_position': '現在位置です。GUI → エージェント → ブリッジ → 実機の観測経路が通っていれば更新されます。',
        'biome': '現在地のバイオームです。',
        'look_target': '現在の視線先です。loot / resource / interactable などの確認に使います。',
        'nearby_resource_count': '周辺資源候補数です。',
        'nearby_interactable_count': '周辺操作候補数です。',
        'nearby_entity_count': '周辺 Entity 数です。',
        'last_error': '最新エラーです。未接続や取得失敗を hidden にしないため表示します。',
        'start_agent': 'エージェントの実行ループを開始します。キュー済みコマンドが順次処理されます。',
        'stop_agent': 'エージェントを停止し、stop_all_input を通して押しっぱなし入力を残さないようにします。',
        'reset_agent': 'エージェントを停止し、キュー、建設進捗、割り込み理由を初期化します。',
        'command_submit': '右側フォームの内容でコマンドを追加または更新します。',
        'command_delete': '選択中コマンドを削除します。',
        'command_up': '選択中コマンドを上へ移動し、実行順を前に寄せます。',
        'command_down': '選択中コマンドを下へ移動し、実行順を後ろへ下げます。',
        'command_interrupt': '選択中コマンドを割り込み済みにし、再開候補として残します。',
        'command_pause': '選択中コマンドを一時停止にします。',
        'command_resume': '一時停止中コマンドを待機状態へ戻します。',
        'command_cancel': '選択中コマンドをキャンセル済みにします。',
        'command_clear_selection': 'コマンド一覧の選択を外します。新規追加に戻るときに使います。',
        'base_submit': '右側フォームの内容で拠点を追加または更新します。',
        'base_start_build': '選択中拠点に対する建設コマンドをキューへ追加します。',
        'base_clear_selection': '拠点一覧の選択を外します。',
        'yaw_scan_min_deg': 'yaw 走査の開始角度です。min から max へ片方向で走査されます。',
        'yaw_scan_max_deg': 'yaw 走査の終了角度です。min より大きく、-180〜180 の範囲で指定します。',
        'yaw_scan_step_deg': 'yaw の刻み角度です。細かいほど精度は上がりますが処理回数も増えます。',
        'pitch_scan_min_deg': 'pitch の最下角度です。実行時は 0 度から下方向へこの値まで刻みます。',
        'pitch_scan_max_deg': 'pitch 設定上の最大角度です。既定挙動では上方向は使わず 0 度基準で下向き走査します。',
        'pitch_scan_step_deg': 'pitch の刻み角度です。0, -step, -2*step の順で下向き確認が増えます。',
        'scan_settle_delay_ms': '視点変更後に query を打つまでの安定待機時間です。',
        'near_ground_priority_enabled': '足元近距離の loot / resource / interactable を優先候補として扱います。',
        'near_ground_distance_threshold': '足元優先を強める距離閾値です。',
        'scan_submit': 'スキャン設定を保存し、以後の sector scan に反映します。',
        'scan_reload': '保存済みスキャン設定を再読込してフォーム値へ戻します。',
    }

    def __init__(self, callbacks: AgentControlCallbacks, root: Optional[tk.Tk] = None) -> None:
        self.callbacks = callbacks
        self.root = root or tk.Tk()
        self.root.title('7DTD AI エージェント操作パネル')
        self._widgets: dict[str, tk.Widget] = {}
        self._status_labels: dict[str, ttk.Label] = {}
        self._tooltips: list[ToolTip] = []
        self._command_vars: dict[str, tk.Variable] = {}
        self._base_vars: dict[str, tk.Variable] = {}
        self._scan_vars: dict[str, tk.Variable] = {}
        self._scan_error_var = tk.StringVar(value='')
        self._command_tree: ttk.Treeview | None = None
        self._base_tree: ttk.Treeview | None = None
        self._log_text: tk.Text | None = None
        self._build_layout()
        self.load_scan_settings_form()

    def _build_layout(self) -> None:
        self.root.columnconfigure(0, weight=1)
        self.root.rowconfigure(0, weight=1)
        container = ttk.Frame(self.root, padding=10)
        container.grid(row=0, column=0, sticky='nsew')
        container.columnconfigure(0, weight=3)
        container.columnconfigure(1, weight=2)
        container.rowconfigure(1, weight=1)
        container.rowconfigure(3, weight=1)
        self._build_status_frame(container)
        self._build_command_frame(container)
        self._build_base_frame(container)
        self._build_log_frame(container)
        side = ttk.Frame(container)
        side.grid(row=0, column=1, rowspan=4, sticky='nsew', padx=(10, 0))
        side.columnconfigure(0, weight=1)
        self._build_command_form(side, row=0)
        self._build_base_form(side, row=1)
        self._build_scan_settings_form(side, row=2)

    def _build_status_frame(self, parent: ttk.Frame) -> None:
        frame = ttk.LabelFrame(parent, text='エージェント状態', padding=8)
        frame.grid(row=0, column=0, sticky='nsew')
        frame.columnconfigure(1, weight=1)
        for row, (key, label_text) in enumerate(self.STATUS_FIELDS):
            label = ttk.Label(frame, text=label_text)
            label.grid(row=row, column=0, sticky='w', padx=(0, 8), pady=1)
            self._attach_tooltip(label, self.TOOLTIP_TEXT.get(key, ''))
            value = ttk.Label(frame, text='-')
            value.grid(row=row, column=1, sticky='w', pady=1)
            self._status_labels[key] = value
            self._widgets[f'status_label:{key}'] = value
        row = len(self.STATUS_FIELDS)
        self._add_button(frame, row, 0, 'button:start_agent', 'エージェント開始', self._on_start_agent, 'start_agent')
        self._add_button(frame, row, 1, 'button:stop_agent', 'エージェント停止', self._on_stop_agent, 'stop_agent')
        self._add_button(frame, row + 1, 0, 'button:reset_agent', 'エージェントリセット', self._on_reset_agent, 'reset_agent', colspan=2)

    def _build_command_frame(self, parent: ttk.Frame) -> None:
        frame = ttk.LabelFrame(parent, text='コマンドキュー', padding=8)
        frame.grid(row=1, column=0, sticky='nsew', pady=(10, 0))
        frame.columnconfigure(0, weight=1)
        frame.rowconfigure(0, weight=1)
        tree = ttk.Treeview(frame, columns=('action', 'status', 'priority', 'summary'), show='headings', height=8)
        for name, text, width in (('action', '行動', 150), ('status', '状態', 110), ('priority', '優先度', 80), ('summary', '概要', 280)):
            tree.heading(name, text=text)
            tree.column(name, width=width, stretch=(name != 'priority'))
        tree.grid(row=0, column=0, columnspan=5, sticky='nsew')
        self._command_tree = tree
        self._widgets['tree:commands'] = tree
        buttons = [
            ('button:command_submit', '追加 / 更新', self._on_submit_command, 'command_submit'),
            ('button:command_delete', '削除', self._on_delete_command, 'command_delete'),
            ('button:command_up', '上へ', self._on_move_command_up, 'command_up'),
            ('button:command_down', '下へ', self._on_move_command_down, 'command_down'),
            ('button:command_interrupt', '強制中断', self._on_interrupt_command, 'command_interrupt'),
            ('button:command_pause', '一時停止', self._on_pause_command, 'command_pause'),
            ('button:command_resume', '再開', self._on_resume_command, 'command_resume'),
            ('button:command_cancel', 'キャンセル', self._on_cancel_command, 'command_cancel'),
            ('button:command_clear_selection', '選択解除', self.clear_command_selection, 'command_clear_selection'),
        ]
        for index, (key, label, command, tooltip_key) in enumerate(buttons):
            self._add_button(frame, 1 + (index // 5), index % 5, key, label, command, tooltip_key)

    def _build_base_frame(self, parent: ttk.Frame) -> None:
        frame = ttk.LabelFrame(parent, text='拠点', padding=8)
        frame.grid(row=2, column=0, sticky='nsew', pady=(10, 0))
        frame.columnconfigure(0, weight=1)
        frame.rowconfigure(0, weight=1)
        tree = ttk.Treeview(frame, columns=('name', 'plan'), show='headings', height=5)
        tree.heading('name', text='拠点名')
        tree.heading('plan', text='建設計画')
        tree.column('name', width=180, stretch=True)
        tree.column('plan', width=180, stretch=True)
        tree.grid(row=0, column=0, columnspan=3, sticky='nsew')
        self._base_tree = tree
        self._widgets['tree:bases'] = tree
        self._add_button(frame, 1, 0, 'button:base_submit', '拠点追加 / 更新', self._on_submit_base, 'base_submit')
        self._add_button(frame, 1, 1, 'button:base_start_build', '建設開始', self._on_start_build, 'base_start_build')
        self._add_button(frame, 1, 2, 'button:base_clear_selection', '選択解除', self.clear_base_selection, 'base_clear_selection')

    def _build_log_frame(self, parent: ttk.Frame) -> None:
        frame = ttk.LabelFrame(parent, text='ログ', padding=8)
        frame.grid(row=3, column=0, sticky='nsew', pady=(10, 0))
        frame.columnconfigure(0, weight=1)
        frame.rowconfigure(0, weight=1)
        text = tk.Text(frame, height=10, width=110)
        text.grid(row=0, column=0, sticky='nsew')
        self._log_text = text
        self._widgets['text:logs'] = text

    def _build_command_form(self, parent: ttk.Frame, row: int) -> None:
        frame = ttk.LabelFrame(parent, text='コマンド入力', padding=8)
        frame.grid(row=row, column=0, sticky='nsew')
        frame.columnconfigure(1, weight=1)
        specs = [
            ('action_type', '行動種別', 'search_resource'), ('target_x', '目標 X', ''), ('target_y', '目標 Y', ''),
            ('target_z', '目標 Z', ''), ('target_resource_type', '資源種別', ''), ('target_entity', '対象 Entity', ''),
            ('target_area', '対象エリア', ''), ('base_id', '拠点 ID', ''), ('priority', '優先度', '50'),
            ('interruptible', '中断ポリシー', 'interruptible'), ('timeout_seconds', 'タイムアウト(秒)', '60'),
            ('retry_max_attempts', '再試行回数', '2'), ('metadata_json', 'metadata JSON', '{}'),
        ]
        actions = [self._internal_to_display_action(k) for k in self.ACTION_LABELS if k not in {'idle', 'none'}]
        policies = [self._internal_to_display_interrupt(k) for k in self.INTERRUPT_POLICY_LABELS]
        for index, (key, text, default) in enumerate(specs):
            ttk.Label(frame, text=text).grid(row=index, column=0, sticky='w', padx=(0, 8), pady=1)
            display_default = self._internal_to_display_action(default) if key == 'action_type' else self._internal_to_display_interrupt(default) if key == 'interruptible' else default
            var = tk.StringVar(value=display_default)
            self._command_vars[key] = var
            widget = ttk.Combobox(frame, textvariable=var, values=actions if key == 'action_type' else policies, state='readonly') if key in {'action_type', 'interruptible'} else ttk.Entry(frame, textvariable=var)
            widget.grid(row=index, column=1, sticky='ew', pady=1)
            self._widgets[f'command_form:{key}'] = widget

    def _build_base_form(self, parent: ttk.Frame, row: int) -> None:
        frame = ttk.LabelFrame(parent, text='拠点入力', padding=8)
        frame.grid(row=row, column=0, sticky='nsew', pady=(10, 0))
        frame.columnconfigure(1, weight=1)
        specs = [
            ('base_id', '拠点 ID', ''), ('base_name', '拠点名', ''), ('anchor_x', '中心 X', ''), ('anchor_y', '中心 Y', ''),
            ('anchor_z', '中心 Z', ''), ('min_x', '範囲 Min X', ''), ('min_y', '範囲 Min Y', ''), ('min_z', '範囲 Min Z', ''),
            ('max_x', '範囲 Max X', ''), ('max_y', '範囲 Max Y', ''), ('max_z', '範囲 Max Z', ''), ('safety_score', '安全スコア', '0.5'),
            ('build_plan_id', '建設計画 ID', ''),
        ]
        for index, (key, text, default) in enumerate(specs):
            ttk.Label(frame, text=text).grid(row=index, column=0, sticky='w', padx=(0, 8), pady=1)
            var = tk.StringVar(value=default)
            self._base_vars[key] = var
            widget = ttk.Entry(frame, textvariable=var)
            widget.grid(row=index, column=1, sticky='ew', pady=1)
            self._widgets[f'base_form:{key}'] = widget

    def _build_scan_settings_form(self, parent: ttk.Frame, row: int) -> None:
        frame = ttk.LabelFrame(parent, text='スキャン設定', padding=8)
        frame.grid(row=row, column=0, sticky='nsew', pady=(10, 0))
        frame.columnconfigure(1, weight=1)
        desc = ttk.Label(frame, text='yaw は min から max へ片方向に走査され、pitch は 0 度から下方向へ走査されます。細かい刻みほど精度は上がりますが処理回数も増えます。', wraplength=380, justify='left')
        desc.grid(row=0, column=0, columnspan=2, sticky='w', pady=(0, 6))
        specs = [
            ('yaw_scan_min_deg', 'Yaw scan min (deg)', '-90'), ('yaw_scan_max_deg', 'Yaw scan max (deg)', '90'),
            ('yaw_scan_step_deg', 'Yaw scan step (deg)', '5'), ('pitch_scan_min_deg', 'Pitch scan min (deg)', '-45'),
            ('pitch_scan_max_deg', 'Pitch scan max (deg)', '0'), ('pitch_scan_step_deg', 'Pitch scan step (deg)', '15'),
            ('scan_settle_delay_ms', 'Scan settle delay (ms)', '60'), ('near_ground_distance_threshold', 'Near-ground distance threshold', '5.0'),
        ]
        current_row = 1
        for key, text, default in specs:
            label = ttk.Label(frame, text=text)
            label.grid(row=current_row, column=0, sticky='w', padx=(0, 8), pady=1)
            self._attach_tooltip(label, self.TOOLTIP_TEXT.get(key, ''))
            var = tk.StringVar(value=default)
            self._scan_vars[key] = var
            entry = ttk.Entry(frame, textvariable=var)
            entry.grid(row=current_row, column=1, sticky='ew', pady=1)
            self._attach_tooltip(entry, self.TOOLTIP_TEXT.get(key, ''))
            self._widgets[f'scan_form:{key}'] = entry
            current_row += 1
        label = ttk.Label(frame, text='Near-ground priority enabled')
        label.grid(row=current_row, column=0, sticky='w', padx=(0, 8), pady=1)
        self._attach_tooltip(label, self.TOOLTIP_TEXT['near_ground_priority_enabled'])
        flag = tk.BooleanVar(value=True)
        self._scan_vars['near_ground_priority_enabled'] = flag
        check = ttk.Checkbutton(frame, variable=flag)
        check.grid(row=current_row, column=1, sticky='w', pady=1)
        self._attach_tooltip(check, self.TOOLTIP_TEXT['near_ground_priority_enabled'])
        self._widgets['scan_form:near_ground_priority_enabled'] = check
        current_row += 1
        self._add_button(frame, current_row, 0, 'button:scan_submit', '設定を保存', self._on_submit_scan_settings, 'scan_submit')
        self._add_button(frame, current_row, 1, 'button:scan_reload', '読込 / 既定値反映', self.load_scan_settings_form, 'scan_reload')
        error_label = ttk.Label(frame, textvariable=self._scan_error_var, foreground='#b00020', wraplength=380, justify='left')
        error_label.grid(row=current_row + 1, column=0, columnspan=2, sticky='w', pady=(6, 0))
        self._widgets['label:scan_error'] = error_label

    def _add_button(self, parent, row: int, column: int, key: str, text: str, command, tooltip_key: str, colspan: int = 1) -> None:
        button = ttk.Button(parent, text=text, command=command)
        button.grid(row=row, column=column, columnspan=colspan, sticky='ew', pady=(6 if row > 0 else 0, 0), padx=(0 if column == 0 else 4, 0))
        self._widgets[key] = button
        self._attach_tooltip(button, self.TOOLTIP_TEXT.get(tooltip_key, ''))

    def widget(self, key: str) -> tk.Widget:
        return self._widgets[key]

    def selected_command_id(self) -> Optional[str]:
        selection = () if self._command_tree is None else self._command_tree.selection()
        return None if not selection else str(selection[0])

    def selected_base_id(self) -> Optional[str]:
        selection = () if self._base_tree is None else self._base_tree.selection()
        return None if not selection else str(selection[0])

    def collect_command_form(self) -> CommandFormData:
        values = {k: v.get() for k, v in self._command_vars.items()}
        values['action_type'] = self._display_to_internal_action(values['action_type'])
        values['interruptible'] = self._display_to_internal_interrupt(values['interruptible'])
        return CommandFormData(**values)

    def collect_base_form(self) -> BaseFormData:
        return BaseFormData(**{k: v.get() for k, v in self._base_vars.items()})

    def collect_scan_settings_form(self) -> ScanSettingsFormData:
        values = {}
        for key, var in self._scan_vars.items():
            values[key] = bool(var.get()) if isinstance(var, tk.BooleanVar) else var.get()
        return ScanSettingsFormData(**values)

    def set_command_form_values(self, **values) -> None:
        for key, value in values.items():
            if key not in self._command_vars:
                continue
            if key == 'action_type':
                value = self._internal_to_display_action(str(value))
            elif key == 'interruptible':
                value = self._internal_to_display_interrupt(str(value))
            self._command_vars[key].set('' if value is None else str(value))
        self.root.update_idletasks()

    def set_base_form_values(self, **values) -> None:
        for key, value in values.items():
            if key in self._base_vars:
                self._base_vars[key].set('' if value is None else str(value))
        self.root.update_idletasks()

    def set_scan_settings_form_values(self, **values) -> None:
        for key, value in values.items():
            if key not in self._scan_vars:
                continue
            variable = self._scan_vars[key]
            if isinstance(variable, tk.BooleanVar):
                variable.set(bool(value))
            else:
                variable.set('' if value is None else str(value))
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
        self._scan_error_var.set('')

    def submit_command_form(self) -> None:
        self._invoke('button:command_submit')

    def submit_base_form(self) -> None:
        self._invoke('button:base_submit')

    def submit_scan_settings_form(self) -> None:
        self._invoke('button:scan_submit')

    def start_agent(self) -> None:
        self._invoke('button:start_agent')

    def stop_agent(self) -> None:
        self._invoke('button:stop_agent')

    def reset_agent(self) -> None:
        self._invoke('button:reset_agent')

    def select_command(self, command_id: str) -> None:
        if self._command_tree is not None and command_id in self._command_tree.get_children():
            self._command_tree.selection_set((command_id,))
            self._command_tree.focus(command_id)
            self._command_tree.event_generate('<<TreeviewSelect>>')
        self.root.update_idletasks()

    def clear_command_selection(self) -> None:
        if self._command_tree is not None:
            self._command_tree.selection_remove(self._command_tree.selection())
            self._command_tree.focus('')
            self._command_tree.event_generate('<<TreeviewSelect>>')
        self.root.update_idletasks()

    def select_base(self, base_id: str) -> None:
        if self._base_tree is not None and base_id in self._base_tree.get_children():
            self._base_tree.selection_set((base_id,))
            self._base_tree.focus(base_id)
            self._base_tree.event_generate('<<TreeviewSelect>>')
        self.root.update_idletasks()

    def clear_base_selection(self) -> None:
        if self._base_tree is not None:
            self._base_tree.selection_remove(self._base_tree.selection())
            self._base_tree.focus('')
            self._base_tree.event_generate('<<TreeviewSelect>>')
        self.root.update_idletasks()

    def invoke_command_action(self, action_name: str) -> None:
        mapping = {
            'delete': 'button:command_delete', 'up': 'button:command_up', 'down': 'button:command_down',
            'pause': 'button:command_pause', 'resume': 'button:command_resume', 'cancel': 'button:command_cancel',
            'interrupt': 'button:command_interrupt', 'clear_selection': 'button:command_clear_selection',
        }
        self._invoke(mapping[action_name])

    def invoke_base_action(self, action_name: str) -> None:
        mapping = {'submit': 'button:base_submit', 'start_build': 'button:base_start_build', 'clear_selection': 'button:base_clear_selection'}
        self._invoke(mapping[action_name])

    def invoke_runtime_action(self, action_name: str) -> None:
        mapping = {'start': 'button:start_agent', 'stop': 'button:stop_agent', 'reset': 'button:reset_agent'}
        self._invoke(mapping[action_name])

    def refresh(self) -> None:
        status = self.callbacks.refresh_status()
        selected_command = self.selected_command_id()
        selected_base = self.selected_base_id()
        for key, label in self._status_labels.items():
            label.configure(text=self._translate_status_value(key, getattr(status, key)))
        if self._command_tree is not None:
            for item in self._command_tree.get_children():
                self._command_tree.delete(item)
            for command in status.command_queue:
                self._command_tree.insert('', 'end', iid=command.command_id, values=(self._internal_to_display_action(command.action_type), self._translate_command_status(command.status), command.priority, command.summary))
            if selected_command:
                self.select_command(selected_command)
        if self._base_tree is not None:
            for item in self._base_tree.get_children():
                self._base_tree.delete(item)
            for base in status.bases:
                self._base_tree.insert('', 'end', iid=base.base_id, values=(base.base_name, base.build_plan_id))
            if selected_base:
                self.select_base(selected_base)
        if self._log_text is not None:
            self._log_text.delete('1.0', tk.END)
            self._log_text.insert(tk.END, '\n'.join(status.logs))
        self._update_button_states(status)
        self.root.update_idletasks()

    def snapshot(self) -> PanelSnapshot:
        return PanelSnapshot(
            status=self.callbacks.refresh_status(),
            selected_command_id=self.selected_command_id() or '',
            selected_base_id=self.selected_base_id() or '',
            command_form=self.collect_command_form(),
            base_form=self.collect_base_form(),
            scan_settings_form=self.collect_scan_settings_form(),
        )

    def run(self) -> None:
        self.refresh()
        self.root.mainloop()

    def _on_start_agent(self) -> None:
        self.callbacks.start_agent()
        self.refresh()

    def _on_stop_agent(self) -> None:
        self.callbacks.stop_agent()
        self.refresh()

    def _on_reset_agent(self) -> None:
        self.callbacks.reset_agent()
        self.refresh()

    def _on_submit_command(self) -> None:
        self.callbacks.submit_command_form(self.collect_command_form(), self.selected_command_id())
        self.refresh()

    def _on_delete_command(self) -> None:
        self.callbacks.delete_selected_command(self.selected_command_id())
        self.refresh()

    def _on_move_command_up(self) -> None:
        self.callbacks.move_selected_up(self.selected_command_id())
        self.refresh()

    def _on_move_command_down(self) -> None:
        self.callbacks.move_selected_down(self.selected_command_id())
        self.refresh()

    def _on_pause_command(self) -> None:
        self.callbacks.pause_selected_command(self.selected_command_id())
        self.refresh()

    def _on_resume_command(self) -> None:
        self.callbacks.resume_selected_command(self.selected_command_id())
        self.refresh()

    def _on_cancel_command(self) -> None:
        self.callbacks.cancel_selected_command(self.selected_command_id())
        self.refresh()

    def _on_interrupt_command(self) -> None:
        self.callbacks.interrupt_selected_command(self.selected_command_id())
        self.refresh()

    def _on_submit_base(self) -> None:
        self.callbacks.submit_base_form(self.collect_base_form(), self.selected_base_id())
        self.refresh()

    def _on_start_build(self) -> None:
        self.callbacks.start_build_for_selected_base(self.selected_base_id())
        self.refresh()

    def _on_submit_scan_settings(self) -> None:
        try:
            self.callbacks.submit_scan_settings_form(self.collect_scan_settings_form())
        except ValueError as exc:
            self._scan_error_var.set(str(exc))
            self.root.update_idletasks()
            return
        self._scan_error_var.set('')
        self.load_scan_settings_form()
        self.refresh()

    def _invoke(self, key: str) -> None:
        widget = self._widgets[key]
        if hasattr(widget, 'invoke'):
            widget.invoke()
        self.root.update_idletasks()

    def _update_button_states(self, status: AgentStatusViewModel) -> None:
        start = self._widgets.get('button:start_agent')
        stop = self._widgets.get('button:stop_agent')
        if hasattr(start, 'configure'):
            start.configure(state='disabled' if status.agent_state == 'running' else 'normal')
        if hasattr(stop, 'configure'):
            stop.configure(state='normal' if status.agent_state == 'running' else 'disabled')

    def _attach_tooltip(self, widget: tk.Widget, text: str) -> None:
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
    def _translate_command_status(cls, value: str) -> str:
        return cls.COMMAND_STATUS_LABELS.get(value, value)

    @classmethod
    def _translate_status_value(cls, field_name: str, value: str) -> str:
        return cls.STATUS_VALUE_LABELS.get((field_name, value), value if value else 'none')
