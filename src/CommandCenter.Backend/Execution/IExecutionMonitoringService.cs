namespace CommandCenter.Backend.Execution;

public interface IExecutionMonitoringService
{
    IExecutionProviderObserver CreateProviderObserver(Guid sessionId);

    Task RecordProviderStartedAsync(Guid sessionId, DateTimeOffset startedAt);

    Task RecordFailureAsync(Guid sessionId, string reason);

    Task RecordRecoveryAsync(Guid sessionId, string message);

    Task RecordCancellationAsync(Guid sessionId, string reason);

    Task<ExecutionStatus?> GetStatusAsync(Guid sessionId);

    Task<IReadOnlyList<ExecutionEvent>> GetEventsAsync(Guid sessionId);

    IAsyncEnumerable<ExecutionEvent> StreamEventsAsync(Guid sessionId, CancellationToken cancellationToken);
}
