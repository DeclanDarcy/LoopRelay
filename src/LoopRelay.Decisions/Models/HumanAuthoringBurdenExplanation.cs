using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record HumanAuthoringBurdenExplanation(
    string DecisionId,
    string SelectionRule,
    HumanAuthoringBurden EffectiveBurden,
    HumanAuthoringBurdenSignal? WinningSignal,
    bool IsUnknown,
    bool IsInferred,
    IReadOnlyList<string> Diagnostics);
