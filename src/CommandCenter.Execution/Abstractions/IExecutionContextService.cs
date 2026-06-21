
namespace CommandCenter.Execution.Abstractions;

public interface IExecutionContextService
{
    Task<ExecutionContext> BuildContextAsync(Guid repositoryId, string milestonePath);
}
