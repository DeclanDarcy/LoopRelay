namespace CommandCenter.Backend.Execution;

public interface IExecutionContextService
{
    Task<ExecutionContext> BuildContextAsync(Guid repositoryId, string milestonePath);
}
