using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionRefinementService
{
    Task<DecisionProposal> RefineProposalAsync(Guid repositoryId, string proposalId, DecisionRefinementRequest request);

    Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Guid repositoryId, string proposalId);
}
