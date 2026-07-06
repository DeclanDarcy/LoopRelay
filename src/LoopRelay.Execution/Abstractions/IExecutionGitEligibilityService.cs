using LoopRelay.Execution.Models;

namespace LoopRelay.Execution.Abstractions;

public interface IExecutionGitEligibilityService
{
    Task<ExecutionGitActionEligibility> GetEligibilityAsync(
        Guid sessionId,
        ExecutionGitActionEligibilityRequest request);
}
