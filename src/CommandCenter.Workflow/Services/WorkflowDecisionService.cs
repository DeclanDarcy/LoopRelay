using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowDecisionService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository) : IWorkflowDecisionService
{
    public async Task<WorkflowDecisionProjection> ProjectDecisionAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        IReadOnlyList<DecisionCandidate> candidates = await decisionRepository.ListCandidatesAsync(repository);
        IReadOnlyList<DecisionProposal> proposals = await decisionRepository.ListProposalsAsync(repository);
        IReadOnlyList<DecisionGovernanceReport> governanceReports = await decisionRepository.ListGovernanceReportsAsync(repository);
        IReadOnlyList<DecisionQualityAssessment> qualityAssessments = await decisionRepository.ListQualityAssessmentsAsync(repository);
        IReadOnlyList<DecisionCertificationReport> certificationReports = await decisionRepository.ListCertificationReportsAsync(repository);

        IReadOnlyList<DecisionPackageVersion> packageVersions = await LoadPackageVersionsAsync(repository, proposals);
        Decision? selectedDecision = SelectCurrentDecision(decisions);
        DecisionCandidate? selectedCandidate = SelectCurrentCandidate(candidates, selectedDecision);
        DecisionProposal? selectedProposal = SelectCurrentProposal(proposals, selectedDecision, selectedCandidate);
        DecisionPackageVersion? selectedPackage = SelectCurrentPackage(packageVersions, selectedProposal, selectedCandidate);
        DecisionGovernanceReport? governance = governanceReports.OrderByDescending(report => report.GeneratedAt).FirstOrDefault();
        DecisionQualityAssessment? quality = SelectQualityAssessment(qualityAssessments, selectedDecision);
        DecisionCertificationReport? certification = certificationReports.OrderByDescending(report => report.GeneratedAt).FirstOrDefault();

        string? replacementDecisionId = selectedDecision is null
            ? null
            : FindReplacementDecisionId(decisions, selectedDecision.Id.Value);

        WorkflowDecisionStatus status = DetermineStatus(selectedDecision, selectedProposal, selectedCandidate, replacementDecisionId);
        bool governanceBlocked = governance?.Health is DecisionHealthAssessment.Blocked ||
            governance?.Findings.Any(finding => finding.BlocksExecutionProjection || finding.Severity is DecisionGovernanceSeverity.Blocking) == true;
        bool resolutionEligible = status is WorkflowDecisionStatus.Missing or WorkflowDecisionStatus.Resolved or WorkflowDecisionStatus.Archived;
        if (status is WorkflowDecisionStatus.Superseded && replacementDecisionId is not null)
        {
            Decision? replacement = decisions.FirstOrDefault(decision => decision.Id.Value == replacementDecisionId);
            resolutionEligible = replacement?.State is DecisionState.Resolved or DecisionState.Archived;
        }

        List<string> reasoning = BuildReasoning(status, selectedDecision, selectedCandidate, selectedProposal, replacementDecisionId);
        List<string> governanceSignals = BuildGovernanceSignals(governance);
        List<string> qualitySignals = BuildQualitySignals(quality);
        List<string> certificationSignals = BuildCertificationSignals(certification);
        List<string> supersessionSignals = BuildSupersessionSignals(decisions);
        List<string> conflicts = [];

        if (status is WorkflowDecisionStatus.Superseded && replacementDecisionId is null)
        {
            conflicts.Add($"Decision {selectedDecision?.Id.Value} is superseded without replacement lineage.");
        }

        var diagnostics = new WorkflowDecisionDiagnostics(
            repository.Id,
            BuildInputs(decisions, candidates, proposals, packageVersions, governance, quality, certification),
            reasoning,
            governanceSignals,
            qualitySignals,
            certificationSignals,
            supersessionSignals,
            conflicts);

        return new WorkflowDecisionProjection(
            repository.Id,
            selectedDecision?.Id.Value,
            selectedProposal?.CandidateId ?? selectedCandidate?.Id,
            selectedCandidate?.State.ToString(),
            selectedProposal?.Id,
            selectedPackage?.Id,
            governanceBlocked ? WorkflowDecisionStatus.AwaitingResolution : status,
            selectedProposal?.State.ToString(),
            selectedDecision?.State.ToString(),
            quality?.HumanAuthoringBurdenSignals
                .OrderByDescending(signal => signal.Burden)
                .FirstOrDefault()
                ?.Burden.ToString() ?? HumanAuthoringBurden.Unknown.ToString(),
            selectedDecision?.Metadata.CreatedAt ?? selectedCandidate?.History.OrderBy(entry => entry.Timestamp).FirstOrDefault()?.Timestamp,
            selectedDecision?.Resolution?.ResolvedAt,
            resolutionEligible && !governanceBlocked,
            governanceBlocked,
            governance?.Health.ToString(),
            quality is null ? null : $"{quality.Rating}:{quality.Score}",
            certification?.Result.Kind.ToString(),
            replacementDecisionId,
            diagnostics);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(candidate => candidate.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<IReadOnlyList<DecisionPackageVersion>> LoadPackageVersionsAsync(
        Repository repository,
        IReadOnlyList<DecisionProposal> proposals)
    {
        List<DecisionPackageVersion> packageVersions = [];
        foreach (DecisionProposal proposal in proposals)
        {
            packageVersions.AddRange(await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id));
        }

        return packageVersions;
    }

    private static Decision? SelectCurrentDecision(IReadOnlyList<Decision> decisions) =>
        decisions
            .OrderBy(decision => decision.State is DecisionState.Open or DecisionState.UnderReview ? 0 : 1)
            .ThenBy(decision => decision.State is DecisionState.Superseded ? 1 : 0)
            .ThenByDescending(decision => decision.Metadata.UpdatedAt)
            .ThenBy(decision => decision.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault();

    private static DecisionCandidate? SelectCurrentCandidate(
        IReadOnlyList<DecisionCandidate> candidates,
        Decision? decision) =>
        candidates
            .Where(candidate => decision is null || string.Equals(candidate.Title, decision.Title, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.History.OrderBy(entry => entry.Timestamp).LastOrDefault()?.Timestamp)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .FirstOrDefault() ??
        candidates
            .OrderByDescending(candidate => candidate.History.OrderBy(entry => entry.Timestamp).LastOrDefault()?.Timestamp)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .FirstOrDefault();

    private static DecisionProposal? SelectCurrentProposal(
        IReadOnlyList<DecisionProposal> proposals,
        Decision? decision,
        DecisionCandidate? candidate) =>
        proposals
            .Where(proposal =>
                candidate is null || string.Equals(proposal.CandidateId, candidate.Id, StringComparison.Ordinal) ||
                (decision is not null && string.Equals(proposal.Title, decision.Title, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(proposal => proposal.State is DecisionProposalState.ReadyForResolution or DecisionProposalState.NeedsRefinement ? 0 : 1)
            .ThenByDescending(proposal => proposal.History.OrderBy(entry => entry.Timestamp).LastOrDefault()?.Timestamp)
            .ThenBy(proposal => proposal.Id, StringComparer.Ordinal)
            .FirstOrDefault() ??
        proposals
            .OrderByDescending(proposal => proposal.History.OrderBy(entry => entry.Timestamp).LastOrDefault()?.Timestamp)
            .ThenBy(proposal => proposal.Id, StringComparer.Ordinal)
            .FirstOrDefault();

    private static DecisionPackageVersion? SelectCurrentPackage(
        IReadOnlyList<DecisionPackageVersion> packageVersions,
        DecisionProposal? proposal,
        DecisionCandidate? candidate) =>
        packageVersions
            .Where(package =>
                (proposal is not null && string.Equals(package.ProposalId, proposal.Id, StringComparison.Ordinal)) ||
                (candidate is not null && string.Equals(package.CandidateId, candidate.Id, StringComparison.Ordinal)))
            .OrderByDescending(package => package.CreatedAt)
            .ThenBy(package => package.Id, StringComparer.Ordinal)
            .FirstOrDefault();

    private static DecisionQualityAssessment? SelectQualityAssessment(
        IReadOnlyList<DecisionQualityAssessment> assessments,
        Decision? decision) =>
        assessments
            .Where(assessment => decision is null || string.Equals(assessment.DecisionId, decision.Id.Value, StringComparison.Ordinal))
            .OrderByDescending(assessment => assessment.AssessedAt)
            .ThenBy(assessment => assessment.Id, StringComparer.Ordinal)
            .FirstOrDefault();

    private static WorkflowDecisionStatus DetermineStatus(
        Decision? decision,
        DecisionProposal? proposal,
        DecisionCandidate? candidate,
        string? replacementDecisionId)
    {
        if (decision is not null)
        {
            return decision.State switch
            {
                DecisionState.Open => WorkflowDecisionStatus.AwaitingResolution,
                DecisionState.UnderReview => WorkflowDecisionStatus.UnderReview,
                DecisionState.Resolved => WorkflowDecisionStatus.Resolved,
                DecisionState.Archived => WorkflowDecisionStatus.Archived,
                DecisionState.Superseded => replacementDecisionId is null
                    ? WorkflowDecisionStatus.Superseded
                    : WorkflowDecisionStatus.Resolved,
                _ => WorkflowDecisionStatus.AwaitingResolution
            };
        }

        if (proposal is not null)
        {
            return proposal.State switch
            {
                DecisionProposalState.Generated => WorkflowDecisionStatus.Generated,
                DecisionProposalState.Viewed or DecisionProposalState.NeedsRefinement or DecisionProposalState.Refined => WorkflowDecisionStatus.UnderReview,
                DecisionProposalState.ReadyForResolution => WorkflowDecisionStatus.AwaitingResolution,
                DecisionProposalState.Resolved => WorkflowDecisionStatus.Resolved,
                _ => WorkflowDecisionStatus.Generated
            };
        }

        return candidate is null ? WorkflowDecisionStatus.Missing : WorkflowDecisionStatus.Discovered;
    }

    private static string? FindReplacementDecisionId(IReadOnlyList<Decision> decisions, string supersededDecisionId) =>
        decisions
            .SelectMany(decision => decision.Relationships
                .Where(relationship =>
                    relationship.Type is DecisionRelationshipType.Supersedes &&
                    relationship.TargetDecisionId.Value == supersededDecisionId)
                .Select(_ => decision.Id.Value))
            .OrderBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault();

    private static List<string> BuildReasoning(
        WorkflowDecisionStatus status,
        Decision? decision,
        DecisionCandidate? candidate,
        DecisionProposal? proposal,
        string? replacementDecisionId)
    {
        List<string> reasoning = [];
        if (decision is not null)
        {
            reasoning.Add($"Decision {decision.Id.Value} is {decision.State}.");
        }
        else if (proposal is not null)
        {
            reasoning.Add($"Decision proposal {proposal.Id} is {proposal.State}.");
        }
        else if (candidate is not null)
        {
            reasoning.Add($"Decision candidate {candidate.Id} is {candidate.State}.");
        }
        else
        {
            reasoning.Add("No decision evidence exists.");
        }

        if (replacementDecisionId is not null)
        {
            reasoning.Add($"Superseded decision follows replacement authority {replacementDecisionId}.");
        }

        reasoning.Add($"Workflow decision status is {status}.");
        return reasoning;
    }

    private static List<string> BuildGovernanceSignals(DecisionGovernanceReport? report)
    {
        if (report is null)
        {
            return ["No decision governance report exists."];
        }

        List<string> signals =
        [
            $"Governance health is {report.Health}.",
            $"Governance findings: {report.Findings.Count}."
        ];
        signals.AddRange(report.Findings
            .Where(finding => finding.Severity is DecisionGovernanceSeverity.Warning or DecisionGovernanceSeverity.Blocking || finding.BlocksExecutionProjection)
            .Select(finding => $"{finding.Severity}: {finding.Title}"));
        return signals;
    }

    private static List<string> BuildQualitySignals(DecisionQualityAssessment? assessment)
    {
        if (assessment is null)
        {
            return ["No decision quality assessment exists."];
        }

        List<string> signals =
        [
            $"Quality rating is {assessment.Rating} with score {assessment.Score}.",
            $"Human authoring burden signals: {assessment.HumanAuthoringBurdenSignals.Count}."
        ];
        signals.AddRange(assessment.Signals
            .Where(signal => signal.Category.Contains("recommendation", StringComparison.OrdinalIgnoreCase) ||
                signal.Category.Contains("tradeoff", StringComparison.OrdinalIgnoreCase) ||
                signal.Category.Contains("context", StringComparison.OrdinalIgnoreCase) ||
                signal.Category.Contains("constraint", StringComparison.OrdinalIgnoreCase))
            .Select(signal => $"{signal.Category}: {signal.Severity}: {signal.Summary}"));
        return signals;
    }

    private static List<string> BuildCertificationSignals(DecisionCertificationReport? report)
    {
        if (report is null)
        {
            return ["No decision certification report exists."];
        }

        return
        [
            $"Certification result is {report.Result.Kind}.",
            $"Certification health is {report.Health}.",
            $"Certification evidence passed={report.Result.PassedEvidenceCount}, failed={report.Result.FailedEvidenceCount}."
        ];
    }

    private static List<string> BuildSupersessionSignals(IReadOnlyList<Decision> decisions)
    {
        Decision[] supersededDecisions = decisions
            .Where(decision => decision.State is DecisionState.Superseded)
            .OrderBy(decision => decision.Id.Value, StringComparer.Ordinal)
            .ToArray();
        if (supersededDecisions.Length == 0)
        {
            return [];
        }

        return supersededDecisions
            .Select(decision =>
            {
                string? replacementDecisionId = FindReplacementDecisionId(decisions, decision.Id.Value);
                return replacementDecisionId is null
                    ? $"Decision {decision.Id.Value} is superseded without replacement authority."
                    : $"Decision {decision.Id.Value} is superseded by {replacementDecisionId}.";
            })
            .ToArray()
            .ToList();
    }

    private static IReadOnlyList<string> BuildInputs(
        IReadOnlyList<Decision> decisions,
        IReadOnlyList<DecisionCandidate> candidates,
        IReadOnlyList<DecisionProposal> proposals,
        IReadOnlyList<DecisionPackageVersion> packageVersions,
        DecisionGovernanceReport? governance,
        DecisionQualityAssessment? quality,
        DecisionCertificationReport? certification) =>
        [
            $"decisions:{decisions.Count}:{string.Join(",", decisions.OrderBy(decision => decision.Id.Value).Select(decision => $"{decision.Id.Value}:{decision.State}"))}",
            $"decision-candidates:{candidates.Count}:{string.Join(",", candidates.OrderBy(candidate => candidate.Id).Select(candidate => $"{candidate.Id}:{candidate.State}"))}",
            $"decision-proposals:{proposals.Count}:{string.Join(",", proposals.OrderBy(proposal => proposal.Id).Select(proposal => $"{proposal.Id}:{proposal.State}"))}",
            $"decision-packages:{packageVersions.Count}:{string.Join(",", packageVersions.OrderBy(package => package.Id).Select(package => $"{package.Id}:{package.ProposalId}"))}",
            $"decision-governance:{governance?.Id ?? "none"}:{governance?.Health.ToString() ?? "none"}",
            $"decision-quality:{quality?.Id ?? "none"}:{quality?.Rating.ToString() ?? "none"}",
            $"decision-certification:{certification?.Id ?? "none"}:{certification?.Result.Kind.ToString() ?? "none"}"
        ];
}
