namespace CommandCenter.Backend.Execution;

public sealed class ExecutionSessionSummary
{
    public Guid SessionId { get; init; }

    public ExecutionSessionState State { get; init; }

    public RepositoryExecutionState RepositoryState { get; init; } = RepositoryExecutionState.Ready;

    public string? MilestonePath { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public TimeSpan? Duration { get; init; }

    public DateTimeOffset? AcceptedAt { get; init; }

    public DateTimeOffset? RejectedAt { get; init; }

    public string? DecisionNote { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }

    public string ProviderName { get; init; } = string.Empty;

    public string? ProviderExecutablePath { get; init; }

    public int? ProviderProcessId { get; init; }

    public DateTimeOffset? ProviderStartedAt { get; init; }

    public string? HandoffPath { get; init; }

    public string? CommitSha { get; init; }

    public DateTimeOffset? CommittedAt { get; init; }

    public string? CommitMessage { get; init; }

    public string? PreparationSnapshotId { get; init; }

    public string? FailureReason { get; init; }
}
