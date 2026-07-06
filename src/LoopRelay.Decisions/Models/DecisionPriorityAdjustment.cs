using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionPriorityAdjustment(
    DecisionCandidatePriority PreviousPriority,
    DecisionCandidatePriority NewPriority,
    string Reason,
    DecisionSourceReference Source,
    string? Attribution = null);
