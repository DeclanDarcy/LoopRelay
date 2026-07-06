using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Persistence;
using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Services;

public sealed class DecisionCertificationService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionGovernanceService governanceService,
    IDecisionProjectionService projectionService) : IDecisionCertificationService
{
    public async Task<DecisionCertificationReport> GetCurrentCertificationAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await BuildReportAsync(repository);
    }

    public async Task<DecisionCertificationReport> RunCertificationAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionCertificationReport report = await BuildReportAsync(repository);
        return await decisionRepository.SaveCertificationReportAsync(repository, report);
    }

    public async Task<IReadOnlyList<DecisionCertificationReport>> ListReportsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await decisionRepository.ListCertificationReportsAsync(repository);
    }

    private async Task<DecisionCertificationReport> BuildReportAsync(Repository repository)
    {
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        IReadOnlyList<DecisionCandidate> candidates = await decisionRepository.ListCandidatesAsync(repository);
        IReadOnlyList<DecisionProposal> proposals = await decisionRepository.ListProposalsAsync(repository);
        IReadOnlyList<DecisionGovernanceReport> persistedGovernanceReports =
            await decisionRepository.ListGovernanceReportsAsync(repository);
        IReadOnlyList<DecisionCertificationReport> persistedCertificationReports =
            await decisionRepository.ListCertificationReportsAsync(repository);
        IReadOnlyList<DecisionAssimilationRecommendation> assimilationRecommendations =
            await ListAssimilationRecommendationsAsync(repository, decisions);
        IReadOnlyDictionary<string, IReadOnlyList<DecisionProposalRevision>> revisionsByProposal =
            await ListProposalRevisionsAsync(repository, proposals);
        IReadOnlyDictionary<string, DecisionReviewStatus?> reviewStatusByProposal =
            await ListReviewStatusAsync(repository, proposals);
        IReadOnlyDictionary<string, IReadOnlyList<DecisionReviewNote>> reviewNotesByProposal =
            await ListReviewNotesAsync(repository, proposals);
        DecisionGovernanceReport governanceReport = await governanceService.GetCurrentReportAsync(repository.Id);
        ExecutionDecisionProjection executionProjection = await projectionService.BuildExecutionProjectionAsync(repository.Id);
        var evidence = new List<DecisionCertificationEvidence>();
        var diagnostics = new List<string>();

        AddEvidence(
            evidence,
            "context-resolution",
            "Context resolution",
            true,
            $"Loaded {decisions.Count} decisions, {candidates.Count} candidates, and {proposals.Count} proposals from repository-owned artifacts.",
            [RepositorySource(repository)],
            decisions.Select(decision => decision.Id.Value).ToArray(),
            candidates.Select(candidate => candidate.Id).ToArray(),
            proposals.Select(proposal => proposal.Id).ToArray());
        AddEvidence(
            evidence,
            "discovery",
            "Discovery",
            candidates.All(candidate => candidate.Sources.Count > 0 || candidate.Evidence.Count > 0),
            $"Read {candidates.Count} candidates; candidates without source references or evidence fail discovery certification.",
            candidates.Select(CandidateSource).ToArray(),
            [],
            candidates.Select(candidate => candidate.Id).ToArray(),
            []);
        AddEvidence(
            evidence,
            "candidate-lifecycle",
            "Candidate lifecycle",
            candidates.All(candidate => candidate.History.Count > 0 && candidate.RepositoryId == repository.Id),
            "Every candidate must carry repository ownership and lifecycle history.",
            candidates.Select(CandidateSource).ToArray(),
            [],
            candidates.Select(candidate => candidate.Id).ToArray(),
            []);
        AddEvidence(
            evidence,
            "proposal-generation",
            "Proposal generation",
            proposals.All(proposal =>
                proposal.RepositoryId == repository.Id &&
                !string.IsNullOrWhiteSpace(proposal.CandidateId) &&
                proposal.Options.Count > 0 &&
                proposal.Evidence.Count > 0),
            "Every proposal must link to a candidate and include options plus evidence.",
            proposals.Select(ProposalSource).ToArray(),
            [],
            proposals.Select(proposal => proposal.CandidateId).ToArray(),
            proposals.Select(proposal => proposal.Id).ToArray());
        AddEvidence(
            evidence,
            "proposal-lifecycle",
            "Proposal lifecycle",
            proposals.All(proposal => proposal.History.Count > 0),
            "Every proposal must preserve lifecycle history.",
            proposals.Select(ProposalSource).ToArray(),
            [],
            proposals.Select(proposal => proposal.CandidateId).ToArray(),
            proposals.Select(proposal => proposal.Id).ToArray());
        AddEvidence(
            evidence,
            "review",
            "Review",
            proposals.All(proposal =>
                proposal.State is not DecisionProposalState.Viewed and not DecisionProposalState.NeedsRefinement and not DecisionProposalState.ReadyForResolution ||
                reviewStatusByProposal.GetValueOrDefault(proposal.Id) is not null),
            "Reviewed proposals must have a persisted review status.",
            proposals.Select(ProposalSource).ToArray(),
            [],
            proposals.Select(proposal => proposal.CandidateId).ToArray(),
            proposals.Select(proposal => proposal.Id).ToArray());
        AddEvidence(
            evidence,
            "refinement",
            "Refinement",
            revisionsByProposal.All(pair => pair.Value.All(revision =>
                revision.RepositoryId == repository.Id &&
                string.Equals(revision.ProposalId, pair.Key, StringComparison.Ordinal))),
            $"Read {revisionsByProposal.Sum(pair => pair.Value.Count)} proposal revisions; each revision must belong to its proposal and repository.",
            revisionsByProposal.SelectMany(pair => pair.Value.Select(revision =>
                new DecisionSourceReference("DecisionProposalRevision", $".agents/decisions/proposals/{pair.Key}/revisions/{revision.Id}.json", ProposalId: pair.Key))).ToArray(),
            [],
            [],
            revisionsByProposal.Keys.ToArray());
        AddEvidence(
            evidence,
            "resolution",
            "Resolution",
            decisions.Where(decision => decision.State == DecisionState.Resolved).All(decision =>
                decision.Resolution is not null &&
                !string.IsNullOrWhiteSpace(decision.Resolution.ResolvedBy) &&
                !string.IsNullOrWhiteSpace(decision.Resolution.Rationale)),
            "Resolved decisions must carry explicit human resolution metadata.",
            decisions.Select(DecisionSource).ToArray(),
            decisions.Select(decision => decision.Id.Value).ToArray(),
            [],
            []);
        AddEvidence(
            evidence,
            "governance",
            "Governance",
            governanceReport.Findings.All(finding => !finding.BlocksExecutionProjection),
            $"Current governance health is {governanceReport.Health} with {governanceReport.Summary.BlockingFindingCount} blocking findings.",
            [new DecisionSourceReference("DecisionGovernanceReport", ".agents/decisions/governance/current")],
            governanceReport.Findings.SelectMany(finding => finding.RelatedDecisionIds).ToArray(),
            governanceReport.Findings.SelectMany(finding => finding.RelatedCandidateIds).ToArray(),
            governanceReport.Findings.SelectMany(finding => finding.RelatedProposalIds).ToArray());
        AddEvidence(
            evidence,
            "execution-consumption",
            "Execution consumption",
            executionProjection.Conflicts.Count == 0 &&
            (executionProjection.Constraints.Count + executionProjection.Directives.Count > 0) ==
            decisions.Any(IsAcceptedResolvedDecisionWithoutBlockingFinding),
            $"Execution projection returned {executionProjection.Constraints.Count} constraints, {executionProjection.Directives.Count} directives, and {executionProjection.Conflicts.Count} conflicts.",
            [new DecisionSourceReference("ExecutionDecisionProjection", ".agents/decisions/execution-projection")],
            executionProjection.Constraints.Select(constraint => constraint.DecisionId)
                .Concat(executionProjection.Directives.Select(directive => directive.DecisionId))
                .ToArray(),
            [],
            []);
        AddEvidence(
            evidence,
            "operational-context-assimilation-boundary",
            "Operational-context assimilation boundary",
            assimilationRecommendations.All(recommendation => recommendation.RepositoryId == repository.Id),
            $"Read {assimilationRecommendations.Count} decision-owned assimilation recommendations; certification does not promote operational context.",
            assimilationRecommendations.Select(AssimilationSource).ToArray(),
            assimilationRecommendations.Select(recommendation => recommendation.DecisionId).ToArray(),
            [],
            []);
        AddEvidence(
            evidence,
            "authority-boundaries",
            "Authority boundaries",
            decisions.All(decision =>
                decision.State != DecisionState.Resolved ||
                decision.Resolution is { } resolution &&
                !IsSystemAuthority(resolution.ResolvedBy)),
            "Only explicit non-system resolution actors may establish resolved decision authority.",
            decisions.Select(DecisionSource).ToArray(),
            decisions.Select(decision => decision.Id.Value).ToArray(),
            [],
            []);
        AddEvidence(
            evidence,
            "recovery-after-reload",
            "Recovery after reload",
            string.Equals(
                FingerprintInputs(decisions, candidates, proposals, assimilationRecommendations),
                FingerprintInputs(
                    await decisionRepository.ListDecisionsAsync(repository),
                    await decisionRepository.ListCandidatesAsync(repository),
                    await decisionRepository.ListProposalsAsync(repository),
                    assimilationRecommendations),
                StringComparison.Ordinal),
            "A second repository read produced the same lifecycle fingerprint.",
            [RepositorySource(repository)],
            decisions.Select(decision => decision.Id.Value).ToArray(),
            candidates.Select(candidate => candidate.Id).ToArray(),
            proposals.Select(proposal => proposal.Id).ToArray());
        AddEvidence(
            evidence,
            "artifact-reconstruction",
            "Artifact reconstruction",
            decisions.All(decision => decision.Metadata.RepositoryId == repository.Id) &&
            candidates.All(candidate => candidate.RepositoryId == repository.Id) &&
            proposals.All(proposal => proposal.RepositoryId == repository.Id),
            "Structured lifecycle artifacts reconstruct with repository ownership intact.",
            [RepositorySource(repository)],
            decisions.Select(decision => decision.Id.Value).ToArray(),
            candidates.Select(candidate => candidate.Id).ToArray(),
            proposals.Select(proposal => proposal.Id).ToArray());
        AddEvidence(
            evidence,
            "multi-repository-isolation",
            "Multi-repository isolation",
            AllArtifactsBelongToRepository(repository, decisions, candidates, proposals, assimilationRecommendations, persistedGovernanceReports, persistedCertificationReports),
            "All loaded decision lifecycle artifacts belong to the requested repository id.",
            [RepositorySource(repository)],
            decisions.Select(decision => decision.Id.Value).ToArray(),
            candidates.Select(candidate => candidate.Id).ToArray(),
            proposals.Select(proposal => proposal.Id).ToArray());
        AddEvidence(
            evidence,
            "long-horizon-histories",
            "Long-horizon decision histories",
            decisions.All(decision => decision.History.Count > 0),
            $"Certified {decisions.Count} decisions with {decisions.Sum(decision => decision.History.Count)} total decision history entries.",
            decisions.Select(DecisionSource).ToArray(),
            decisions.Select(decision => decision.Id.Value).ToArray(),
            [],
            []);

        if (decisions.Count is >= 50 and < 100)
        {
            diagnostics.Add("Long-horizon certification reached the 50-decision fixture threshold.");
        }
        else if (decisions.Count is >= 100 and < 200)
        {
            diagnostics.Add("Long-horizon certification reached the 100-decision fixture threshold.");
        }
        else if (decisions.Count >= 200)
        {
            diagnostics.Add("Long-horizon certification reached the 200-decision fixture threshold.");
        }

        int failedEvidenceCount = evidence.Count(item => !item.Passed);
        DecisionLifecycleCertificationResult result = new(
            failedEvidenceCount == 0
                ? DecisionLifecycleCertificationResultKind.Passed
                : DecisionLifecycleCertificationResultKind.Failed,
            evidence.Count(item => item.Passed),
            failedEvidenceCount);
        DecisionHealthAssessment health = failedEvidenceCount > 0
            ? DecisionHealthAssessment.Blocked
            : governanceReport.Health;

        return new DecisionCertificationReport(
            $"certification.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfffffff}",
            repository.Id,
            DateTimeOffset.UtcNow,
            FingerprintInputs(decisions, candidates, proposals, assimilationRecommendations),
            result,
            health,
            evidence.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            governanceReport.Findings,
            diagnostics.Order(StringComparer.Ordinal).ToArray());

        bool IsAcceptedResolvedDecisionWithoutBlockingFinding(Decision decision)
        {
            if (decision.State != DecisionState.Resolved || decision.Resolution?.Outcome != DecisionOutcome.Accepted)
            {
                return false;
            }

            return !governanceReport.Findings
                .Where(finding => finding.BlocksExecutionProjection)
                .SelectMany(finding => finding.RelatedDecisionIds)
                .Contains(decision.Id.Value, StringComparer.Ordinal);
        }
    }

    private async Task<IReadOnlyList<DecisionAssimilationRecommendation>> ListAssimilationRecommendationsAsync(
        Repository repository,
        IReadOnlyList<Decision> decisions)
    {
        var recommendations = new List<DecisionAssimilationRecommendation>();
        foreach (Decision decision in decisions)
        {
            DecisionAssimilationRecommendation? recommendation =
                await decisionRepository.GetAssimilationRecommendationAsync(repository, decision.Id);
            if (recommendation is not null)
            {
                recommendations.Add(recommendation);
            }
        }

        return recommendations.OrderBy(recommendation => recommendation.DecisionId, StringComparer.Ordinal).ToArray();
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<DecisionProposalRevision>>> ListProposalRevisionsAsync(
        Repository repository,
        IReadOnlyList<DecisionProposal> proposals)
    {
        var revisions = new Dictionary<string, IReadOnlyList<DecisionProposalRevision>>(StringComparer.Ordinal);
        foreach (DecisionProposal proposal in proposals)
        {
            revisions[proposal.Id] = await decisionRepository.ListProposalRevisionsAsync(repository, proposal.Id);
        }

        return revisions;
    }

    private async Task<IReadOnlyDictionary<string, DecisionReviewStatus?>> ListReviewStatusAsync(
        Repository repository,
        IReadOnlyList<DecisionProposal> proposals)
    {
        var statuses = new Dictionary<string, DecisionReviewStatus?>(StringComparer.Ordinal);
        foreach (DecisionProposal proposal in proposals)
        {
            statuses[proposal.Id] = await decisionRepository.GetReviewStatusAsync(repository, proposal.Id);
        }

        return statuses;
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<DecisionReviewNote>>> ListReviewNotesAsync(
        Repository repository,
        IReadOnlyList<DecisionProposal> proposals)
    {
        var notes = new Dictionary<string, IReadOnlyList<DecisionReviewNote>>(StringComparer.Ordinal);
        foreach (DecisionProposal proposal in proposals)
        {
            notes[proposal.Id] = await decisionRepository.ListReviewNotesAsync(repository, proposal.Id);
        }

        return notes;
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static bool AllArtifactsBelongToRepository(
        Repository repository,
        IReadOnlyList<Decision> decisions,
        IReadOnlyList<DecisionCandidate> candidates,
        IReadOnlyList<DecisionProposal> proposals,
        IReadOnlyList<DecisionAssimilationRecommendation> assimilationRecommendations,
        IReadOnlyList<DecisionGovernanceReport> governanceReports,
        IReadOnlyList<DecisionCertificationReport> certificationReports)
    {
        return decisions.All(decision => decision.Metadata.RepositoryId == repository.Id) &&
            candidates.All(candidate => candidate.RepositoryId == repository.Id) &&
            proposals.All(proposal => proposal.RepositoryId == repository.Id) &&
            assimilationRecommendations.All(recommendation => recommendation.RepositoryId == repository.Id) &&
            governanceReports.All(report => report.RepositoryId == repository.Id) &&
            certificationReports.All(report => report.RepositoryId == repository.Id);
    }

    private static bool IsAcceptedResolvedDecision(Decision decision)
    {
        return decision.State == DecisionState.Resolved &&
            decision.Resolution?.Outcome == DecisionOutcome.Accepted;
    }

    private static bool IsSystemAuthority(string value)
    {
        return string.Equals(value, "system", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "governance", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "execution", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "certification", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddEvidence(
        List<DecisionCertificationEvidence> evidence,
        string id,
        string area,
        bool passed,
        string detail,
        IReadOnlyList<DecisionSourceReference> sources,
        IReadOnlyList<string> relatedDecisionIds,
        IReadOnlyList<string> relatedCandidateIds,
        IReadOnlyList<string> relatedProposalIds)
    {
        evidence.Add(new DecisionCertificationEvidence(
            id,
            area,
            passed,
            detail,
            sources,
            relatedDecisionIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            relatedCandidateIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            relatedProposalIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
    }

    private static string FingerprintInputs(
        IReadOnlyList<Decision> decisions,
        IReadOnlyList<DecisionCandidate> candidates,
        IReadOnlyList<DecisionProposal> proposals,
        IReadOnlyList<DecisionAssimilationRecommendation> assimilationRecommendations)
    {
        object input = new
        {
            Decisions = decisions.OrderBy(decision => decision.Id.Value, StringComparer.Ordinal),
            Candidates = candidates.OrderBy(candidate => candidate.Id, StringComparer.Ordinal),
            Proposals = proposals.OrderBy(proposal => proposal.Id, StringComparer.Ordinal),
            AssimilationRecommendations = assimilationRecommendations.OrderBy(recommendation => recommendation.DecisionId, StringComparer.Ordinal)
        };
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input, DecisionJson.Options));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static DecisionSourceReference RepositorySource(Repository repository)
    {
        return new DecisionSourceReference("Repository", ".");
    }

    private static DecisionSourceReference DecisionSource(Decision decision)
    {
        return new DecisionSourceReference(
            "DecisionRecord",
            $".agents/decisions/records/{decision.Id.Value}/decision.json",
            DecisionId: decision.Id);
    }

    private static DecisionSourceReference CandidateSource(DecisionCandidate candidate)
    {
        return new DecisionSourceReference(
            "DecisionCandidate",
            $".agents/decisions/candidates/{candidate.Id}/candidate.json",
            CandidateId: candidate.Id);
    }

    private static DecisionSourceReference ProposalSource(DecisionProposal proposal)
    {
        return new DecisionSourceReference(
            "DecisionProposal",
            $".agents/decisions/proposals/{proposal.Id}/proposal.json",
            ProposalId: proposal.Id,
            CandidateId: proposal.CandidateId);
    }

    private static DecisionSourceReference AssimilationSource(DecisionAssimilationRecommendation recommendation)
    {
        return new DecisionSourceReference(
            "DecisionAssimilationRecommendation",
            $".agents/decisions/assimilation/{recommendation.DecisionId}/recommendation.json",
            DecisionId: new DecisionId(recommendation.DecisionId));
    }
}
