namespace CommandCenter.Continuity.Models;

public sealed class CompressionTrend
{
    public int ProposalCount { get; init; }

    public int CompressedItemCount { get; init; }

    public int RemovedItemCount { get; init; }

    public int ResolvedQuestionCount { get; init; }

    public int RetiredRiskCount { get; init; }

    public int WarningCount { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NoiseRemovedIndicators { get; init; } = [];
}
