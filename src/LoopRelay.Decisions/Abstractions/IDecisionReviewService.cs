using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Abstractions;

public interface IDecisionReviewService
{
    Task<DecisionReviewWorkspace> GetReviewWorkspaceAsync(Guid repositoryId, string proposalId);

    Task<IReadOnlyList<DecisionProposalBrowserItem>> ListProposalBrowserItemsAsync(
        Guid repositoryId,
        IReadOnlySet<DecisionProposalState>? states = null);

    Task<DecisionOptionComparison> GetOptionComparisonAsync(Guid repositoryId, string proposalId);

    Task<DecisionEvidenceInspection> GetEvidenceInspectionAsync(Guid repositoryId, string proposalId);

    Task<IReadOnlyList<DecisionSourceAttribution>> ListSourceAttributionsAsync(Guid repositoryId, string proposalId);

    Task<IReadOnlyList<DecisionReviewNote>> ListReviewNotesAsync(Guid repositoryId, string proposalId);

    Task<DecisionReviewNote> AddReviewNoteAsync(Guid repositoryId, string proposalId, DecisionReviewNoteRequest request);

    Task<DecisionReviewWorkspace> MarkProposalViewedAsync(Guid repositoryId, string proposalId, string? reason);

    Task<DecisionReviewWorkspace> MarkProposalNeedsRefinementAsync(Guid repositoryId, string proposalId, string? reason);

    Task<DecisionReviewWorkspace> MarkProposalReadyForResolutionAsync(Guid repositoryId, string proposalId, string? reason);
}
