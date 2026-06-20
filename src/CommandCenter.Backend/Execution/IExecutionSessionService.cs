namespace CommandCenter.Backend.Execution;

public interface IExecutionSessionService
{
    Task<RepositoryExecutionState> GetRepositoryStateAsync(Guid repositoryId);

    Task<ExecutionSessionSummary?> GetActiveSessionAsync(Guid repositoryId);
}
