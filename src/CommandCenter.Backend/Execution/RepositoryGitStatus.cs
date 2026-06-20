namespace CommandCenter.Backend.Execution;

public sealed class RepositoryGitStatus
{
    public string Branch { get; init; } = string.Empty;

    public int AheadCount { get; init; }

    public int BehindCount { get; init; }

    public RepositoryDirtyState DirtyState { get; init; } = new();

    public DateTimeOffset CapturedAt { get; init; }
}
