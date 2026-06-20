namespace CommandCenter.Backend.Execution;

public sealed class ExecutionSessionSummary
{
    public Guid SessionId { get; init; }

    public ExecutionSessionState State { get; init; }

    public RepositoryExecutionState RepositoryState { get; init; } = RepositoryExecutionState.Ready;

    public string? MilestonePath { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }
}
