using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class InMemoryDecisionRepository : IDecisionRepository
{
    private readonly Dictionary<Guid, SortedDictionary<string, Decision>> decisionsByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, DecisionCandidate>> candidatesByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, DecisionProposal>> proposalsByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, SortedDictionary<string, DecisionProposalRevision>>> revisionsByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, SortedDictionary<string, DecisionPackageVersion>>> packageVersionsByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, SortedDictionary<string, DecisionRefinementArtifact>>> refinementArtifactsByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, DecisionReviewStatus>> reviewStatusByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, SortedDictionary<string, DecisionReviewNote>>> reviewNotesByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, DecisionAssimilationRecommendation>> assimilationByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, DecisionGovernanceReport>> governanceReportsByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, DecisionCertificationReport>> certificationReportsByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, DecisionGenerationCertificationReport>> generationCertificationReportsByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, DecisionQualityAssessment>> qualityAssessmentsByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, DecisionQualityReport>> qualityReportsByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, DecisionQualityTrend>> qualityTrendsByRepository = [];

    public Task<DecisionId> AllocateDecisionIdAsync(Repository repository)
    {
        return Task.FromResult(new DecisionId(NextId(GetDecisions(repository.Id).Keys, "DEC")));
    }

    public Task<string> AllocateCandidateIdAsync(Repository repository)
    {
        return Task.FromResult(NextId(GetCandidates(repository.Id).Keys, "CAND"));
    }

    public Task<string> AllocateProposalIdAsync(Repository repository)
    {
        return Task.FromResult(NextId(GetProposals(repository.Id).Keys, "PROP"));
    }

    public Task<string> AllocateProposalRevisionIdAsync(Repository repository, string proposalId)
    {
        return Task.FromResult(NextId(GetRevisions(repository.Id, proposalId).Keys, "REV"));
    }

    public Task<string> AllocatePackageVersionIdAsync(Repository repository, string proposalId)
    {
        return Task.FromResult(NextId(GetPackageVersions(repository.Id, proposalId).Keys, "PKG"));
    }

    public Task<string> AllocateRefinementArtifactIdAsync(Repository repository, string proposalId)
    {
        return Task.FromResult(NextId(GetRefinementArtifacts(repository.Id, proposalId).Keys, "REF"));
    }

    public Task<string> AllocateReviewNoteIdAsync(Repository repository, string proposalId)
    {
        return Task.FromResult(NextId(GetReviewNotes(repository.Id, proposalId).Keys, "NOTE"));
    }

    public Task<IReadOnlyList<Decision>> ListDecisionsAsync(Repository repository)
    {
        return Task.FromResult<IReadOnlyList<Decision>>(GetDecisions(repository.Id).Values.ToArray());
    }

    public Task<Decision?> GetDecisionAsync(Repository repository, DecisionId decisionId)
    {
        GetDecisions(repository.Id).TryGetValue(decisionId.Value, out Decision? decision);
        return Task.FromResult(decision);
    }

    public Task<Decision> SaveDecisionAsync(Repository repository, Decision decision)
    {
        if (decision.Metadata.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision belongs to a different repository.");
        }

        GetDecisions(repository.Id)[decision.Id.Value] = decision;
        return Task.FromResult(decision);
    }

    public Task<IReadOnlyList<DecisionCandidate>> ListCandidatesAsync(Repository repository)
    {
        return Task.FromResult<IReadOnlyList<DecisionCandidate>>(GetCandidates(repository.Id).Values.ToArray());
    }

    public Task<DecisionCandidate?> GetCandidateAsync(Repository repository, string candidateId)
    {
        GetCandidates(repository.Id).TryGetValue(candidateId, out DecisionCandidate? candidate);
        return Task.FromResult(candidate);
    }

    public Task<DecisionCandidate> SaveCandidateAsync(Repository repository, DecisionCandidate candidate)
    {
        if (candidate.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision candidate belongs to a different repository.");
        }

        GetCandidates(repository.Id)[candidate.Id] = candidate;
        return Task.FromResult(candidate);
    }

    public Task<IReadOnlyList<DecisionProposal>> ListProposalsAsync(Repository repository)
    {
        return Task.FromResult<IReadOnlyList<DecisionProposal>>(GetProposals(repository.Id).Values.ToArray());
    }

    public Task<DecisionProposal?> GetProposalAsync(Repository repository, string proposalId)
    {
        GetProposals(repository.Id).TryGetValue(proposalId, out DecisionProposal? proposal);
        return Task.FromResult(proposal);
    }

    public Task<DecisionProposal> SaveProposalAsync(Repository repository, DecisionProposal proposal)
    {
        if (proposal.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision proposal belongs to a different repository.");
        }

        GetProposals(repository.Id)[proposal.Id] = proposal;
        return Task.FromResult(proposal);
    }

    public Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Repository repository, string proposalId)
    {
        return Task.FromResult<IReadOnlyList<DecisionProposalRevision>>(GetRevisions(repository.Id, proposalId).Values.ToArray());
    }

    public Task<DecisionProposalRevision> SaveProposalRevisionAsync(Repository repository, DecisionProposalRevision revision)
    {
        if (revision.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision proposal revision belongs to a different repository.");
        }

        GetRevisions(repository.Id, revision.ProposalId)[revision.Id] = revision;
        return Task.FromResult(revision);
    }

    public Task<IReadOnlyList<DecisionPackageVersion>> ListPackageVersionsAsync(Repository repository, string proposalId)
    {
        return Task.FromResult<IReadOnlyList<DecisionPackageVersion>>(GetPackageVersions(repository.Id, proposalId).Values.ToArray());
    }

    public Task<DecisionPackageVersion?> GetPackageVersionAsync(Repository repository, string proposalId, string packageId)
    {
        GetPackageVersions(repository.Id, proposalId).TryGetValue(packageId, out DecisionPackageVersion? packageVersion);
        return Task.FromResult(packageVersion);
    }

    public Task<DecisionPackageVersion> SavePackageVersionAsync(Repository repository, DecisionPackageVersion packageVersion)
    {
        if (packageVersion.RepositoryId != repository.Id || packageVersion.Package.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision package version belongs to a different repository.");
        }

        SortedDictionary<string, DecisionPackageVersion> versions = GetPackageVersions(repository.Id, packageVersion.ProposalId);
        if (versions.ContainsKey(packageVersion.Id))
        {
            throw new InvalidOperationException($"Decision package version already exists: {packageVersion.Id}.");
        }

        versions[packageVersion.Id] = packageVersion;
        return Task.FromResult(packageVersion);
    }

    public Task<IReadOnlyList<DecisionRefinementArtifact>> ListRefinementArtifactsAsync(Repository repository, string proposalId)
    {
        return Task.FromResult<IReadOnlyList<DecisionRefinementArtifact>>(GetRefinementArtifacts(repository.Id, proposalId).Values.ToArray());
    }

    public Task<DecisionRefinementArtifact> SaveRefinementArtifactAsync(
        Repository repository,
        DecisionRefinementArtifact refinementArtifact)
    {
        if (refinementArtifact.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision refinement artifact belongs to a different repository.");
        }

        SortedDictionary<string, DecisionRefinementArtifact> refinements =
            GetRefinementArtifacts(repository.Id, refinementArtifact.ProposalId);
        if (refinements.ContainsKey(refinementArtifact.Id))
        {
            throw new InvalidOperationException($"Decision refinement artifact already exists: {refinementArtifact.Id}.");
        }

        refinements[refinementArtifact.Id] = refinementArtifact;
        return Task.FromResult(refinementArtifact);
    }

    public Task<DecisionReviewStatus?> GetReviewStatusAsync(Repository repository, string proposalId)
    {
        GetReviewStatuses(repository.Id).TryGetValue(proposalId, out DecisionReviewStatus? reviewStatus);
        return Task.FromResult(reviewStatus);
    }

    public Task<DecisionReviewStatus> SaveReviewStatusAsync(Repository repository, DecisionReviewStatus reviewStatus)
    {
        if (reviewStatus.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision review status belongs to a different repository.");
        }

        GetReviewStatuses(repository.Id)[reviewStatus.ProposalId] = reviewStatus;
        return Task.FromResult(reviewStatus);
    }

    public Task<IReadOnlyList<DecisionReviewNote>> ListReviewNotesAsync(Repository repository, string proposalId)
    {
        return Task.FromResult<IReadOnlyList<DecisionReviewNote>>(GetReviewNotes(repository.Id, proposalId).Values.ToArray());
    }

    public Task<DecisionReviewNote> SaveReviewNoteAsync(Repository repository, DecisionReviewNote note)
    {
        if (note.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision review note belongs to a different repository.");
        }

        GetReviewNotes(repository.Id, note.ProposalId)[note.Id] = note;
        return Task.FromResult(note);
    }

    public Task<DecisionAssimilationRecommendation?> GetAssimilationRecommendationAsync(
        Repository repository,
        DecisionId decisionId)
    {
        GetAssimilationRecommendations(repository.Id).TryGetValue(decisionId.Value, out DecisionAssimilationRecommendation? recommendation);
        return Task.FromResult(recommendation);
    }

    public Task<DecisionAssimilationRecommendation> SaveAssimilationRecommendationAsync(
        Repository repository,
        DecisionAssimilationRecommendation recommendation)
    {
        if (recommendation.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision assimilation recommendation belongs to a different repository.");
        }

        GetAssimilationRecommendations(repository.Id)[recommendation.DecisionId] = recommendation;
        return Task.FromResult(recommendation);
    }

    public Task<IReadOnlyList<DecisionGovernanceReport>> ListGovernanceReportsAsync(Repository repository)
    {
        return Task.FromResult<IReadOnlyList<DecisionGovernanceReport>>(GetGovernanceReports(repository.Id).Values.ToArray());
    }

    public Task<DecisionGovernanceReport> SaveGovernanceReportAsync(
        Repository repository,
        DecisionGovernanceReport report)
    {
        if (report.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision governance report belongs to a different repository.");
        }

        GetGovernanceReports(repository.Id)[report.Id] = report;
        return Task.FromResult(report);
    }

    public Task<IReadOnlyList<DecisionCertificationReport>> ListCertificationReportsAsync(Repository repository)
    {
        return Task.FromResult<IReadOnlyList<DecisionCertificationReport>>(GetCertificationReports(repository.Id).Values.ToArray());
    }

    public Task<DecisionCertificationReport> SaveCertificationReportAsync(
        Repository repository,
        DecisionCertificationReport report)
    {
        if (report.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision certification report belongs to a different repository.");
        }

        GetCertificationReports(repository.Id)[report.Id] = report;
        return Task.FromResult(report);
    }

    public Task<IReadOnlyList<DecisionGenerationCertificationReport>> ListGenerationCertificationReportsAsync(
        Repository repository)
    {
        return Task.FromResult<IReadOnlyList<DecisionGenerationCertificationReport>>(
            GetGenerationCertificationReports(repository.Id).Values.ToArray());
    }

    public Task<DecisionGenerationCertificationReport> SaveGenerationCertificationReportAsync(
        Repository repository,
        DecisionGenerationCertificationReport report)
    {
        if (report.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision generation certification report belongs to a different repository.");
        }

        GetGenerationCertificationReports(repository.Id)[report.Id] = report;
        return Task.FromResult(report);
    }

    public Task<IReadOnlyList<DecisionQualityAssessment>> ListQualityAssessmentsAsync(Repository repository)
    {
        return Task.FromResult<IReadOnlyList<DecisionQualityAssessment>>(GetQualityAssessments(repository.Id).Values.ToArray());
    }

    public Task<DecisionQualityAssessment> SaveQualityAssessmentAsync(
        Repository repository,
        DecisionQualityAssessment assessment)
    {
        if (assessment.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision quality assessment belongs to a different repository.");
        }

        GetQualityAssessments(repository.Id)[assessment.Id] = assessment;
        return Task.FromResult(assessment);
    }

    public Task<IReadOnlyList<DecisionQualityReport>> ListQualityReportsAsync(Repository repository)
    {
        return Task.FromResult<IReadOnlyList<DecisionQualityReport>>(GetQualityReports(repository.Id).Values.ToArray());
    }

    public Task<DecisionQualityReport> SaveQualityReportAsync(
        Repository repository,
        DecisionQualityReport report)
    {
        if (report.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision quality report belongs to a different repository.");
        }

        GetQualityReports(repository.Id)[report.Id] = report;
        return Task.FromResult(report);
    }

    public Task<IReadOnlyList<DecisionQualityTrend>> ListQualityTrendsAsync(Repository repository)
    {
        return Task.FromResult<IReadOnlyList<DecisionQualityTrend>>(GetQualityTrends(repository.Id).Values.ToArray());
    }

    public Task<DecisionQualityTrend> SaveQualityTrendAsync(
        Repository repository,
        DecisionQualityTrend trend)
    {
        if (trend.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision quality trend belongs to a different repository.");
        }

        GetQualityTrends(repository.Id)[trend.Id] = trend;
        return Task.FromResult(trend);
    }

    private SortedDictionary<string, Decision> GetDecisions(Guid repositoryId)
    {
        return GetRepositoryMap(decisionsByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionCandidate> GetCandidates(Guid repositoryId)
    {
        return GetRepositoryMap(candidatesByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionProposal> GetProposals(Guid repositoryId)
    {
        return GetRepositoryMap(proposalsByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionReviewStatus> GetReviewStatuses(Guid repositoryId)
    {
        return GetRepositoryMap(reviewStatusByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionProposalRevision> GetRevisions(Guid repositoryId, string proposalId)
    {
        SortedDictionary<string, SortedDictionary<string, DecisionProposalRevision>> repositoryRevisions =
            GetRepositoryMap(revisionsByRepository, repositoryId);
        if (!repositoryRevisions.TryGetValue(proposalId, out SortedDictionary<string, DecisionProposalRevision>? revisions))
        {
            revisions = new SortedDictionary<string, DecisionProposalRevision>(StringComparer.Ordinal);
            repositoryRevisions[proposalId] = revisions;
        }

        return revisions;
    }

    private SortedDictionary<string, DecisionPackageVersion> GetPackageVersions(Guid repositoryId, string proposalId)
    {
        SortedDictionary<string, SortedDictionary<string, DecisionPackageVersion>> repositoryPackageVersions =
            GetRepositoryMap(packageVersionsByRepository, repositoryId);
        if (!repositoryPackageVersions.TryGetValue(proposalId, out SortedDictionary<string, DecisionPackageVersion>? versions))
        {
            versions = new SortedDictionary<string, DecisionPackageVersion>(StringComparer.Ordinal);
            repositoryPackageVersions[proposalId] = versions;
        }

        return versions;
    }

    private SortedDictionary<string, DecisionRefinementArtifact> GetRefinementArtifacts(Guid repositoryId, string proposalId)
    {
        SortedDictionary<string, SortedDictionary<string, DecisionRefinementArtifact>> repositoryRefinements =
            GetRepositoryMap(refinementArtifactsByRepository, repositoryId);
        if (!repositoryRefinements.TryGetValue(proposalId, out SortedDictionary<string, DecisionRefinementArtifact>? refinements))
        {
            refinements = new SortedDictionary<string, DecisionRefinementArtifact>(StringComparer.Ordinal);
            repositoryRefinements[proposalId] = refinements;
        }

        return refinements;
    }

    private SortedDictionary<string, DecisionReviewNote> GetReviewNotes(Guid repositoryId, string proposalId)
    {
        SortedDictionary<string, SortedDictionary<string, DecisionReviewNote>> repositoryNotes =
            GetRepositoryMap(reviewNotesByRepository, repositoryId);
        if (!repositoryNotes.TryGetValue(proposalId, out SortedDictionary<string, DecisionReviewNote>? notes))
        {
            notes = new SortedDictionary<string, DecisionReviewNote>(StringComparer.Ordinal);
            repositoryNotes[proposalId] = notes;
        }

        return notes;
    }

    private SortedDictionary<string, DecisionAssimilationRecommendation> GetAssimilationRecommendations(Guid repositoryId)
    {
        return GetRepositoryMap(assimilationByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionGovernanceReport> GetGovernanceReports(Guid repositoryId)
    {
        return GetRepositoryMap(governanceReportsByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionCertificationReport> GetCertificationReports(Guid repositoryId)
    {
        return GetRepositoryMap(certificationReportsByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionGenerationCertificationReport> GetGenerationCertificationReports(Guid repositoryId)
    {
        return GetRepositoryMap(generationCertificationReportsByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionQualityAssessment> GetQualityAssessments(Guid repositoryId)
    {
        return GetRepositoryMap(qualityAssessmentsByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionQualityReport> GetQualityReports(Guid repositoryId)
    {
        return GetRepositoryMap(qualityReportsByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionQualityTrend> GetQualityTrends(Guid repositoryId)
    {
        return GetRepositoryMap(qualityTrendsByRepository, repositoryId);
    }

    private static SortedDictionary<string, T> GetRepositoryMap<T>(
        Dictionary<Guid, SortedDictionary<string, T>> maps,
        Guid repositoryId)
    {
        if (!maps.TryGetValue(repositoryId, out SortedDictionary<string, T>? map))
        {
            map = new SortedDictionary<string, T>(StringComparer.Ordinal);
            maps[repositoryId] = map;
        }

        return map;
    }

    private static string NextId(IEnumerable<string> existingIds, string prefix)
    {
        int next = existingIds
            .Select(id => ParseSequence(id, prefix))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}-{next:0000}";
    }

    private static int ParseSequence(string id, string prefix)
    {
        return id.StartsWith($"{prefix}-", StringComparison.Ordinal) &&
            int.TryParse(id[(prefix.Length + 1)..], out int sequence)
            ? sequence
            : 0;
    }
}
