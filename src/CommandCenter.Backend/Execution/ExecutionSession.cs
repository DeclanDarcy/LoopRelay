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

    public ExecutionRepositorySnapshot? RepositorySnapshot { get; init; }

    public string? PreviousHandoffContent { get; init; }

    public DateTimeOffset? PreviousHandoffCapturedAt { get; init; }

    public string? FailureReason { get; init; }

    public ExecutionSessionSummary ToSummary()
    {
        return new ExecutionSessionSummary
        {
            SessionId = Id,
            State = State,
            RepositoryState = RepositoryState,
            MilestonePath = MilestonePath,
            StartedAt = StartedAt,
            CompletedAt = CompletedAt,
            LastActivityAt = LastActivityAt,
            ProviderName = ProviderName,
            FailureReason = FailureReason
        };
    }
}
