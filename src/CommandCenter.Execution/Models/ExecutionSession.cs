using CommandCenter.Execution.Primitives;

namespace CommandCenter.Execution.Models;

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

    public ExecutionPromptManifest? PromptManifest { get; init; }

    public ExecutionRepositorySnapshot? RepositorySnapshot { get; init; }

    public CommitPreparation? CommitPreparation { get; init; }

    public string? CommitSha { get; init; }

    public DateTimeOffset? CommittedAt { get; init; }

    public string? CommitMessage { get; init; }

    public string? PreparationSnapshotId { get; init; }

    public DateTimeOffset? PushAttemptedAt { get; init; }

    public DateTimeOffset? PushedAt { get; init; }

    public string? PushedCommitSha { get; init; }

    public string? PushRemoteName { get; init; }

    public string? PushBranchName { get; init; }

    public string? PreviousHandoffContent { get; init; }

    public DateTimeOffset? PreviousHandoffCapturedAt { get; init; }

    public string? HandoffPath { get; init; }

    public ExecutionHandoffProcessing? HandoffProcessing { get; init; }

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
            CommitSha = CommitSha,
            CommittedAt = CommittedAt,
            CommitMessage = CommitMessage,
            PreparationSnapshotId = PreparationSnapshotId,
            PushAttemptedAt = PushAttemptedAt,
            PushedAt = PushedAt,
            PushedCommitSha = PushedCommitSha,
            PushRemoteName = PushRemoteName,
            PushBranchName = PushBranchName,
            FailureReason = FailureReason
        };
    }

    public static TimeSpan? CalculateDuration(DateTimeOffset startedAt, DateTimeOffset? completedAt)
    {
        return completedAt is null ? null : completedAt.Value - startedAt;
    }
}
