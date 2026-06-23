using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionArtifactProjectionService
{
    Task ProjectDecisionAsync(Repository repository, Decision decision);

    Task ProjectCandidateAsync(Repository repository, DecisionCandidate candidate);

    Task ProjectProposalAsync(Repository repository, DecisionProposal proposal);

    Task ProjectProposalRevisionAsync(Repository repository, DecisionProposalRevision revision);

    Task ProjectProposalRevisionComparisonAsync(
        Repository repository,
        DecisionProposalRevisionComparison comparison);

    Task ProjectPackageVersionAsync(Repository repository, DecisionPackageVersion packageVersion);

    Task ProjectPackageComparisonAsync(
        Repository repository,
        DecisionPackageComparison comparison);

    Task ProjectRefinementArtifactAsync(
        Repository repository,
        DecisionRefinementArtifact refinementArtifact);

    Task ProjectDecisionAssimilationRecommendationAsync(
        Repository repository,
        DecisionAssimilationRecommendation recommendation);

    Task ProjectQualityAssessmentAsync(Repository repository, DecisionQualityAssessment assessment);

    Task ProjectQualityReportAsync(Repository repository, DecisionQualityReport report);

    Task ProjectQualityTrendAsync(Repository repository, DecisionQualityTrend trend);

    Task RefreshDecisionIndexAsync(Repository repository);

    Task RefreshAllAsync(Repository repository);

    Task RecoverMissingProjectionsAsync(Repository repository);
}
