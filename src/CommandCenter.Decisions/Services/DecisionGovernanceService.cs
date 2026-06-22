using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Persistence;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionGovernanceService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository) : IDecisionGovernanceService
{
    private static readonly DecisionProposalState[] ActiveProposalStates =
    [
        DecisionProposalState.Draft,
        DecisionProposalState.Generated,
        DecisionProposalState.Viewed,
        DecisionProposalState.NeedsRefinement,
        DecisionProposalState.ReadyForResolution,
        DecisionProposalState.Refined
    ];

    public async Task<DecisionGovernanceReport> GetCurrentReportAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await BuildReportAsync(repository);
    }

    public async Task<DecisionGovernanceReport> GenerateReportAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionGovernanceReport report = await BuildReportAsync(repository);
        return await decisionRepository.SaveGovernanceReportAsync(repository, report);
    }

    public async Task<IReadOnlyList<DecisionGovernanceReport>> ListReportsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await decisionRepository.ListGovernanceReportsAsync(repository);
    }

    private async Task<DecisionGovernanceReport> BuildReportAsync(Repository repository)
    {
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        IReadOnlyList<DecisionCandidate> candidates = await decisionRepository.ListCandidatesAsync(repository);
        IReadOnlyList<DecisionProposal> proposals = await decisionRepository.ListProposalsAsync(repository);
        IReadOnlyList<DecisionAssimilationRecommendation> assimilationRecommendations =
            await ListAssimilationRecommendationsAsync(repository, decisions);
        var findings = new List<DecisionGovernanceFinding>();
        var diagnostics = new List<string>();

        AnalyzeDecisionConsistency(decisions, findings);
        AnalyzeRelationships(decisions, findings);
        AnalyzeResolvedDecisionAuthority(decisions, findings);
        AnalyzeProposalQuality(candidates, proposals, findings);
        AnalyzeExecutionReadiness(decisions, findings);
        AnalyzeAuthorityBoundaries(decisions, proposals, assimilationRecommendations, findings);
        AnalyzeCoverage(candidates, proposals, findings, diagnostics);

        IReadOnlyList<DecisionGovernanceFinding> orderedFindings = findings
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.Category)
            .ThenBy(finding => finding.Id, StringComparer.Ordinal)
            .ToArray();
        int blockingCount = orderedFindings.Count(finding => finding.BlocksExecutionProjection);
        DecisionHealthAssessment health = blockingCount > 0
            ? DecisionHealthAssessment.Blocked
            : orderedFindings.Count == 0
                ? DecisionHealthAssessment.Healthy
                : DecisionHealthAssessment.AdvisoryFindings;
        var summary = new DecisionGovernanceSummary(
            decisions.Count,
            decisions.Count(decision => decision.State == DecisionState.Resolved),
            candidates.Count(candidate => candidate.State is DecisionCandidateState.Discovered or DecisionCandidateState.Promoted),
            proposals.Count(proposal => ActiveProposalStates.Contains(proposal.State)),
            assimilationRecommendations.Count,
            orderedFindings.Count,
            blockingCount);

        return new DecisionGovernanceReport(
            $"governance.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfffffff}",
            repository.Id,
            DateTimeOffset.UtcNow,
            FingerprintInputs(decisions, candidates, proposals, assimilationRecommendations),
            health,
            summary,
            orderedFindings,
            diagnostics.Order(StringComparer.Ordinal).ToArray());
    }

    private static void AnalyzeDecisionConsistency(
        IReadOnlyList<Decision> decisions,
        List<DecisionGovernanceFinding> findings)
    {
        foreach (IGrouping<string, Decision> duplicate in decisions.GroupBy(decision => decision.Id.Value)
                     .Where(group => group.Count() > 1))
        {
            AddFinding(
                findings,
                DecisionGovernanceCategory.Consistency,
                DecisionGovernanceSeverity.Blocking,
                true,
                "Duplicate decision id",
                $"Decision id {duplicate.Key} appears more than once in structured decision records.",
                duplicate.Select(DecisionSource).ToArray(),
                [duplicate.Key],
                [],
                []);
        }

        foreach (Decision decision in decisions.Where(decision => decision.Metadata.RepositoryId == Guid.Empty))
        {
            AddFinding(
                findings,
                DecisionGovernanceCategory.Consistency,
                DecisionGovernanceSeverity.Blocking,
                true,
                "Decision repository metadata is missing",
                $"Decision {decision.Id.Value} does not have repository ownership metadata.",
                [DecisionSource(decision)],
                [decision.Id.Value],
                [],
                []);
        }
    }

    private static void AnalyzeRelationships(
        IReadOnlyList<Decision> decisions,
        List<DecisionGovernanceFinding> findings)
    {
        Dictionary<string, Decision> byId = decisions.ToDictionary(decision => decision.Id.Value, StringComparer.Ordinal);
        foreach (Decision decision in decisions)
        {
            foreach (DecisionRelationship relationship in decision.Relationships)
            {
                if (relationship.SourceDecisionId != decision.Id)
                {
                    AddFinding(
                        findings,
                        DecisionGovernanceCategory.SupersessionLineage,
                        DecisionGovernanceSeverity.Blocking,
                        true,
                        "Relationship source does not match owning decision",
                        $"Decision {decision.Id.Value} contains a {relationship.Type} relationship sourced from {relationship.SourceDecisionId.Value}.",
                        [DecisionSource(decision)],
                        [decision.Id.Value, relationship.SourceDecisionId.Value],
                        [],
                        []);
                }

                if (!byId.ContainsKey(relationship.TargetDecisionId.Value))
                {
                    AddFinding(
                        findings,
                        DecisionGovernanceCategory.DependencyIntegrity,
                        DecisionGovernanceSeverity.Blocking,
                        true,
                        "Relationship target is missing",
                        $"Decision {decision.Id.Value} has a {relationship.Type} relationship to missing decision {relationship.TargetDecisionId.Value}.",
                        [DecisionSource(decision)],
                        [decision.Id.Value, relationship.TargetDecisionId.Value],
                        [],
                        []);
                }

                if (relationship.Type == DecisionRelationshipType.ConflictsWith &&
                    byId.TryGetValue(relationship.TargetDecisionId.Value, out Decision? target) &&
                    decision.State == DecisionState.Resolved &&
                    target.State == DecisionState.Resolved)
                {
                    AddFinding(
                        findings,
                        DecisionGovernanceCategory.Consistency,
                        DecisionGovernanceSeverity.Blocking,
                        true,
                        "Conflicting resolved decisions",
                        $"Resolved decision {decision.Id.Value} conflicts with resolved decision {target.Id.Value}.",
                        [DecisionSource(decision), DecisionSource(target)],
                        [decision.Id.Value, target.Id.Value],
                        [],
                        []);
                }
            }
        }

        foreach (Decision decision in decisions)
        {
            if (HasCircularSupersession(decision.Id.Value, byId, []))
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.SupersessionLineage,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Circular supersession lineage",
                    $"Decision {decision.Id.Value} participates in a circular Supersedes relationship.",
                    [DecisionSource(decision)],
                    [decision.Id.Value],
                    [],
                    []);
            }
        }
    }

    private static void AnalyzeResolvedDecisionAuthority(
        IReadOnlyList<Decision> decisions,
        List<DecisionGovernanceFinding> findings)
    {
        foreach (Decision decision in decisions.Where(decision => decision.State == DecisionState.Resolved))
        {
            if (decision.Resolution is null)
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.AuthorityMetadata,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Resolved decision has no resolution",
                    $"Decision {decision.Id.Value} is Resolved but has no resolution metadata.",
                    [DecisionSource(decision)],
                    [decision.Id.Value],
                    [],
                    []);
                continue;
            }

            if (string.IsNullOrWhiteSpace(decision.Resolution.ResolvedBy) ||
                string.IsNullOrWhiteSpace(decision.Resolution.Rationale) ||
                string.IsNullOrWhiteSpace(decision.Resolution.SelectedOptionId))
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.AuthorityMetadata,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Resolved decision metadata is incomplete",
                    $"Decision {decision.Id.Value} is missing resolver, rationale, or selected option metadata.",
                    [DecisionSource(decision)],
                    [decision.Id.Value],
                    [],
                    []);
            }

            if (decision.Resolution.SourceProposalSnapshot is null)
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.FingerprintIntegrity,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Resolved decision lacks source proposal snapshot",
                    $"Decision {decision.Id.Value} cannot prove the proposal fingerprint used for resolution.",
                    [DecisionSource(decision)],
                    [decision.Id.Value],
                    [],
                    []);
            }
        }
    }

    private static void AnalyzeProposalQuality(
        IReadOnlyList<DecisionCandidate> candidates,
        IReadOnlyList<DecisionProposal> proposals,
        List<DecisionGovernanceFinding> findings)
    {
        HashSet<string> candidateIds = candidates.Select(candidate => candidate.Id).ToHashSet(StringComparer.Ordinal);
        foreach (DecisionProposal proposal in proposals)
        {
            if (!candidateIds.Contains(proposal.CandidateId))
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.DependencyIntegrity,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Proposal source candidate is missing",
                    $"Proposal {proposal.Id} references missing candidate {proposal.CandidateId}.",
                    [ProposalSource(proposal)],
                    [],
                    [proposal.CandidateId],
                    [proposal.Id]);
            }

            if (ActiveProposalStates.Contains(proposal.State) &&
                (proposal.Options.Count == 0 || proposal.Recommendation is null || proposal.Evidence.Count == 0))
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.ProposalQuality,
                    DecisionGovernanceSeverity.Warning,
                    false,
                    "Active proposal is incomplete",
                    $"Proposal {proposal.Id} is active but lacks options, a recommendation, or evidence.",
                    [ProposalSource(proposal)],
                    [],
                    [proposal.CandidateId],
                    [proposal.Id]);
            }
        }
    }

    private static void AnalyzeExecutionReadiness(
        IReadOnlyList<Decision> decisions,
        List<DecisionGovernanceFinding> findings)
    {
        foreach (Decision decision in decisions.Where(decision => decision.State == DecisionState.Resolved && decision.Resolution is not null))
        {
            if (decision.Resolution!.Outcome != DecisionOutcome.Accepted)
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.ExecutionProjectionReadiness,
                    DecisionGovernanceSeverity.Warning,
                    false,
                    "Resolved decision is not accepted",
                    $"Decision {decision.Id.Value} is resolved with outcome {decision.Resolution?.Outcome}; it should not be projected as an execution constraint.",
                    [DecisionSource(decision)],
                    [decision.Id.Value],
                    [],
                    []);
            }
        }
    }

    private static void AnalyzeAuthorityBoundaries(
        IReadOnlyList<Decision> decisions,
        IReadOnlyList<DecisionProposal> proposals,
        IReadOnlyList<DecisionAssimilationRecommendation> assimilationRecommendations,
        List<DecisionGovernanceFinding> findings)
    {
        Dictionary<string, Decision> decisionsById = decisions.ToDictionary(decision => decision.Id.Value, StringComparer.Ordinal);
        Dictionary<string, DecisionProposal> proposalsById = proposals.ToDictionary(proposal => proposal.Id, StringComparer.Ordinal);

        foreach (DecisionAssimilationRecommendation recommendation in assimilationRecommendations)
        {
            if (!decisionsById.TryGetValue(recommendation.DecisionId, out Decision? decision))
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.AuthorityBoundary,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Assimilation recommendation references missing decision",
                    $"Assimilation recommendation for {recommendation.DecisionId} has no source decision record.",
                    [AssimilationSource(recommendation)],
                    [recommendation.DecisionId],
                    [],
                    []);
                continue;
            }

            if (decision.State != DecisionState.Resolved || decision.Resolution?.Outcome != DecisionOutcome.Accepted)
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.AuthorityBoundary,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Assimilation recommendation is not backed by accepted authority",
                    $"Assimilation recommendation for {decision.Id.Value} is backed by state {decision.State} and outcome {decision.Resolution?.Outcome}.",
                    [DecisionSource(decision), AssimilationSource(recommendation)],
                    [decision.Id.Value],
                    [],
                    []);
            }
        }

        foreach (DecisionProposal proposal in proposals.Where(proposal => proposal.State == DecisionProposalState.Resolved))
        {
            bool hasDecision = decisions.Any(decision =>
                string.Equals(decision.Resolution?.SourceProposalSnapshot?.ProposalId, proposal.Id, StringComparison.Ordinal));
            if (!hasDecision)
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.AuthorityBoundary,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Resolved proposal has no authoritative decision",
                    $"Proposal {proposal.Id} is Resolved but no decision records it as the source proposal.",
                    [ProposalSource(proposal)],
                    [],
                    [proposal.CandidateId],
                    [proposal.Id]);
            }
        }

        foreach (Decision decision in decisions.Where(decision => decision.Resolution?.SourceProposalSnapshot is not null))
        {
            string proposalId = decision.Resolution!.SourceProposalSnapshot!.ProposalId;
            if (proposalsById.TryGetValue(proposalId, out DecisionProposal? currentProposal) &&
                currentProposal.State != DecisionProposalState.Resolved)
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.AuthorityBoundary,
                    DecisionGovernanceSeverity.Warning,
                    false,
                    "Decision source proposal is not marked resolved",
                    $"Decision {decision.Id.Value} was resolved from proposal {proposalId}, but the current proposal state is {currentProposal.State}.",
                    [DecisionSource(decision), ProposalSource(currentProposal)],
                    [decision.Id.Value],
                    [currentProposal.CandidateId],
                    [proposalId]);
            }
        }
    }

    private static void AnalyzeCoverage(
        IReadOnlyList<DecisionCandidate> candidates,
        IReadOnlyList<DecisionProposal> proposals,
        List<DecisionGovernanceFinding> findings,
        List<string> diagnostics)
    {
        foreach (DecisionCandidate candidate in candidates.Where(candidate => candidate.State == DecisionCandidateState.Promoted))
        {
            bool hasActiveProposal = proposals.Any(proposal =>
                proposal.CandidateId == candidate.Id && ActiveProposalStates.Contains(proposal.State));
            bool hasResolvedProposal = proposals.Any(proposal =>
                proposal.CandidateId == candidate.Id && proposal.State == DecisionProposalState.Resolved);
            if (!hasActiveProposal && !hasResolvedProposal)
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.DecisionCoverage,
                    DecisionGovernanceSeverity.Warning,
                    false,
                    "Promoted candidate has no proposal",
                    $"Candidate {candidate.Id} is promoted but has no active or resolved proposal.",
                    [CandidateSource(candidate)],
                    [],
                    [candidate.Id],
                    []);
            }
        }

        diagnostics.Add("Repeated ambiguity, repeated blocker, repeated fork, and repeated unresolved-question analysis is limited to structured candidate/proposal evidence in this slice.");
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

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static bool HasCircularSupersession(
        string decisionId,
        IReadOnlyDictionary<string, Decision> decisions,
        HashSet<string> visited)
    {
        if (!visited.Add(decisionId))
        {
            return true;
        }

        if (!decisions.TryGetValue(decisionId, out Decision? decision))
        {
            return false;
        }

        foreach (DecisionRelationship relationship in decision.Relationships
                     .Where(relationship => relationship.Type == DecisionRelationshipType.Supersedes))
        {
            if (HasCircularSupersession(relationship.TargetDecisionId.Value, decisions, [.. visited]))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddFinding(
        List<DecisionGovernanceFinding> findings,
        DecisionGovernanceCategory category,
        DecisionGovernanceSeverity severity,
        bool blocksExecutionProjection,
        string title,
        string detail,
        IReadOnlyList<DecisionSourceReference> sources,
        IReadOnlyList<string> relatedDecisionIds,
        IReadOnlyList<string> relatedCandidateIds,
        IReadOnlyList<string> relatedProposalIds)
    {
        string id = $"GOV-{findings.Count + 1:0000}";
        findings.Add(new DecisionGovernanceFinding(
            id,
            category,
            severity,
            blocksExecutionProjection,
            title,
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
