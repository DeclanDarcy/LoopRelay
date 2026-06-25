using CommandCenter.Execution.Models;

namespace CommandCenter.Execution.Abstractions;

public interface IExecutionGitEligibilityService
{
    Task<ExecutionGitActionEligibility> GetEligibilityAsync(
        Guid sessionId,
        ExecutionGitActionEligibilityRequest request);
}
