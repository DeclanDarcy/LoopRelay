using CommandCenter.Core.Repositories;

namespace CommandCenter.Execution.Models;

public sealed class CommitStatusSnapshot
{
    public string Id { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public int AheadCount { get; init; }

    public int BehindCount { get; init; }

    public RepositoryDirtyState DirtyState { get; init; } = new();

    public DateTimeOffset CapturedAt { get; init; }
}
