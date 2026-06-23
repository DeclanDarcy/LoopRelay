using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Abstractions;

public interface IReasoningMaterializationReviewService
{
    Task<ReasoningMaterializationReviewReport> RunReviewAsync(
        Guid repositoryId,
        ReasoningMaterializationReviewRequest? request = null);
}
