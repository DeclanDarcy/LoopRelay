namespace CommandCenter.Backend.Continuity;

public sealed class OperationalContextCompressionSummary
{
    public int PreservedItemCount { get; init; }

    public int AddedItemCount { get; init; }

    public int WarningCount { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];
}
