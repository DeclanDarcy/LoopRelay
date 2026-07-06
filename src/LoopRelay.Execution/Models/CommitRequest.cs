namespace LoopRelay.Execution.Models;

public sealed class CommitRequest
{
    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<string> SelectedPaths { get; init; } = Array.Empty<string>();

    public string StatusSnapshotId { get; init; } = string.Empty;
}
