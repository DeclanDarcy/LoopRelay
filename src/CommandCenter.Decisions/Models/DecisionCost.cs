using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionCost(
    string Statement,
    TradeoffImpact Impact,
    IReadOnlyList<DecisionEvidence> Evidence);
