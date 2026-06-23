using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionQualityAssessmentService
{
    Task<DecisionQualityAssessment> AssessDecisionAsync(Guid repositoryId, string decisionId);

    Task<IReadOnlyList<DecisionQualityAssessment>> AssessRepositoryAsync(Guid repositoryId);

    Task<DecisionQualityAssessment> AssessAndSaveDecisionAsync(Guid repositoryId, string decisionId);

    Task<IReadOnlyList<DecisionQualityAssessment>> AssessAndSaveRepositoryAsync(Guid repositoryId);

    Task<IReadOnlyList<DecisionQualityAssessment>> ListAssessmentsAsync(Guid repositoryId);

    Task<DecisionQualityAssessment> SaveAssessmentAsync(Guid repositoryId, DecisionQualityAssessment assessment);
}
