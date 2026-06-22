using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionGenerationService
{
    Task<IReadOnlyList<DecisionProposal>> ListProposalsAsync(Guid repositoryId);

    Task<DecisionProposal> GetProposalAsync(Guid repositoryId, string proposalId);

    Task<DecisionProposal> GenerateProposalAsync(Guid repositoryId, string candidateId);

    Task<DecisionProposal> ExpireProposalAsync(Guid repositoryId, string proposalId, string? reason);
}
