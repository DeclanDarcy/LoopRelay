namespace CommandCenter.Backend.Execution;

public sealed class ExecutionSession
{
    public Guid Id { get; init; }

    public Guid RepositoryId { get; init; }

    public string RepositoryPath { get; init; } = string.Empty;

    public string MilestonePath { get; init; } = string.Empty;

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }

    public ExecutionSessionState State { get; init; } = ExecutionSessionState.Created;

    public RepositoryExecutionState RepositoryState { get; init; } = RepositoryExecutionState.Ready;

    public string ProviderName { get; init; } = string.Empty;

    public string? FailureReason { get; init; }
}
