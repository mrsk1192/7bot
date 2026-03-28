from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable, Optional

from .target_classifier import DecisionTarget


@dataclass(frozen=True)
class ScoredTarget:
    target: DecisionTarget
    score: float
    reason: str


class TargetPriority:
    """Scores deterministic targets using explicit bonuses and penalties."""

    BASE_KIND_BONUS = {
        "loot_candidate": 100.0,
        "resource_candidate": 80.0,
        "interactable_candidate": 60.0,
        "npc_candidate": 30.0,
        "hostile_candidate": -200.0,
        "ignore_candidate": -500.0,
    }

    def score_target(self, target: DecisionTarget, result_memory, hostile_nearby: bool = False) -> ScoredTarget:
        score = self.BASE_KIND_BONUS.get(target.category, -500.0)
        reasons = [f"base={score:.1f}"]

        if target.can_interact:
            score += 30.0
            reasons.append("can_interact=+30")

        distance_penalty = float(target.distance)
        score -= distance_penalty
        reasons.append(f"distance=-{distance_penalty:.1f}")

        confidence_bonus = float(target.candidate_confidence) * 20.0
        score += confidence_bonus
        reasons.append(f"confidence=+{confidence_bonus:.1f}")

        if target.line_of_sight_clear is True:
            score += 10.0
            reasons.append("los=+10")

        repeat_penalty = result_memory.get_repeat_penalty(target.target_key)
        if repeat_penalty:
            score -= repeat_penalty
            reasons.append(f"repeat=-{repeat_penalty:.1f}")

        unreachable_penalty = result_memory.get_unreachable_penalty(target.target_key)
        if unreachable_penalty:
            score -= unreachable_penalty
            reasons.append(f"unreachable=-{unreachable_penalty:.1f}")

        if hostile_nearby and target.category not in {"hostile_candidate"}:
            score -= 25.0
            reasons.append("hostile_nearby=-25")

        return ScoredTarget(target=target, score=score, reason=", ".join(reasons))

    def choose_best(self, targets: Iterable[DecisionTarget], result_memory, hostile_nearby: bool = False) -> Optional[ScoredTarget]:
        scored = [self.score_target(target, result_memory, hostile_nearby=hostile_nearby) for target in targets]
        if not scored:
            return None
        scored.sort(key=lambda item: (-item.score, item.target.distance, item.target.name))
        return scored[0]
