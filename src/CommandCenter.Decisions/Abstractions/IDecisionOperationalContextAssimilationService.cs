using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionOperationalContextAssimilationService
{
    Task<DecisionAssimilationRecommendation?> GetRecommendationAsync(Guid repositoryId, string decisionId);

    Task<DecisionAssimilationRecommendation> ProposeOperationalContextAssimilationAsync(
        Guid repositoryId,
        string decisionId,
        CreateDecisionAssimilationRecommendationCommand? command);
}
