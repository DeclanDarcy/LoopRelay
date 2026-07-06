using LoopRelay.Execution.Primitives;

namespace LoopRelay.Execution.Models;

public sealed class ExecutionHandoffProcessing
{
    public bool HandoffProduced { get; init; }

    public bool HandoffMissing { get; init; }

    public bool HandoffArchived { get; init; }

    public string? ArchivePath { get; init; }

    public int? ArchiveSequence { get; init; }

    public bool ArchiveFailed { get; init; }

    public bool HandoffValidated { get; init; }

    public string? ValidationFailure { get; init; }

    public ExecutionSessionState ResultingSessionState { get; init; }

    public RepositoryExecutionState ResultingRepositoryState { get; init; }

    public DateTimeOffset ProcessedAt { get; init; }

    public bool ProviderFailureDistinctFromHandoffFailure { get; init; }

    public string? ProviderFailureReason { get; init; }

    public string? HandoffFailureReason { get; init; }
}
