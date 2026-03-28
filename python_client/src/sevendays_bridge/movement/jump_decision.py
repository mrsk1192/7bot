from __future__ import annotations

from dataclasses import dataclass
from typing import Optional

from .navigation_config import NavigationConfig


@dataclass(frozen=True)
class JumpDecisionResult:
    should_jump: bool
    reason: str
    obstacle_height: Optional[float]
    landing_clearance: Optional[float]
    forward_space: Optional[float]


class JumpDecision:
    """Strict jump gate. Jumping is denied unless every condition is satisfied."""

    def __init__(self, config: Optional[NavigationConfig] = None):
        self.config = config or NavigationConfig()

    def evaluate(
        self,
        terrain_summary,
        environment_summary,
        distance_to_target: float,
        recovery_attempt_index: int,
        jump_attempts_for_target: int,
        left_and_right_failed: bool,
    ) -> JumpDecisionResult:
        if not self.config.jump_only_for_bypass_when_capable:
            return JumpDecisionResult(False, "jump_disabled_by_config", None, None, None)
        if jump_attempts_for_target >= self.config.jump_max_attempts_per_target:
            return JumpDecisionResult(False, "jump_already_attempted_for_target", None, None, None)
        if recovery_attempt_index < 3 or not left_and_right_failed:
            return JumpDecisionResult(False, "left_right_recovery_not_exhausted", None, None, None)
        if environment_summary is None or terrain_summary is None:
            return JumpDecisionResult(False, "terrain_or_environment_unavailable", None, None, None)
        if environment_summary.fall_hazard_ahead_hint is not False:
            return JumpDecisionResult(False, "landing_not_safe", None, None, None)

        obstacle_height = terrain_summary.height_span
        if obstacle_height is None:
            obstacle_height = environment_summary.local_height_span
        if obstacle_height is None:
            return JumpDecisionResult(False, "obstacle_height_unknown", None, None, None)
        if obstacle_height <= 0.0:
            return JumpDecisionResult(False, "no_obstacle_detected", obstacle_height, None, None)
        if obstacle_height > self.config.max_jumpable_obstacle_height:
            return JumpDecisionResult(False, "obstacle_too_high", obstacle_height, None, None)

        forward_space = float(distance_to_target)
        if forward_space < self.config.required_forward_space_before_jump:
            return JumpDecisionResult(False, "insufficient_forward_space", obstacle_height, None, forward_space)

        landing_clearance = forward_space - obstacle_height
        if landing_clearance < self.config.required_landing_clearance:
            return JumpDecisionResult(False, "insufficient_landing_clearance", obstacle_height, landing_clearance, forward_space)

        return JumpDecisionResult(
            should_jump=True,
            reason="all_jump_conditions_satisfied",
            obstacle_height=obstacle_height,
            landing_clearance=landing_clearance,
            forward_space=forward_space,
        )

    def should_jump(
        self,
        terrain_summary,
        environment_summary,
        distance_to_target: float,
        recovery_attempt_index: int = 3,
        jump_attempts_for_target: int = 0,
        left_and_right_failed: bool = True,
    ) -> bool:
        return self.evaluate(
            terrain_summary=terrain_summary,
            environment_summary=environment_summary,
            distance_to_target=distance_to_target,
            recovery_attempt_index=recovery_attempt_index,
            jump_attempts_for_target=jump_attempts_for_target,
            left_and_right_failed=left_and_right_failed,
        ).should_jump
