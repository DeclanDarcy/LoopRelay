namespace CommandCenter.Backend.Execution;

public sealed class CommitScopeItem
{
    public string Path { get; init; } = string.Empty;

    public CommitChangeType ChangeType { get; init; }

    public CommitChangeOrigin Origin { get; init; } = CommitChangeOrigin.ExecutionGenerated;

    public bool IsSelected { get; init; } = true;
}
