using CommandCenter.Execution.Models;

namespace CommandCenter.Execution.Abstractions;

public interface IExecutionMonitoringService
{
    IExecutionProviderObserver CreateProviderObserver(Guid sessionId);

    Task RecordProviderStartedAsync(Guid sessionId, DateTimeOffset startedAt);

    Task RecordFailureAsync(Guid sessionId, string reason);

    Task RecordRecoveryAsync(Guid sessionId, string message);

    Task RecordCommitPreparationCreatedAsync(Guid sessionId, int changedPathCount, bool hasPreExistingChanges);

    Task RecordCommitSucceededAsync(Guid sessionId, string commitSha);

    Task RecordPushAttemptedAsync(Guid sessionId);

    Task RecordPushSucceededAsync(Guid sessionId, string? commitSha);

    Task RecordPushFailedAsync(Guid sessionId, string reason);

    Task RecordCancellationAsync(Guid sessionId, string reason);

    Task<ExecutionStatus?> GetStatusAsync(Guid sessionId);

    Task<IReadOnlyList<ExecutionEvent>> GetEventsAsync(Guid sessionId);

    IAsyncEnumerable<ExecutionEvent> StreamEventsAsync(Guid sessionId, CancellationToken cancellationToken);
}
