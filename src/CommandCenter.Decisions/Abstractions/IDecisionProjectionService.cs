using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionProjectionService
{
    Task<ExecutionDecisionProjection> BuildExecutionProjectionAsync(
        Guid repositoryId,
        string? executionRequest = null,
        string? milestoneContent = null);
}
