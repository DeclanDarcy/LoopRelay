using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IRefinementAnalysisService
{
    Task<RefinementPlan> AnalyzeRefinementAsync(
        Guid repositoryId,
        string proposalId,
        DecisionRefinementAnalysisRequest request);
}
