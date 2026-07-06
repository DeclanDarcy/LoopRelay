namespace LoopRelay.Continuity.Models;

public sealed class DecisionAssimilationLimit
{
    public int Limit { get; init; }

    public string Reason { get; init; } = string.Empty;

    public int TotalAnalyzedItemCount { get; init; }

    public int TotalQualifyingItemCount { get; init; }

    public int AssimilatedItemCount { get; init; }

    public int OmittedItemCount { get; init; }
}
