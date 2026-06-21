namespace CommandCenter.Execution.Models;

public sealed class ExecutionRepositorySnapshot
{
    public string Branch { get; init; } = string.Empty;

    public RepositoryDirtyState DirtyState { get; init; } = new();

    public DateTimeOffset CapturedAt { get; init; }
}
