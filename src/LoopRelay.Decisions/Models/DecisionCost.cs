using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionCost(
    string Statement,
    TradeoffImpact Impact,
    IReadOnlyList<DecisionEvidence> Evidence);
