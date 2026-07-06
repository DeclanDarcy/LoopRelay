using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Abstractions;

public interface IReasoningMaterializationReviewService
{
    Task<ReasoningMaterializationReviewReport> RunReviewAsync(
        Guid repositoryId,
        ReasoningMaterializationReviewRequest? request = null);
}
