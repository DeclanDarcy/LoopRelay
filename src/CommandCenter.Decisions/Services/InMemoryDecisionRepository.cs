using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class InMemoryDecisionRepository : IDecisionRepository
{
    private readonly Dictionary<Guid, SortedDictionary<string, Decision>> decisionsByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, DecisionCandidate>> candidatesByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, DecisionProposal>> proposalsByRepository = [];
    private readonly Dictionary<Guid, SortedDictionary<string, SortedDictionary<string, DecisionProposalRevision>>> revisionsByRepository = [];

    public Task<DecisionId> AllocateDecisionIdAsync(Repository repository)
    {
        return Task.FromResult(new DecisionId(NextId(GetDecisions(repository.Id).Keys, "DEC")));
    }

    public Task<string> AllocateCandidateIdAsync(Repository repository)
    {
        return Task.FromResult(NextId(GetCandidates(repository.Id).Keys, "CAND"));
    }

    public Task<string> AllocateProposalIdAsync(Repository repository)
    {
        return Task.FromResult(NextId(GetProposals(repository.Id).Keys, "PROP"));
    }

    public Task<string> AllocateProposalRevisionIdAsync(Repository repository, string proposalId)
    {
        return Task.FromResult(NextId(GetRevisions(repository.Id, proposalId).Keys, "REV"));
    }

    public Task<IReadOnlyList<Decision>> ListDecisionsAsync(Repository repository)
    {
        return Task.FromResult<IReadOnlyList<Decision>>(GetDecisions(repository.Id).Values.ToArray());
    }

    public Task<Decision?> GetDecisionAsync(Repository repository, DecisionId decisionId)
    {
        GetDecisions(repository.Id).TryGetValue(decisionId.Value, out Decision? decision);
        return Task.FromResult(decision);
    }

    public Task<Decision> SaveDecisionAsync(Repository repository, Decision decision)
    {
        if (decision.Metadata.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision belongs to a different repository.");
        }

        GetDecisions(repository.Id)[decision.Id.Value] = decision;
        return Task.FromResult(decision);
    }

    public Task<IReadOnlyList<DecisionCandidate>> ListCandidatesAsync(Repository repository)
    {
        return Task.FromResult<IReadOnlyList<DecisionCandidate>>(GetCandidates(repository.Id).Values.ToArray());
    }

    public Task<DecisionCandidate?> GetCandidateAsync(Repository repository, string candidateId)
    {
        GetCandidates(repository.Id).TryGetValue(candidateId, out DecisionCandidate? candidate);
        return Task.FromResult(candidate);
    }

    public Task<DecisionCandidate> SaveCandidateAsync(Repository repository, DecisionCandidate candidate)
    {
        if (candidate.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision candidate belongs to a different repository.");
        }

        GetCandidates(repository.Id)[candidate.Id] = candidate;
        return Task.FromResult(candidate);
    }

    public Task<IReadOnlyList<DecisionProposal>> ListProposalsAsync(Repository repository)
    {
        return Task.FromResult<IReadOnlyList<DecisionProposal>>(GetProposals(repository.Id).Values.ToArray());
    }

    public Task<DecisionProposal?> GetProposalAsync(Repository repository, string proposalId)
    {
        GetProposals(repository.Id).TryGetValue(proposalId, out DecisionProposal? proposal);
        return Task.FromResult(proposal);
    }

    public Task<DecisionProposal> SaveProposalAsync(Repository repository, DecisionProposal proposal)
    {
        if (proposal.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision proposal belongs to a different repository.");
        }

        GetProposals(repository.Id)[proposal.Id] = proposal;
        return Task.FromResult(proposal);
    }

    public Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Repository repository, string proposalId)
    {
        return Task.FromResult<IReadOnlyList<DecisionProposalRevision>>(GetRevisions(repository.Id, proposalId).Values.ToArray());
    }

    public Task<DecisionProposalRevision> SaveProposalRevisionAsync(Repository repository, DecisionProposalRevision revision)
    {
        if (revision.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision proposal revision belongs to a different repository.");
        }

        GetRevisions(repository.Id, revision.ProposalId)[revision.Id] = revision;
        return Task.FromResult(revision);
    }

    private SortedDictionary<string, Decision> GetDecisions(Guid repositoryId)
    {
        return GetRepositoryMap(decisionsByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionCandidate> GetCandidates(Guid repositoryId)
    {
        return GetRepositoryMap(candidatesByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionProposal> GetProposals(Guid repositoryId)
    {
        return GetRepositoryMap(proposalsByRepository, repositoryId);
    }

    private SortedDictionary<string, DecisionProposalRevision> GetRevisions(Guid repositoryId, string proposalId)
    {
        SortedDictionary<string, SortedDictionary<string, DecisionProposalRevision>> repositoryRevisions =
            GetRepositoryMap(revisionsByRepository, repositoryId);
        if (!repositoryRevisions.TryGetValue(proposalId, out SortedDictionary<string, DecisionProposalRevision>? revisions))
        {
            revisions = new SortedDictionary<string, DecisionProposalRevision>(StringComparer.Ordinal);
            repositoryRevisions[proposalId] = revisions;
        }

        return revisions;
    }

    private static SortedDictionary<string, T> GetRepositoryMap<T>(
        Dictionary<Guid, SortedDictionary<string, T>> maps,
        Guid repositoryId)
    {
        if (!maps.TryGetValue(repositoryId, out SortedDictionary<string, T>? map))
        {
            map = new SortedDictionary<string, T>(StringComparer.Ordinal);
            maps[repositoryId] = map;
        }

        return map;
    }

    private static string NextId(IEnumerable<string> existingIds, string prefix)
    {
        int next = existingIds
            .Select(id => ParseSequence(id, prefix))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}-{next:0000}";
    }

    private static int ParseSequence(string id, string prefix)
    {
        return id.StartsWith($"{prefix}-", StringComparison.Ordinal) &&
            int.TryParse(id[(prefix.Length + 1)..], out int sequence)
            ? sequence
            : 0;
    }
}
