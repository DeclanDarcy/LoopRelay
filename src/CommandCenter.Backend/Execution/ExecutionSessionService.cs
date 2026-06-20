namespace CommandCenter.Backend.Execution;

public sealed class ExecutionSessionService : IExecutionSessionService
{
    public Task<RepositoryExecutionState> GetRepositoryStateAsync(Guid repositoryId)
    {
        return Task.FromResult(RepositoryExecutionState.Ready);
    }

    public Task<ExecutionSessionSummary?> GetActiveSessionAsync(Guid repositoryId)
    {
        return Task.FromResult<ExecutionSessionSummary?>(null);
    }
}
