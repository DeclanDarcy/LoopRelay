namespace CommandCenter.Backend.Execution;

public sealed class ExecutionSession
{
    public Guid Id { get; init; }

    public Guid RepositoryId { get; init; }

    public string RepositoryPath { get; init; } = string.Empty;

    public string MilestonePath { get; init; } = string.Empty;

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public TimeSpan? Duration => CalculateDuration(StartedAt, CompletedAt);

    public DateTimeOffset? AcceptedAt { get; init; }

    public DateTimeOffset? RejectedAt { get; init; }

    public string? DecisionNote { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }

    public ExecutionSessionState State { get; init; } = ExecutionSessionState.Created;

    public RepositoryExecutionState RepositoryState { get; init; } = RepositoryExecutionState.Ready;

    public string ProviderName { get; init; } = string.Empty;

    public string? ProviderExecutablePath { get; init; }

    public int? ProviderProcessId { get; init; }

    public DateTimeOffset? ProviderStartedAt { get; init; }

    public ExecutionPromptMetadata? PromptMetadata { get; init; }

    public ExecutionRepositorySnapshot? RepositorySnapshot { get; init; }

    public string? PreviousHandoffContent { get; init; }

    public DateTimeOffset? PreviousHandoffCapturedAt { get; init; }

    public string? HandoffPath { get; init; }

    public string? FailureReason { get; init; }

    public IReadOnlyList<ExecutionEvent> Events { get; init; } = [];

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
            Duration = Duration,
            AcceptedAt = AcceptedAt,
            RejectedAt = RejectedAt,
            DecisionNote = DecisionNote,
            LastActivityAt = LastActivityAt,
            ProviderName = ProviderName,
            ProviderExecutablePath = ProviderExecutablePath,
            ProviderProcessId = ProviderProcessId,
            ProviderStartedAt = ProviderStartedAt,
            HandoffPath = HandoffPath,
            FailureReason = FailureReason
        };
    }

    public static TimeSpan? CalculateDuration(DateTimeOffset startedAt, DateTimeOffset? completedAt)
    {
        return completedAt is null ? null : completedAt.Value - startedAt;
    }
}
