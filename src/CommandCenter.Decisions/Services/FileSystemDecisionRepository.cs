using System.Text.Json;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Persistence;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class FileSystemDecisionRepository(IArtifactStore artifactStore) : IDecisionRepository
{
    public async Task<DecisionId> AllocateDecisionIdAsync(Repository repository)
    {
        return new DecisionId(await AllocateIdAsync(repository, DecisionArtifactKind.Decision, "DEC"));
    }

    public async Task<string> AllocateCandidateIdAsync(Repository repository)
    {
        return await AllocateIdAsync(repository, DecisionArtifactKind.Candidate, "CAND");
    }

    public async Task<string> AllocateProposalIdAsync(Repository repository)
    {
        return await AllocateIdAsync(repository, DecisionArtifactKind.Proposal, "PROP");
    }

    public async Task<string> AllocateProposalRevisionIdAsync(Repository repository, string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        string revisionsRoot = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.ProposalRevisionsDirectory(id));
        IReadOnlyList<string> files = await artifactStore.ListAsync(revisionsRoot, "REV-*.json");
        int next = files
            .Select(Path.GetFileNameWithoutExtension)
            .Where(revisionId => !string.IsNullOrWhiteSpace(revisionId))
            .Select(revisionId => ParseSequence(revisionId!, "REV"))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"REV-{next:0000}";
    }

    public async Task<IReadOnlyList<Decision>> ListDecisionsAsync(Repository repository)
    {
        IReadOnlyList<string> directories = await ListArtifactDirectoriesAsync(repository, DecisionArtifactKind.Decision);
        var decisions = new List<Decision>();
        foreach (string directory in directories)
        {
            string id = Path.GetFileName(directory);
            Decision? decision = await GetDecisionAsync(repository, new DecisionId(id));
            if (decision is not null)
            {
                decisions.Add(decision);
            }
        }

        return decisions.OrderBy(decision => decision.Id.Value, StringComparer.Ordinal).ToArray();
    }

    public async Task<Decision?> GetDecisionAsync(Repository repository, DecisionId decisionId)
    {
        string id = DecisionArtifactPaths.ValidateId(decisionId.Value, "DEC");
        return await ReadPayloadAsync<Decision>(
            repository,
            DecisionArtifactPaths.DecisionJson(id));
    }

    public async Task<Decision> SaveDecisionAsync(Repository repository, Decision decision)
    {
        string id = DecisionArtifactPaths.ValidateId(decision.Id.Value, "DEC");
        if (decision.Metadata.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision belongs to a different repository.");
        }

        string directory = DecisionArtifactPaths.DecisionDirectory(id);
        await WriteDocumentAsync(repository, DecisionArtifactPaths.DecisionJson(id), decision, decision.Metadata.CreatedAt, decision.Metadata.UpdatedAt);
        await WriteHistoryAsync(repository, directory, decision.History);
        return decision;
    }

    public async Task<IReadOnlyList<DecisionCandidate>> ListCandidatesAsync(Repository repository)
    {
        IReadOnlyList<string> directories = await ListArtifactDirectoriesAsync(repository, DecisionArtifactKind.Candidate);
        var candidates = new List<DecisionCandidate>();
        foreach (string directory in directories)
        {
            string id = Path.GetFileName(directory);
            DecisionCandidate? candidate = await GetCandidateAsync(repository, id);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        return candidates.OrderBy(candidate => candidate.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<DecisionCandidate?> GetCandidateAsync(Repository repository, string candidateId)
    {
        string id = DecisionArtifactPaths.ValidateId(candidateId, "CAND");
        return await ReadPayloadAsync<DecisionCandidate>(
            repository,
            DecisionArtifactPaths.CandidateJson(id));
    }

    public async Task<DecisionCandidate> SaveCandidateAsync(Repository repository, DecisionCandidate candidate)
    {
        string id = DecisionArtifactPaths.ValidateId(candidate.Id, "CAND");
        if (candidate.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision candidate belongs to a different repository.");
        }

        string directory = DecisionArtifactPaths.CandidateDirectory(id);
        string relativePath = DecisionArtifactPaths.CandidateJson(id);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset createdAt = await GetExistingCreatedAtAsync<DecisionCandidate>(repository, relativePath) ?? now;
        await WriteDocumentAsync(repository, relativePath, candidate, createdAt, now);
        await WriteHistoryAsync(repository, directory, candidate.History);
        return candidate;
    }

    public async Task<IReadOnlyList<DecisionProposal>> ListProposalsAsync(Repository repository)
    {
        IReadOnlyList<string> directories = await ListArtifactDirectoriesAsync(repository, DecisionArtifactKind.Proposal);
        var proposals = new List<DecisionProposal>();
        foreach (string directory in directories)
        {
            string id = Path.GetFileName(directory);
            DecisionProposal? proposal = await GetProposalAsync(repository, id);
            if (proposal is not null)
            {
                proposals.Add(proposal);
            }
        }

        return proposals.OrderBy(proposal => proposal.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<DecisionProposal?> GetProposalAsync(Repository repository, string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        return await ReadPayloadAsync<DecisionProposal>(
            repository,
            DecisionArtifactPaths.ProposalJson(id));
    }

    public async Task<DecisionProposal> SaveProposalAsync(Repository repository, DecisionProposal proposal)
    {
        string id = DecisionArtifactPaths.ValidateId(proposal.Id, "PROP");
        if (proposal.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision proposal belongs to a different repository.");
        }

        string directory = DecisionArtifactPaths.ProposalDirectory(id);
        string relativePath = DecisionArtifactPaths.ProposalJson(id);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset createdAt = await GetExistingCreatedAtAsync<DecisionProposal>(repository, relativePath) ?? now;
        await WriteDocumentAsync(repository, relativePath, proposal, createdAt, now);
        await WriteHistoryAsync(repository, directory, proposal.History);
        return proposal;
    }

    public async Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Repository repository, string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        string revisionsRoot = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.ProposalRevisionsDirectory(id));
        IReadOnlyList<string> files = await artifactStore.ListAsync(revisionsRoot, "REV-*.json");
        var revisions = new List<DecisionProposalRevision>();

        foreach (string file in files
            .Where(file => string.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.Ordinal))
        {
            string revisionId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(revisionId))
            {
                continue;
            }

            DecisionProposalRevision? revision = await ReadPayloadAsync<DecisionProposalRevision>(
                repository,
                DecisionArtifactPaths.ProposalRevisionJson(id, revisionId));
            if (revision is not null)
            {
                revisions.Add(revision);
            }
        }

        return revisions.OrderBy(revision => revision.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<DecisionProposalRevision> SaveProposalRevisionAsync(Repository repository, DecisionProposalRevision revision)
    {
        string proposalId = DecisionArtifactPaths.ValidateId(revision.ProposalId, "PROP");
        string revisionId = DecisionArtifactPaths.ValidateId(revision.Id, "REV");
        if (revision.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision proposal revision belongs to a different repository.");
        }

        await WriteDocumentAsync(
            repository,
            DecisionArtifactPaths.ProposalRevisionJson(proposalId, revisionId),
            revision,
            revision.CreatedAt,
            revision.CreatedAt);
        return revision;
    }

    private async Task<string> AllocateIdAsync(Repository repository, DecisionArtifactKind kind, string prefix)
    {
        IReadOnlyList<string> directories = await ListArtifactDirectoriesAsync(repository, kind);
        int next = directories
            .Select(Path.GetFileName)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => ParseSequence(id!, prefix))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}-{next:0000}";
    }

    private async Task<IReadOnlyList<string>> ListArtifactDirectoriesAsync(Repository repository, DecisionArtifactKind kind)
    {
        string root = DecisionArtifactPaths.ResolveRoot(repository, kind);
        return await artifactStore.ListDirectoriesAsync(root);
    }

    private async Task<T?> ReadPayloadAsync<T>(Repository repository, string relativePath)
    {
        DecisionArtifactDocument<T>? document = await ReadDocumentAsync<T>(repository, relativePath);
        if (document is null)
        {
            return default;
        }

        if (!string.Equals(document.SchemaVersion, DecisionArtifactPaths.SchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported decision artifact schema version '{document.SchemaVersion}'.");
        }

        if (document.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision artifact belongs to a different repository.");
        }

        return document.Payload;
    }

    private async Task<DateTimeOffset?> GetExistingCreatedAtAsync<T>(Repository repository, string relativePath)
    {
        DecisionArtifactDocument<T>? existing = await ReadDocumentAsync<T>(repository, relativePath);
        return existing?.CreatedAt;
    }

    private async Task<DecisionArtifactDocument<T>?> ReadDocumentAsync<T>(Repository repository, string relativePath)
    {
        string? json = await artifactStore.ReadAsync(DecisionArtifactPaths.Resolve(repository, relativePath));
        if (json is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<DecisionArtifactDocument<T>>(
            json,
            DecisionJson.Options);
    }

    private async Task WriteDocumentAsync<T>(
        Repository repository,
        string relativePath,
        T payload,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var document = new DecisionArtifactDocument<T>(
            DecisionArtifactPaths.SchemaVersion,
            repository.Id,
            createdAt,
            updatedAt,
            payload);
        await artifactStore.WriteAsync(
            DecisionArtifactPaths.Resolve(repository, relativePath),
            JsonSerializer.Serialize(document, DecisionJson.Options));
    }

    private async Task WriteHistoryAsync(
        Repository repository,
        string relativeDirectory,
        IReadOnlyList<DecisionHistoryEntry> history)
    {
        await WriteDocumentAsync(
            repository,
            DecisionArtifactPaths.HistoryJsonForDirectory(relativeDirectory),
            history,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static int ParseSequence(string id, string prefix)
    {
        return id.StartsWith($"{prefix}-", StringComparison.Ordinal) &&
            int.TryParse(id[(prefix.Length + 1)..], out int sequence)
            ? sequence
            : 0;
    }
}
