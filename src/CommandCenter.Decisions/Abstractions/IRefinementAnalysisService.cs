using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IRefinementAnalysisService
{
    Task<RefinementPlan> AnalyzeRefinementAsync(
        Guid repositoryId,
        string proposalId,
        DecisionRefinementAnalysisRequest request);
}
