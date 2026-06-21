using CommandCenter.Execution.Primitives;

namespace CommandCenter.Execution.Models;

public sealed class ExecutionStatus
{
    public Guid SessionId { get; init; }

    public ExecutionSessionState State { get; init; }

    public RepositoryExecutionState RepositoryState { get; init; }

    public DateTimeOffset StartedAt { get; init; }

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

    public string? FailureReason { get; init; }

    public IReadOnlyList<ExecutionEvent> RecentEvents { get; init; } = [];
}
