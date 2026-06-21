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
    private const string EditedFileName = "edited.md";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<OperationalContextProposal> SaveAsync(
        Repository repository,
        OperationalContextProposal proposal,
        string generatedContent)
    {
        string proposalId = NormalizeProposalId(proposal.ProposalId);
        string generatedRelativePath = ArtifactPath.CombineRelative(
            ProposalsRelativePath,
            proposalId,
            ProposedFileName);
        string metadataRelativePath = ArtifactPath.CombineRelative(
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
            CompressionSummary = proposal.CompressionSummary,
            Review = proposal.Review.ProposalId.Length == 0
                ? CreatePendingReview(proposalId, proposal.BaselineCurrentContextHash)
                : proposal.Review,
            Promotion = proposal.Promotion.ProposalId.Length == 0
                ? new OperationalContextPromotion { ProposalId = proposalId }
                : proposal.Promotion
        };

        await artifactStore.WriteAsync(ArtifactPath.ResolveRepositoryPath(repository, generatedRelativePath), generatedContent);
        await artifactStore.WriteAsync(
            ArtifactPath.ResolveRepositoryPath(repository, metadataRelativePath),
            JsonSerializer.Serialize(storedProposal, JsonOptions));

        return storedProposal.WithContent(generatedContent, editedContent: null);
    }

    public async Task<IReadOnlyList<OperationalContextProposal>> ListAsync(
        Repository repository,
        bool includeContent = false)
    {
        string proposalRoot = ArtifactPath.ResolveRepositoryPath(repository, ProposalsRelativePath);
        var proposals = new List<OperationalContextProposal>();

        foreach (string directory in await artifactStore.ListDirectoriesAsync(proposalRoot))
        {
            string proposalId = Path.GetFileName(directory);
            OperationalContextProposal? proposal = await GetAsync(repository, proposalId, includeContent);
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
        string metadataPath = ArtifactPath.ResolveRepositoryPath(
            repository,
            ArtifactPath.CombineRelative(ProposalsRelativePath, proposalId, MetadataFileName));
        string? metadata = await artifactStore.ReadAsync(metadataPath);
        if (metadata is null)
        {
            return null;
        }

        OperationalContextProposal? proposal;
        try
        {
            proposal = JsonSerializer.Deserialize<OperationalContextProposal>(metadata, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (proposal is null)
        {
            return null;
        }

        if (!includeContent)
        {
            return proposal;
        }

        string? content = await artifactStore.ReadAsync(
            ArtifactPath.ResolveRepositoryPath(repository, proposal.GeneratedContentRelativePath));
        string? editedContent = null;
        if (!string.IsNullOrWhiteSpace(proposal.EditedContentRelativePath))
        {
            editedContent = await artifactStore.ReadAsync(
                ArtifactPath.ResolveRepositoryPath(repository, proposal.EditedContentRelativePath));
        }

        return proposal.WithContent(content, editedContent);
    }

    public async Task<OperationalContextProposal> UpdateAsync(
        Repository repository,
        OperationalContextProposal proposal,
        string? editedContent = null,
        bool includeContent = false)
    {
        string proposalId = NormalizeProposalId(proposal.ProposalId);
        string? editedRelativePath = proposal.EditedContentRelativePath;
        if (editedContent is not null)
        {
            editedRelativePath = ArtifactPath.CombineRelative(
                ProposalsRelativePath,
                proposalId,
                EditedFileName);
            await artifactStore.WriteAsync(
                ArtifactPath.ResolveRepositoryPath(repository, editedRelativePath),
                editedContent);
        }

        var storedProposal = new OperationalContextProposal
        {
            ProposalId = proposalId,
            RepositoryId = proposal.RepositoryId,
            GeneratedAt = proposal.GeneratedAt,
            Status = proposal.Status,
            InputFingerprints = proposal.InputFingerprints,
            BaselineCurrentContextHash = proposal.BaselineCurrentContextHash,
            GeneratedContentHash = proposal.GeneratedContentHash,
            GeneratedContentRelativePath = proposal.GeneratedContentRelativePath,
            EditedContentRelativePath = editedRelativePath,
            SemanticChanges = proposal.SemanticChanges,
            CompressionSummary = proposal.CompressionSummary,
            Review = proposal.Review,
            Promotion = proposal.Promotion.ProposalId.Length == 0
                ? new OperationalContextPromotion { ProposalId = proposalId }
                : proposal.Promotion
        };

        await WriteMetadataAsync(repository, storedProposal);
        return includeContent
            ? (await GetAsync(repository, proposalId, includeContent: true)) ?? storedProposal
            : storedProposal;
    }

    public async Task SupersedePendingAsync(Repository repository)
    {
        IReadOnlyList<OperationalContextProposal> proposals = await ListAsync(repository);
        foreach (OperationalContextProposal proposal in proposals.Where(proposal => proposal.Status == OperationalContextProposalStatus.Pending))
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
                CompressionSummary = proposal.CompressionSummary,
                Review = new OperationalContextReview
                {
                    ProposalId = proposal.ProposalId,
                    ReviewState = OperationalContextReviewState.Stale,
                    BaselineCurrentContextHash = proposal.BaselineCurrentContextHash,
                    ReviewedContentHash = proposal.Review.ReviewedContentHash,
                    ReviewedAt = DateTimeOffset.UtcNow,
                    ReviewNote = proposal.Review.ReviewNote,
                    StaleReason = "Proposal was superseded by a newer generated proposal."
                },
                Promotion = proposal.Promotion
            });
        }
    }

    private async Task WriteMetadataAsync(Repository repository, OperationalContextProposal proposal)
    {
        string metadataRelativePath = ArtifactPath.CombineRelative(
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

    private static OperationalContextReview CreatePendingReview(string proposalId, string baselineHash)
    {
        return new OperationalContextReview
        {
            ProposalId = proposalId,
            ReviewState = OperationalContextReviewState.PendingReview,
            BaselineCurrentContextHash = baselineHash
        };
    }
}

file static class OperationalContextProposalContentMutation
{
    public static OperationalContextProposal WithContent(
        this OperationalContextProposal proposal,
        string? generatedContent,
        string? editedContent)
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
            Review = proposal.Review,
            Promotion = proposal.Promotion,
            GeneratedContent = generatedContent,
            EditedContent = editedContent
        };
    }
}
