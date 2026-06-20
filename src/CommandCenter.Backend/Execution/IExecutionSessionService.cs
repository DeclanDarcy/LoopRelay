namespace CommandCenter.Backend.Execution;

public interface IExecutionSessionService
{
    Task RecoverAsync();

    Task<RepositoryExecutionState> GetRepositoryStateAsync(Guid repositoryId);

    Task<ExecutionSessionSummary?> GetActiveSessionAsync(Guid repositoryId);

    Task<ExecutionSessionSummary?> GetRepositorySessionSummaryAsync(Guid repositoryId);

    Task<IReadOnlyList<ExecutionSessionSummary>> GetRepositorySessionHistoryAsync(Guid repositoryId, int limit = 10);

    Task<ExecutionSessionSummary> StartAsync(Guid repositoryId, ExecutionStartRequest request);

    Task<ExecutionSession?> GetSessionAsync(Guid sessionId);

    Task<ExecutionSessionSummary> AcceptAsync(Guid sessionId, ExecutionAcceptanceRequest request);

    Task<ExecutionSessionSummary> RejectAsync(Guid sessionId, ExecutionAcceptanceRequest request);

    Task<CommitPreparation> PrepareCommitAsync(Guid sessionId);

    Task<ExecutionSessionSummary> CommitAsync(Guid sessionId, CommitRequest request);

    Task<ExecutionSessionSummary> PushAsync(Guid sessionId, PushRequest request);
}
