using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionDiscoveryService
{
    Task<IReadOnlyList<DecisionCandidate>> ListCandidatesAsync(Guid repositoryId);

    Task<DecisionDiscoveryResult> DiscoverAsync(Guid repositoryId);

    Task<DecisionCandidate> PromoteCandidateAsync(Guid repositoryId, string candidateId, string? reason);

    Task<DecisionCandidate> DismissCandidateAsync(Guid repositoryId, string candidateId, string? reason);

    Task<DecisionCandidate> ExpireCandidateAsync(Guid repositoryId, string candidateId, string? reason);

    Task<DecisionCandidate> MarkCandidateDuplicateAsync(
        Guid repositoryId,
        string candidateId,
        string duplicateOfCandidateId,
        string? reason);
}
