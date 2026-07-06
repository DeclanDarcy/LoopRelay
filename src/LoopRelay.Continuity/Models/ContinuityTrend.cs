namespace LoopRelay.Continuity.Models;

public sealed class ContinuityTrend
{
    public int AddedCount { get; init; }

    public int ModifiedCount { get; init; }

    public int RemovedCount { get; init; }

    public int ResolvedCount { get; init; }

    public int LostCount { get; init; }
}
