using CommandCenter.Continuity.Abstractions;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowOperationalContextService(
    IRepositoryService repositoryService,
    IOperationalContextProposalStore proposalStore,
    IDecisionRepository decisionRepository) : IWorkflowOperationalContextService
{
    public async Task<WorkflowOperationalContextProjection> ProjectOperationalContextAsync(
        Guid repositoryId,
        WorkflowDecisionProjection decision,
        WorkflowExecutionProjection execution)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<OperationalContextProposal> proposals = await proposalStore.ListAsync(repository);
        OperationalContextProposal? selectedProposal = SelectCurrentProposal(proposals);
        DecisionAssimilationRecommendation? assimilation = await LoadAssimilationRecommendationAsync(repository, decision);

        WorkflowOperationalContextStatus status = DetermineStatus(selectedProposal);
        string? sourceDecisionId = DetermineSourceDecisionId(selectedProposal, assimilation, decision);
        string? sourceExecutionId = DetermineSourceExecutionId(selectedProposal, execution);
        bool reviewEligible = status is WorkflowOperationalContextStatus.Edited or
            WorkflowOperationalContextStatus.ReadyForPromotion or
            WorkflowOperationalContextStatus.Rejected or
            WorkflowOperationalContextStatus.Promoted or
            WorkflowOperationalContextStatus.NoContextRequired;
        bool promotionEligible = status is WorkflowOperationalContextStatus.Promoted or
            WorkflowOperationalContextStatus.Rejected or
            WorkflowOperationalContextStatus.NoContextRequired;
        bool commitEligible = reviewEligible && promotionEligible;

        List<string> reasoning = BuildReasoning(status, selectedProposal);
        List<string> reviewSignals = BuildReviewSignals(selectedProposal, status);
        List<string> promotionSignals = BuildPromotionSignals(selectedProposal, status);
        List<string> linkageSignals = BuildLinkageSignals(selectedProposal, assimilation, sourceDecisionId, sourceExecutionId);
        List<string> conflicts = BuildConflicts(selectedProposal, status);

        var diagnostics = new WorkflowOperationalContextDiagnostics(
            repository.Id,
            BuildInputs(proposals, assimilation, execution),
            reasoning,
            reviewSignals,
            promotionSignals,
            linkageSignals,
            conflicts);

        return new WorkflowOperationalContextProjection(
            repository.Id,
            selectedProposal?.ProposalId,
            status,
            selectedProposal?.Review.ReviewState.ToString(),
            selectedProposal is null
                ? null
                : selectedProposal.Promotion.PromotedAt is null ? "PendingPromotion" : "Promoted",
            selectedProposal?.GeneratedAt,
            selectedProposal?.Review.ReviewedAt,
            selectedProposal?.Promotion.PromotedAt,
            null,
            BuildSummary(status, selectedProposal),
            sourceDecisionId,
            sourceExecutionId,
            reviewEligible,
            promotionEligible,
            commitEligible,
            diagnostics);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(candidate => candidate.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<DecisionAssimilationRecommendation?> LoadAssimilationRecommendationAsync(
        Repository repository,
        WorkflowDecisionProjection decision)
    {
        if (decision.DecisionId is null)
        {
            return null;
        }

        return await decisionRepository.GetAssimilationRecommendationAsync(repository, new DecisionId(decision.DecisionId));
    }

    private static OperationalContextProposal? SelectCurrentProposal(IReadOnlyList<OperationalContextProposal> proposals) =>
        proposals
            .OrderBy(proposal => proposal.Status switch
            {
                OperationalContextProposalStatus.Pending => 0,
                OperationalContextProposalStatus.Edited => 1,
                OperationalContextProposalStatus.Accepted => 2,
                OperationalContextProposalStatus.Promoted => 3,
                OperationalContextProposalStatus.Rejected => 4,
                OperationalContextProposalStatus.Superseded => 5,
                _ => 6
            })
            .ThenByDescending(proposal => proposal.Promotion.PromotedAt ?? proposal.Review.ReviewedAt ?? proposal.GeneratedAt)
            .ThenBy(proposal => proposal.ProposalId, StringComparer.Ordinal)
            .FirstOrDefault();

    private static WorkflowOperationalContextStatus DetermineStatus(OperationalContextProposal? proposal)
    {
        if (proposal is null)
        {
            return WorkflowOperationalContextStatus.NoContextRequired;
        }

        return proposal.Status switch
        {
            OperationalContextProposalStatus.Pending => proposal.Review.ReviewState is OperationalContextReviewState.PendingReview or OperationalContextReviewState.Stale
                ? WorkflowOperationalContextStatus.UnderReview
                : WorkflowOperationalContextStatus.Proposed,
            OperationalContextProposalStatus.Edited => WorkflowOperationalContextStatus.Edited,
            OperationalContextProposalStatus.Accepted => WorkflowOperationalContextStatus.ReadyForPromotion,
            OperationalContextProposalStatus.Rejected => WorkflowOperationalContextStatus.Rejected,
            OperationalContextProposalStatus.Promoted => WorkflowOperationalContextStatus.Promoted,
            OperationalContextProposalStatus.Superseded => WorkflowOperationalContextStatus.Archived,
            _ => WorkflowOperationalContextStatus.Missing
        };
    }

    private static string? DetermineSourceDecisionId(
        OperationalContextProposal? proposal,
        DecisionAssimilationRecommendation? assimilation,
        WorkflowDecisionProjection decision)
    {
        if (proposal is null || assimilation is null)
        {
            return null;
        }

        if (proposal.GeneratedAt >= assimilation.CreatedAt &&
            (MatchesFingerprint(proposal, assimilation.DecisionFingerprint) ||
             MatchesFingerprint(proposal, assimilation.ContextFingerprint) ||
             MatchesText(proposal, assimilation.DecisionId) ||
             string.Equals(decision.DecisionId, assimilation.DecisionId, StringComparison.Ordinal)))
        {
            return assimilation.DecisionId;
        }

        return null;
    }

    private static string? DetermineSourceExecutionId(
        OperationalContextProposal? proposal,
        WorkflowExecutionProjection execution)
    {
        string? executionId = execution.ExecutionId?.ToString();
        if (proposal is null || executionId is null)
        {
            return null;
        }

        if (MatchesText(proposal, executionId) ||
            (execution.CompletedAt is DateTimeOffset completedAt && proposal.GeneratedAt >= completedAt))
        {
            return executionId;
        }

        return null;
    }

    private static bool MatchesFingerprint(OperationalContextProposal proposal, string fingerprint) =>
        !string.IsNullOrWhiteSpace(fingerprint) &&
        proposal.InputFingerprints.Any(input => string.Equals(input.Hash, fingerprint, StringComparison.Ordinal));

    private static bool MatchesText(OperationalContextProposal proposal, string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (proposal.InputFingerprints.Any(input =>
            input.Name.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            input.RelativePath.Contains(value, StringComparison.OrdinalIgnoreCase)) ||
         proposal.SemanticChanges.Any(change =>
            (change.ItemId?.Contains(value, StringComparison.OrdinalIgnoreCase) == true) ||
            change.Description.Contains(value, StringComparison.OrdinalIgnoreCase)));

    private static List<string> BuildReasoning(WorkflowOperationalContextStatus status, OperationalContextProposal? proposal)
    {
        if (proposal is null)
        {
            return ["No operational-context proposal exists; Continuity evidence indicates no context update is required."];
        }

        return
        [
            $"Operational-context proposal {proposal.ProposalId} is {proposal.Status}.",
            $"Workflow operational-context status is {status}."
        ];
    }

    private static List<string> BuildReviewSignals(
        OperationalContextProposal? proposal,
        WorkflowOperationalContextStatus status)
    {
        if (proposal is null)
        {
            return ["No proposal review is required."];
        }

        List<string> signals =
        [
            $"Review state is {proposal.Review.ReviewState}.",
            $"Reviewed at: {proposal.Review.ReviewedAt?.ToString("O") ?? "none"}."
        ];

        if (status is WorkflowOperationalContextStatus.UnderReview or WorkflowOperationalContextStatus.Proposed)
        {
            signals.Add("Continuity proposal is awaiting human review.");
        }

        if (!string.IsNullOrWhiteSpace(proposal.Review.StaleReason))
        {
            signals.Add($"Review stale reason: {proposal.Review.StaleReason}");
        }

        return signals;
    }

    private static List<string> BuildPromotionSignals(
        OperationalContextProposal? proposal,
        WorkflowOperationalContextStatus status)
    {
        if (proposal is null)
        {
            return ["No proposal promotion is required."];
        }

        List<string> signals =
        [
            $"Promoted at: {proposal.Promotion.PromotedAt?.ToString("O") ?? "none"}."
        ];

        if (status is WorkflowOperationalContextStatus.ReadyForPromotion or WorkflowOperationalContextStatus.Edited)
        {
            signals.Add("Continuity proposal is reviewed and awaiting human promotion.");
        }

        if (!string.IsNullOrWhiteSpace(proposal.Promotion.ArchiveFailureReason))
        {
            signals.Add($"Archive failure: {proposal.Promotion.ArchiveFailureReason}");
        }

        if (!string.IsNullOrWhiteSpace(proposal.Promotion.WriteFailureReason))
        {
            signals.Add($"Write failure: {proposal.Promotion.WriteFailureReason}");
        }

        return signals;
    }

    private static List<string> BuildLinkageSignals(
        OperationalContextProposal? proposal,
        DecisionAssimilationRecommendation? assimilation,
        string? sourceDecisionId,
        string? sourceExecutionId)
    {
        List<string> signals = [];
        if (proposal is null)
        {
            signals.Add("No proposal exists to link to decision or execution evidence.");
            return signals;
        }

        signals.Add(sourceDecisionId is null
            ? "No decision-to-context linkage evidence was found."
            : $"Proposal {proposal.ProposalId} links to decision {sourceDecisionId}.");
        signals.Add(sourceExecutionId is null
            ? "No execution-to-context linkage evidence was found."
            : $"Proposal {proposal.ProposalId} links to execution {sourceExecutionId}.");

        if (assimilation is not null)
        {
            signals.Add($"Decision assimilation recommendation exists for {assimilation.DecisionId}.");
        }

        return signals;
    }

    private static List<string> BuildConflicts(
        OperationalContextProposal? proposal,
        WorkflowOperationalContextStatus status)
    {
        List<string> conflicts = [];
        if (proposal is null)
        {
            return conflicts;
        }

        if (proposal.RepositoryId == Guid.Empty)
        {
            conflicts.Add($"Operational-context proposal {proposal.ProposalId} has no repository id.");
        }

        if (status is WorkflowOperationalContextStatus.Promoted && proposal.Promotion.PromotedAt is null)
        {
            conflicts.Add($"Operational-context proposal {proposal.ProposalId} is promoted without promotion timestamp.");
        }

        if (status is WorkflowOperationalContextStatus.Rejected && proposal.Review.ReviewState is not OperationalContextReviewState.Rejected)
        {
            conflicts.Add($"Operational-context proposal {proposal.ProposalId} is rejected but review state is {proposal.Review.ReviewState}.");
        }

        return conflicts;
    }

    private static IReadOnlyList<string> BuildInputs(
        IReadOnlyList<OperationalContextProposal> proposals,
        DecisionAssimilationRecommendation? assimilation,
        WorkflowExecutionProjection execution) =>
        [
            $"operational-context-proposals:{proposals.Count}:{string.Join(",", proposals.OrderBy(proposal => proposal.ProposalId).Select(proposal => $"{proposal.ProposalId}:{proposal.Status}:{proposal.Review.ReviewState}:promoted={proposal.Promotion.PromotedAt is not null}"))}",
            $"decision-assimilation:{assimilation?.DecisionId ?? "none"}:{assimilation?.CreatedAt.ToString("O") ?? "none"}",
            $"execution:{execution.ExecutionId?.ToString() ?? "none"}:completed={execution.CompletedAt?.ToString("O") ?? "none"}"
        ];

    private static string BuildSummary(WorkflowOperationalContextStatus status, OperationalContextProposal? proposal) =>
        proposal is null
            ? "No operational-context update is required before commit."
            : $"Operational-context proposal {proposal.ProposalId} is {status}.";
}
