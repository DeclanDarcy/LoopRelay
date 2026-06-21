namespace CommandCenter.Execution.Models;

public sealed class CommitResult
{
    public string CommitSha { get; init; } = string.Empty;

    public DateTimeOffset CommittedAt { get; init; }

    public string CommitMessage { get; init; } = string.Empty;

    public string PreparationSnapshotId { get; init; } = string.Empty;

    public IReadOnlyList<string> SelectedPaths { get; init; } = Array.Empty<string>();
}
