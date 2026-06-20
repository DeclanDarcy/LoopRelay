namespace CommandCenter.Backend.Execution;

public interface IExecutionSessionService
{
    Task RecoverAsync();

    Task<RepositoryExecutionState> GetRepositoryStateAsync(Guid repositoryId);

    Task<ExecutionSessionSummary?> GetActiveSessionAsync(Guid repositoryId);

    Task<ExecutionSessionSummary?> GetRepositorySessionSummaryAsync(Guid repositoryId);

    Task<ExecutionSessionSummary> StartAsync(Guid repositoryId, ExecutionStartRequest request);

    Task<ExecutionSession?> GetSessionAsync(Guid sessionId);

    Task<ExecutionSessionSummary> AcceptAsync(Guid sessionId, ExecutionAcceptanceRequest request);

    Task<ExecutionSessionSummary> RejectAsync(Guid sessionId, ExecutionAcceptanceRequest request);

    Task<CommitPreparation> PrepareCommitAsync(Guid sessionId);
}
