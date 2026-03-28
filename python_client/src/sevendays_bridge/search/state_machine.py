from __future__ import annotations

from enum import Enum


class SearchStateMachineState(str, Enum):
    IDLE = "IDLE"
    OBSERVE = "OBSERVE"
    EVALUATE_LOOK_TARGET = "EVALUATE_LOOK_TARGET"
    QUERY_CANDIDATES = "QUERY_CANDIDATES"
    SELECT_TARGET = "SELECT_TARGET"
    MOVE_TO_TARGET = "MOVE_TO_TARGET"
    ALIGN_VIEW = "ALIGN_VIEW"
    ACT = "ACT"
    VERIFY_RESULT = "VERIFY_RESULT"
    EXPLORE = "EXPLORE"
    RECOVER = "RECOVER"
    AVOID_HOSTILE = "AVOID_HOSTILE"
    FAILED = "FAILED"


class SearchStateMachine:
    def __init__(self) -> None:
        self.state = SearchStateMachineState.IDLE

    def transition(self, next_state: SearchStateMachineState) -> SearchStateMachineState:
        self.state = next_state
        return self.state
