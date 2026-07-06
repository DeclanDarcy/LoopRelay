using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IDecisionProjectionService
{
    Task<ExecutionDecisionProjection> BuildExecutionProjectionAsync(
        Guid repositoryId,
        string? executionRequest = null,
        string? milestoneContent = null);
}
