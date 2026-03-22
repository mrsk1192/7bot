from __future__ import annotations

PRESS_ACTIONS = {
    "move_forward": "move_forward_start",
    "move_back": "move_back_start",
    "move_left": "move_left_start",
    "move_right": "move_right_start",
    "sprint": "sprint_start",
    "crouch": "crouch_start",
    "primary_action": "primary_action_start",
    "secondary_action": "secondary_action_start",
    "hold_interact": "hold_interact_start",
    "aim": "aim_start",
}

RELEASE_ACTIONS = {
    "move_forward": "move_forward_stop",
    "move_back": "move_back_stop",
    "move_left": "move_left_stop",
    "move_right": "move_right_stop",
    "sprint": "sprint_stop",
    "crouch": "crouch_stop",
    "primary_action": "primary_action_stop",
    "secondary_action": "secondary_action_stop",
    "hold_interact": "hold_interact_stop",
    "aim": "aim_stop",
}

TAP_ACTIONS = {
    "jump": "jump",
    "reload": "reload",
    "interact": "use_interact",
    "attack_primary": "attack_light_tap",
    "hotbar_next": "hotbar_next",
    "hotbar_prev": "hotbar_prev",
    "mouse_wheel_up": "mouse_wheel_up",
    "mouse_wheel_down": "mouse_wheel_down",
    "toggle_inventory": "toggle_inventory",
    "toggle_map": "toggle_map",
    "toggle_quest_log": "toggle_quest_log",
    "escape_menu": "escape_menu",
    "confirm": "confirm",
    "cancel": "cancel",
    "toggle_flashlight": "toggle_flashlight",
    "console_toggle": "console_toggle",
    "respawn_default": "respawn_select_default",
    "respawn_bedroll": "respawn_at_bedroll",
    "respawn_near_bedroll": "respawn_near_bedroll",
    "respawn_random": "respawn_at_random",
    "respawn_confirm": "respawn_confirm",
    "respawn_cancel": "respawn_cancel",
    "stop_all": "stop_all_input",
}


def resolve_press(name: str) -> str:
    if name not in PRESS_ACTIONS:
        raise ValueError(f"'{name}' is not a press/release action")
    return PRESS_ACTIONS[name]


def resolve_release(name: str) -> str:
    if name not in RELEASE_ACTIONS:
        raise ValueError(f"'{name}' is not a press/release action")
    return RELEASE_ACTIONS[name]


def resolve_tap(name: str) -> str:
    if name not in TAP_ACTIONS:
        raise ValueError(f"'{name}' is not a tap action")
    return TAP_ACTIONS[name]
