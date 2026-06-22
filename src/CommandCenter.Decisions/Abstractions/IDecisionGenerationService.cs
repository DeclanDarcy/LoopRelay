using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionGenerationService
{
    Task<IReadOnlyList<DecisionProposal>> ListProposalsAsync(Guid repositoryId);

    Task<DecisionProposal> GetProposalAsync(Guid repositoryId, string proposalId);

    Task<DecisionProposal> GenerateProposalAsync(Guid repositoryId, string candidateId);

    Task<DecisionProposal> MarkProposalViewedAsync(Guid repositoryId, string proposalId, string? reason);

    Task<DecisionProposal> MarkProposalNeedsRefinementAsync(Guid repositoryId, string proposalId, string? reason);

    Task<DecisionProposal> MarkProposalReadyForResolutionAsync(Guid repositoryId, string proposalId, string? reason);

    Task<DecisionProposal> RefineProposalAsync(Guid repositoryId, string proposalId, DecisionRefinementRequest request);

    Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Guid repositoryId, string proposalId);

    Task<DecisionProposal> ExpireProposalAsync(Guid repositoryId, string proposalId, string? reason);
}
