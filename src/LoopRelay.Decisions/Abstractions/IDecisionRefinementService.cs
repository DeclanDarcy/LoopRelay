using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IDecisionRefinementService
{
    Task<DecisionProposal> RefineProposalAsync(Guid repositoryId, string proposalId, DecisionRefinementRequest request);

    Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Guid repositoryId, string proposalId);

    Task<DecisionProposalLineage> GetProposalLineageAsync(Guid repositoryId, string proposalId);

    Task<DecisionProposalRevisionComparison> GetProposalRevisionComparisonAsync(
        Guid repositoryId,
        string proposalId,
        string revisionId);
}
