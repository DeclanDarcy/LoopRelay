namespace LoopRelay.Continuity.Models;

public sealed class DecisionAssimilationProjection
{
    public IReadOnlyList<DecisionAssimilationRecord> Decisions { get; init; } = [];

    public IReadOnlyList<ContinuityDecisionConsequence> Consequences { get; init; } = [];

    public IReadOnlyList<ContinuityDecisionContradiction> Contradictions { get; init; } = [];

    public DecisionAssimilationLimit Limit { get; init; } = new();
}
