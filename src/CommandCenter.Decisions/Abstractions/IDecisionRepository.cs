using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionRepository
{
    Task<DecisionId> AllocateDecisionIdAsync(Repository repository);

    Task<string> AllocateCandidateIdAsync(Repository repository);

    Task<string> AllocateProposalIdAsync(Repository repository);

    Task<string> AllocateProposalRevisionIdAsync(Repository repository, string proposalId);

    Task<IReadOnlyList<Decision>> ListDecisionsAsync(Repository repository);

    Task<Decision?> GetDecisionAsync(Repository repository, DecisionId decisionId);

    Task<Decision> SaveDecisionAsync(Repository repository, Decision decision);

    Task<IReadOnlyList<DecisionCandidate>> ListCandidatesAsync(Repository repository);

    Task<DecisionCandidate?> GetCandidateAsync(Repository repository, string candidateId);

    Task<DecisionCandidate> SaveCandidateAsync(Repository repository, DecisionCandidate candidate);

    Task<IReadOnlyList<DecisionProposal>> ListProposalsAsync(Repository repository);

    Task<DecisionProposal?> GetProposalAsync(Repository repository, string proposalId);

    Task<DecisionProposal> SaveProposalAsync(Repository repository, DecisionProposal proposal);

    Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Repository repository, string proposalId);

    Task<DecisionProposalRevision> SaveProposalRevisionAsync(Repository repository, DecisionProposalRevision revision);
}
