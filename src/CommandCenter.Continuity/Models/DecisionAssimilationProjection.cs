namespace CommandCenter.Continuity.Models;

public sealed class DecisionAssimilationProjection
{
    public IReadOnlyList<DecisionAssimilationRecord> Decisions { get; init; } = [];

    public DecisionAssimilationLimit Limit { get; init; } = new();
}
