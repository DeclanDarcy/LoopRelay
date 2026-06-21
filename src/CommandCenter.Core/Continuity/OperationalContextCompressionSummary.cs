namespace CommandCenter.Core.Continuity;

public sealed class OperationalContextCompressionSummary
{
    public int PreservedItemCount { get; init; }

    public int AddedItemCount { get; init; }

    public int ModifiedItemCount { get; init; }

    public int RemovedItemCount { get; init; }

    public int CompressedItemCount { get; init; }

    public int PermanentUnderstandingItemCount { get; init; }

    public int ActiveUnderstandingItemCount { get; init; }

    public int HistoricalUnderstandingItemCount { get; init; }

    public int HistoricalNoiseItemCount { get; init; }

    public int ResolvedQuestionCount { get; init; }

    public int RetiredRiskCount { get; init; }

    public int WarningCount { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> RevisionSummary { get; init; } = [];

    public IReadOnlyList<string> NoiseRemovedIndicators { get; init; } = [];

    public IReadOnlyList<string> StableUnderstandingRetentionWarnings { get; init; } = [];
}
