using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionBenefit(
    string Statement,
    TradeoffImpact Impact,
    IReadOnlyList<DecisionEvidence> Evidence);
