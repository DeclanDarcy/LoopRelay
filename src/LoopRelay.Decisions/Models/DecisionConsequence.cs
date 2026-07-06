using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionConsequence(
    string Statement,
    TradeoffImpact Impact,
    IReadOnlyList<DecisionEvidence> Evidence);
