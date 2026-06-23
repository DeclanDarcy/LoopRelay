using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionQualityAssessmentService
{
    Task<DecisionQualityAssessment> AssessDecisionAsync(Guid repositoryId, string decisionId);

    Task<IReadOnlyList<DecisionQualityAssessment>> AssessRepositoryAsync(Guid repositoryId);
}
