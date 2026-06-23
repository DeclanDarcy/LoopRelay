using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionRepository
{
    Task<DecisionId> AllocateDecisionIdAsync(Repository repository);

    Task<string> AllocateCandidateIdAsync(Repository repository);

    Task<string> AllocateProposalIdAsync(Repository repository);

    Task<string> AllocateProposalRevisionIdAsync(Repository repository, string proposalId);

    Task<string> AllocatePackageVersionIdAsync(Repository repository, string proposalId);

    Task<string> AllocateRefinementArtifactIdAsync(Repository repository, string proposalId);

    Task<string> AllocateReviewNoteIdAsync(Repository repository, string proposalId);

    Task<IReadOnlyList<Decision>> ListDecisionsAsync(Repository repository);

    Task<Decision?> GetDecisionAsync(Repository repository, DecisionId decisionId);

    Task<Decision> SaveDecisionAsync(Repository repository, Decision decision);

    Task<IReadOnlyList<DecisionCandidate>> ListCandidatesAsync(Repository repository);

    Task<DecisionCandidate?> GetCandidateAsync(Repository repository, string candidateId);

    Task<DecisionCandidate> SaveCandidateAsync(Repository repository, DecisionCandidate candidate);

    Task<IReadOnlyList<DecisionProposal>> ListProposalsAsync(Repository repository);

    Task<DecisionProposal?> GetProposalAsync(Repository repository, string proposalId);

    Task<DecisionProposal> SaveProposalAsync(Repository repository, DecisionProposal proposal);

    Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Repository repository, string proposalId);

    Task<DecisionProposalRevision> SaveProposalRevisionAsync(Repository repository, DecisionProposalRevision revision);

    Task<IReadOnlyList<DecisionPackageVersion>> ListPackageVersionsAsync(Repository repository, string proposalId);

    Task<DecisionPackageVersion?> GetPackageVersionAsync(Repository repository, string proposalId, string packageId);

    Task<DecisionPackageVersion> SavePackageVersionAsync(Repository repository, DecisionPackageVersion packageVersion);

    Task<IReadOnlyList<DecisionRefinementArtifact>> ListRefinementArtifactsAsync(Repository repository, string proposalId);

    Task<DecisionRefinementArtifact> SaveRefinementArtifactAsync(
        Repository repository,
        DecisionRefinementArtifact refinementArtifact);

    Task<DecisionReviewStatus?> GetReviewStatusAsync(Repository repository, string proposalId);

    Task<DecisionReviewStatus> SaveReviewStatusAsync(Repository repository, DecisionReviewStatus reviewStatus);

    Task<IReadOnlyList<DecisionReviewNote>> ListReviewNotesAsync(Repository repository, string proposalId);

    Task<DecisionReviewNote> SaveReviewNoteAsync(Repository repository, DecisionReviewNote note);

    Task<DecisionAssimilationRecommendation?> GetAssimilationRecommendationAsync(Repository repository, DecisionId decisionId);

    Task<DecisionAssimilationRecommendation> SaveAssimilationRecommendationAsync(
        Repository repository,
        DecisionAssimilationRecommendation recommendation);

    Task<IReadOnlyList<DecisionGovernanceReport>> ListGovernanceReportsAsync(Repository repository);

    Task<DecisionGovernanceReport> SaveGovernanceReportAsync(Repository repository, DecisionGovernanceReport report);

    Task<IReadOnlyList<DecisionCertificationReport>> ListCertificationReportsAsync(Repository repository);

    Task<DecisionCertificationReport> SaveCertificationReportAsync(Repository repository, DecisionCertificationReport report);

    Task<IReadOnlyList<DecisionQualityAssessment>> ListQualityAssessmentsAsync(Repository repository);

    Task<DecisionQualityAssessment> SaveQualityAssessmentAsync(Repository repository, DecisionQualityAssessment assessment);

    Task<IReadOnlyList<DecisionQualityReport>> ListQualityReportsAsync(Repository repository);

    Task<DecisionQualityReport> SaveQualityReportAsync(Repository repository, DecisionQualityReport report);

    Task<IReadOnlyList<DecisionQualityTrend>> ListQualityTrendsAsync(Repository repository);

    Task<DecisionQualityTrend> SaveQualityTrendAsync(Repository repository, DecisionQualityTrend trend);
}
