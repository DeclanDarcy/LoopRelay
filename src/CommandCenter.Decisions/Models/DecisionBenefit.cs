using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionBenefit(
    string Statement,
    TradeoffImpact Impact,
    IReadOnlyList<DecisionEvidence> Evidence);
