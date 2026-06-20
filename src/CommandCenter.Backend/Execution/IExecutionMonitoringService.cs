namespace CommandCenter.Backend.Execution;

public interface IExecutionMonitoringService
{
    IExecutionProviderObserver CreateProviderObserver(Guid sessionId);

    Task RecordProviderStartedAsync(Guid sessionId, DateTimeOffset startedAt);

    Task RecordFailureAsync(Guid sessionId, string reason);

    Task RecordRecoveryAsync(Guid sessionId, string message);

    Task<ExecutionStatus?> GetStatusAsync(Guid sessionId);

    Task<IReadOnlyList<ExecutionEvent>> GetEventsAsync(Guid sessionId);
}
