using LoopRelay.Execution.Primitives;

namespace LoopRelay.Execution.Models;

public sealed class ExecutionGitActionEligibility
{
    public Guid SessionId { get; init; }

    public bool SessionExists { get; init; }

    public RepositoryExecutionState RepositoryState { get; init; } = RepositoryExecutionState.Ready;

    public bool CommitPreparationLoaded { get; init; }

    public bool CommitPreparationCurrent { get; init; }

    public Guid? CommitPreparationId { get; init; }

    public string? PreparedStatusSnapshotId { get; init; }

    public string? CurrentStatusSnapshotId { get; init; }

    public int SelectedPathCount { get; init; }

    public int PreparedPathCount { get; init; }

    public IReadOnlyList<string> UnknownSelectedPaths { get; init; } = [];

    public bool CommitMessagePresent { get; init; }

    public bool RepositoryAllowsCommit { get; init; }

    public bool AwaitingPush { get; init; }

    public bool CommitShaExists { get; init; }

    public string? CommitSha { get; init; }

    public DateTimeOffset? PreviousPushAttemptedAt { get; init; }

    public string? PreviousPushFailure { get; init; }

    public ExecutionGitRemoteBranchState? RemoteBranchState { get; init; }

    public bool CanCommit { get; init; }

    public bool CanPush { get; init; }

    public IReadOnlyList<string> CommitDisabledReasons { get; init; } = [];

    public IReadOnlyList<string> PushDisabledReasons { get; init; } = [];

    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

public sealed class ExecutionGitRemoteBranchState
{
    public string Branch { get; init; } = string.Empty;

    public int AheadCount { get; init; }

    public int BehindCount { get; init; }

    public bool HasUnpushedChanges { get; init; }

    public bool HasRemoteDivergence { get; init; }

    public DateTimeOffset CapturedAt { get; init; }
}
