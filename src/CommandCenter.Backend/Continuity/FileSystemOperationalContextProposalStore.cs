using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Continuity;

public sealed class FileSystemOperationalContextProposalStore(IArtifactStore artifactStore)
    : IOperationalContextProposalStore
{
    private const string ProposalsRelativePath = ".agents/operational_context/proposals";
    private const string MetadataFileName = "metadata.json";
    private const string ProposedFileName = "proposed.md";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<OperationalContextProposal> SaveAsync(
        Repository repository,
        OperationalContextProposal proposal,
        string generatedContent)
    {
        var proposalId = NormalizeProposalId(proposal.ProposalId);
        var generatedRelativePath = ArtifactPath.CombineRelative(
            ProposalsRelativePath,
            proposalId,
            ProposedFileName);
        var metadataRelativePath = ArtifactPath.CombineRelative(
            ProposalsRelativePath,
            proposalId,
            MetadataFileName);

        var storedProposal = new OperationalContextProposal
        {
            ProposalId = proposalId,
            RepositoryId = proposal.RepositoryId,
            GeneratedAt = proposal.GeneratedAt,
            Status = proposal.Status,
            InputFingerprints = proposal.InputFingerprints,
            BaselineCurrentContextHash = proposal.BaselineCurrentContextHash,
            GeneratedContentHash = proposal.GeneratedContentHash,
            GeneratedContentRelativePath = generatedRelativePath,
            EditedContentRelativePath = proposal.EditedContentRelativePath,
            SemanticChanges = proposal.SemanticChanges,
            CompressionSummary = proposal.CompressionSummary
        };

        await artifactStore.WriteAsync(ArtifactPath.ResolveRepositoryPath(repository, generatedRelativePath), generatedContent);
        await artifactStore.WriteAsync(
            ArtifactPath.ResolveRepositoryPath(repository, metadataRelativePath),
            JsonSerializer.Serialize(storedProposal, JsonOptions));

        return storedProposal.WithContent(generatedContent);
    }

    public async Task<IReadOnlyList<OperationalContextProposal>> ListAsync(
        Repository repository,
        bool includeContent = false)
    {
        var proposalRoot = ArtifactPath.ResolveRepositoryPath(repository, ProposalsRelativePath);
        var proposals = new List<OperationalContextProposal>();

        foreach (var directory in await artifactStore.ListDirectoriesAsync(proposalRoot))
        {
            var proposalId = Path.GetFileName(directory);
            var proposal = await GetAsync(repository, proposalId, includeContent);
            if (proposal is not null)
            {
                proposals.Add(proposal);
            }
        }

        return proposals
            .OrderByDescending(proposal => proposal.GeneratedAt)
            .ThenByDescending(proposal => proposal.ProposalId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<OperationalContextProposal?> GetAsync(
        Repository repository,
        string proposalId,
        bool includeContent = false)
    {
        proposalId = NormalizeProposalId(proposalId);
        var metadataPath = ArtifactPath.ResolveRepositoryPath(
            repository,
            ArtifactPath.CombineRelative(ProposalsRelativePath, proposalId, MetadataFileName));
        var metadata = await artifactStore.ReadAsync(metadataPath);
        if (metadata is null)
        {
            return null;
        }

        var proposal = JsonSerializer.Deserialize<OperationalContextProposal>(metadata, JsonOptions);
        if (proposal is null)
        {
            return null;
        }

        if (!includeContent)
        {
            return proposal;
        }

        var content = await artifactStore.ReadAsync(
            ArtifactPath.ResolveRepositoryPath(repository, proposal.GeneratedContentRelativePath));
        return proposal.WithContent(content);
    }

    public async Task SupersedePendingAsync(Repository repository)
    {
        var proposals = await ListAsync(repository);
        foreach (var proposal in proposals.Where(proposal => proposal.Status == OperationalContextProposalStatus.Pending))
        {
            await WriteMetadataAsync(repository, new OperationalContextProposal
            {
                ProposalId = proposal.ProposalId,
                RepositoryId = proposal.RepositoryId,
                GeneratedAt = proposal.GeneratedAt,
                Status = OperationalContextProposalStatus.Superseded,
                InputFingerprints = proposal.InputFingerprints,
                BaselineCurrentContextHash = proposal.BaselineCurrentContextHash,
                GeneratedContentHash = proposal.GeneratedContentHash,
                GeneratedContentRelativePath = proposal.GeneratedContentRelativePath,
                EditedContentRelativePath = proposal.EditedContentRelativePath,
                SemanticChanges = proposal.SemanticChanges,
                CompressionSummary = proposal.CompressionSummary
            });
        }
    }

    private async Task WriteMetadataAsync(Repository repository, OperationalContextProposal proposal)
    {
        var metadataRelativePath = ArtifactPath.CombineRelative(
            ProposalsRelativePath,
            NormalizeProposalId(proposal.ProposalId),
            MetadataFileName);
        await artifactStore.WriteAsync(
            ArtifactPath.ResolveRepositoryPath(repository, metadataRelativePath),
            JsonSerializer.Serialize(proposal, JsonOptions));
    }

    private static string NormalizeProposalId(string proposalId)
    {
        if (string.IsNullOrWhiteSpace(proposalId) ||
            proposalId.Any(character => !char.IsLetterOrDigit(character) && character != '-' && character != '_'))
        {
            throw new ArgumentException("Proposal id must contain only letters, digits, hyphen, or underscore.", nameof(proposalId));
        }

        return proposalId;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

file static class OperationalContextProposalContentMutation
{
    public static OperationalContextProposal WithContent(this OperationalContextProposal proposal, string? generatedContent)
    {
        return new OperationalContextProposal
        {
            ProposalId = proposal.ProposalId,
            RepositoryId = proposal.RepositoryId,
            GeneratedAt = proposal.GeneratedAt,
            Status = proposal.Status,
            InputFingerprints = proposal.InputFingerprints,
            BaselineCurrentContextHash = proposal.BaselineCurrentContextHash,
            GeneratedContentHash = proposal.GeneratedContentHash,
            GeneratedContentRelativePath = proposal.GeneratedContentRelativePath,
            EditedContentRelativePath = proposal.EditedContentRelativePath,
            SemanticChanges = proposal.SemanticChanges,
            CompressionSummary = proposal.CompressionSummary,
            GeneratedContent = generatedContent
        };
    }
}
