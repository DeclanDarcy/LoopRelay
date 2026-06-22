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
        AnalyzeActiveAuthority(decisions, findings);
        AnalyzeProposalQuality(candidates, proposals, findings);
        AnalyzeExecutionReadiness(decisions, findings);
        AnalyzeConflictingExecutionDirectives(decisions, findings);
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

                if (byId.TryGetValue(relationship.TargetDecisionId.Value, out Decision? relationshipTarget) &&
                    relationship.Type is DecisionRelationshipType.DependsOn or DecisionRelationshipType.Supports or DecisionRelationshipType.Constrains &&
                    relationshipTarget.State is DecisionState.Archived or DecisionState.Superseded)
                {
                    AddFinding(
                        findings,
                        DecisionGovernanceCategory.AuthorityBoundary,
                        DecisionGovernanceSeverity.Blocking,
                        true,
                        "Relationship references inactive authority",
                        $"Decision {decision.Id.Value} has a {relationship.Type} relationship to {relationshipTarget.State} decision {relationshipTarget.Id.Value}.",
                        [DecisionSource(decision), DecisionSource(relationshipTarget)],
                        [decision.Id.Value, relationshipTarget.Id.Value],
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

        ILookup<string, DecisionRelationship> incomingSupersedes = decisions
            .SelectMany(decision => decision.Relationships)
            .Where(relationship => relationship.Type == DecisionRelationshipType.Supersedes)
            .ToLookup(relationship => relationship.TargetDecisionId.Value, StringComparer.Ordinal);
        foreach (Decision superseded in decisions.Where(decision => decision.State == DecisionState.Superseded))
        {
            DecisionRelationship[] parents = incomingSupersedes[superseded.Id.Value].ToArray();
            if (parents.Length == 0)
            {
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.SupersessionLineage,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Superseded decision has no replacement ancestry",
                    $"Decision {superseded.Id.Value} is Superseded but no active decision records a Supersedes relationship to it.",
                    [DecisionSource(superseded)],
                    [superseded.Id.Value],
                    [],
                    []);
            }
            else if (parents.Length > 1)
            {
                Decision[] parentDecisions = parents
                    .Select(parent => byId.TryGetValue(parent.SourceDecisionId.Value, out Decision? source) ? source : null)
                    .OfType<Decision>()
                    .ToArray();
                AddFinding(
                    findings,
                    DecisionGovernanceCategory.SupersessionLineage,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Superseded decision has multiple replacement parents",
                    $"Decision {superseded.Id.Value} is superseded by multiple decisions: {string.Join(", ", parents.Select(parent => parent.SourceDecisionId.Value).Order(StringComparer.Ordinal))}.",
                    parentDecisions.Select(DecisionSource).Prepend(DecisionSource(superseded)).ToArray(),
                    parents.Select(parent => parent.SourceDecisionId.Value).Append(superseded.Id.Value).ToArray(),
                    [],
                    []);
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
            else
            {
                AnalyzeResolvedDecisionSnapshot(decision, findings);
            }
        }
    }

    private static void AnalyzeResolvedDecisionSnapshot(
        Decision decision,
        List<DecisionGovernanceFinding> findings)
    {
        DecisionResolvedProposalSnapshot snapshot = decision.Resolution!.SourceProposalSnapshot!;
        bool incomplete = string.IsNullOrWhiteSpace(snapshot.ProposalId) ||
            string.IsNullOrWhiteSpace(snapshot.CandidateId) ||
            string.IsNullOrWhiteSpace(snapshot.ProposalFingerprint) ||
            string.IsNullOrWhiteSpace(snapshot.Title) ||
            string.IsNullOrWhiteSpace(snapshot.Context) ||
            snapshot.Options.Count == 0 ||
            snapshot.Evidence.Count == 0 ||
            !snapshot.Options.Any(option => string.Equals(option.Id, decision.Resolution.SelectedOptionId, StringComparison.Ordinal));
        if (incomplete)
        {
            AddFinding(
                findings,
                DecisionGovernanceCategory.FingerprintIntegrity,
                DecisionGovernanceSeverity.Blocking,
                true,
                "Resolved decision source proposal snapshot is incomplete",
                $"Decision {decision.Id.Value} has a source proposal snapshot missing required content or the selected option {decision.Resolution.SelectedOptionId}.",
                [DecisionSource(decision)],
                [decision.Id.Value],
                [snapshot.CandidateId],
                [snapshot.ProposalId]);
            return;
        }

        string actualFingerprint = Fingerprint(new DecisionProposal(
            snapshot.ProposalId,
            decision.Metadata.RepositoryId,
            snapshot.CandidateId,
            snapshot.ProposalState,
            snapshot.Title,
            snapshot.Context,
            snapshot.Options,
            snapshot.Tradeoffs,
            snapshot.Recommendation,
            snapshot.Assumptions,
            snapshot.Evidence,
            snapshot.History));
        if (!string.Equals(snapshot.ProposalFingerprint, actualFingerprint, StringComparison.Ordinal))
        {
            AddFinding(
                findings,
                DecisionGovernanceCategory.FingerprintIntegrity,
                DecisionGovernanceSeverity.Blocking,
                true,
                "Resolved decision source proposal fingerprint is invalid",
                $"Decision {decision.Id.Value} records source proposal fingerprint {snapshot.ProposalFingerprint}, but the resolved snapshot hashes to {actualFingerprint}.",
                [DecisionSource(decision)],
                [decision.Id.Value],
                [snapshot.CandidateId],
                [snapshot.ProposalId]);
        }
    }

    private static void AnalyzeActiveAuthority(
        IReadOnlyList<Decision> decisions,
        List<DecisionGovernanceFinding> findings)
    {
        Decision[] activeAuthorities = decisions
            .Where(decision => decision.State == DecisionState.Resolved && decision.Resolution?.Outcome == DecisionOutcome.Accepted)
            .ToArray();
        foreach (IGrouping<string, Decision> duplicateCandidate in activeAuthorities
                     .Where(decision => !string.IsNullOrWhiteSpace(decision.Resolution?.SourceProposalSnapshot?.CandidateId))
                     .GroupBy(decision => decision.Resolution!.SourceProposalSnapshot!.CandidateId, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            Decision[] authorities = duplicateCandidate.ToArray();
            AddFinding(
                findings,
                DecisionGovernanceCategory.AuthorityBoundary,
                DecisionGovernanceSeverity.Blocking,
                true,
                "Multiple active authorities for one candidate",
                $"Candidate {duplicateCandidate.Key} has multiple accepted resolved decisions: {string.Join(", ", authorities.Select(decision => decision.Id.Value).Order(StringComparer.Ordinal))}.",
                authorities.Select(DecisionSource).ToArray(),
                authorities.Select(decision => decision.Id.Value).ToArray(),
                [duplicateCandidate.Key],
                authorities
                    .Select(decision => decision.Resolution?.SourceProposalSnapshot?.ProposalId)
                    .OfType<string>()
                    .ToArray());
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

    private static void AnalyzeConflictingExecutionDirectives(
        IReadOnlyList<Decision> decisions,
        List<DecisionGovernanceFinding> findings)
    {
        ProjectedDecisionDirective[] directives = decisions
            .Where(decision => decision.State == DecisionState.Resolved &&
                decision.Resolution?.Outcome == DecisionOutcome.Accepted &&
                decision.Resolution.SourceProposalSnapshot is not null)
            .Select(TryProjectExecutionDirective)
            .OfType<ProjectedDecisionDirective>()
            .ToArray();

        foreach (IGrouping<string, ProjectedDecisionDirective> directiveGroup in directives
                     .GroupBy(directive => directive.Subject, StringComparer.Ordinal)
                     .Where(group => group.Any(directive => directive.IsPositive) &&
                         group.Any(directive => !directive.IsPositive)))
        {
            ProjectedDecisionDirective[] conflicting = directiveGroup.ToArray();
            AddFinding(
                findings,
                DecisionGovernanceCategory.ExecutionProjectionReadiness,
                DecisionGovernanceSeverity.Blocking,
                true,
                "Conflicting execution directives",
                $"Accepted resolved decisions project contradictory execution directives for '{directiveGroup.Key}': {string.Join("; ", conflicting.Select(directive => $"{directive.Decision.Id.Value}: {directive.Statement}").Order(StringComparer.Ordinal))}.",
                conflicting.Select(directive => DecisionSource(directive.Decision)).ToArray(),
                conflicting.Select(directive => directive.Decision.Id.Value).ToArray(),
                conflicting.Select(directive => directive.Decision.Resolution?.SourceProposalSnapshot?.CandidateId).OfType<string>().ToArray(),
                conflicting.Select(directive => directive.Decision.Resolution?.SourceProposalSnapshot?.ProposalId).OfType<string>().ToArray());
        }
    }

    private static ProjectedDecisionDirective? TryProjectExecutionDirective(Decision decision)
    {
        DecisionResolution resolution = decision.Resolution!;
        DecisionResolvedProposalSnapshot snapshot = resolution.SourceProposalSnapshot!;
        DecisionOption? selectedOption = snapshot.Options
            .FirstOrDefault(option => string.Equals(option.Id, resolution.SelectedOptionId, StringComparison.Ordinal));
        if (selectedOption is null)
        {
            return null;
        }

        return TryParseDirective(selectedOption.Title, decision) ??
            TryParseDirective(selectedOption.Description, decision) ??
            TryParseDirective(decision.Title, decision);
    }

    private static ProjectedDecisionDirective? TryParseDirective(string statement, Decision decision)
    {
        string normalized = NormalizeDirectiveText(statement);
        if (normalized.Length == 0)
        {
            return null;
        }

        foreach ((string Prefix, bool IsPositive) directive in DirectivePrefixes)
        {
            if (!normalized.StartsWith(directive.Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string subject = normalized[directive.Prefix.Length..].Trim();
            if (subject.Length == 0)
            {
                return null;
            }

            return new ProjectedDecisionDirective(
                subject,
                directive.IsPositive,
                statement.Trim(),
                decision);
        }

        return null;
    }

    private static string NormalizeDirectiveText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        bool previousWasSpace = true;
        foreach (char character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
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

    private static string Fingerprint(DecisionProposal proposal)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(proposal, DecisionJson.Options));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static readonly (string Prefix, bool IsPositive)[] DirectivePrefixes =
    [
        ("do not use ", false),
        ("do not enable ", false),
        ("do not allow ", false),
        ("disable ", false),
        ("avoid ", false),
        ("exclude ", false),
        ("forbid ", false),
        ("reject ", false),
        ("remove ", false),
        ("prevent ", false),
        ("use ", true),
        ("enable ", true),
        ("adopt ", true),
        ("include ", true),
        ("allow ", true),
        ("require ", true),
        ("keep ", true),
        ("preserve ", true)
    ];

    private sealed record ProjectedDecisionDirective(
        string Subject,
        bool IsPositive,
        string Statement,
        Decision Decision);

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
