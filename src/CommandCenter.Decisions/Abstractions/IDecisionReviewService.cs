using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionReviewService
{
    Task<DecisionReviewWorkspace> GetReviewWorkspaceAsync(Guid repositoryId, string proposalId);

    Task<IReadOnlyList<DecisionReviewNote>> ListReviewNotesAsync(Guid repositoryId, string proposalId);

    Task<DecisionReviewNote> AddReviewNoteAsync(Guid repositoryId, string proposalId, DecisionReviewNoteRequest request);

    Task<DecisionReviewWorkspace> MarkProposalViewedAsync(Guid repositoryId, string proposalId, string? reason);

    Task<DecisionReviewWorkspace> MarkProposalNeedsRefinementAsync(Guid repositoryId, string proposalId, string? reason);

    Task<DecisionReviewWorkspace> MarkProposalReadyForResolutionAsync(Guid repositoryId, string proposalId, string? reason);
}
