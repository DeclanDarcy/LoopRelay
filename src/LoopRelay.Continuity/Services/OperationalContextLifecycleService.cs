using System.Security.Cryptography;
using System.Text;
using LoopRelay.Continuity.Abstractions;
using LoopRelay.Continuity.Models;
using LoopRelay.Continuity.Primitives;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;

namespace LoopRelay.Continuity.Services;

public sealed class OperationalContextLifecycleService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IArtifactRotationService artifactRotationService,
    IOperationalContextProposalStore proposalStore) : IOperationalContextLifecycleService
{
    private const string CurrentOperationalContextPath = ".agents/operational_context.md";

    public async Task<OperationalContextProposal> PromoteAsync(Guid repositoryId, string proposalId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        OperationalContextProposal proposal = await GetRequiredProposalAsync(repository, proposalId);
        await EnsurePromotableLatestAsync(repository, proposal);

        string acceptedContent = GetAcceptedContent(proposal);
        string acceptedContentHash = HashContent(acceptedContent);
        if (!string.Equals(acceptedContentHash, proposal.Review.ReviewedContentHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cannot promote operational-context proposal because accepted content no longer matches review metadata.");
        }

        string? currentContent = await ReadCurrentOperationalContextAsync(repository);
        string currentHash = HashOptionalContent(currentContent);
        if (!string.Equals(currentHash, proposal.Review.BaselineCurrentContextHash, StringComparison.Ordinal))
        {
            OperationalContextProposal staleProposal = WithReviewState(
                proposal,
                OperationalContextReviewState.Stale,
                "Current operational context changed after this proposal was accepted.");
            await proposalStore.UpdateAsync(repository, staleProposal);
            throw new InvalidOperationException("Cannot promote a stale operational-context proposal because current context changed after acceptance.");
        }

        Artifact? archivedArtifact = null;
        if (currentContent is not null)
        {
            try
            {
                archivedArtifact = await artifactRotationService.RotateCurrentOperationalContextAsync(repository);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                await proposalStore.UpdateAsync(repository, WithPromotionFailure(
                    proposal,
                    archivedArtifact: null,
                    archiveFailureReason: exception.Message,
                    writeFailureReason: null));
                throw new InvalidOperationException("Cannot promote operational-context proposal because current context archival failed.", exception);
            }
        }

        try
        {
            await artifactService.SaveAsync(repository, CurrentOperationalContextPath, acceptedContent);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await proposalStore.UpdateAsync(repository, WithPromotionFailure(
                proposal,
                archivedArtifact,
                archiveFailureReason: null,
                writeFailureReason: exception.Message));
            throw new InvalidOperationException("Cannot promote operational-context proposal because writing current context failed.", exception);
        }

        return await proposalStore.UpdateAsync(repository, WithPromotionSuccess(
            proposal,
            acceptedContentHash,
            GetAcceptedContentSourceRelativePath(proposal),
            archivedArtifact),
            includeContent: true);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<OperationalContextProposal> GetRequiredProposalAsync(Repository repository, string proposalId)
    {
        return await proposalStore.GetAsync(repository, proposalId, includeContent: true)
            ?? throw new KeyNotFoundException($"Operational-context proposal was not found: {proposalId}");
    }

    private async Task EnsurePromotableLatestAsync(Repository repository, OperationalContextProposal proposal)
    {
        if (proposal.Status != OperationalContextProposalStatus.Accepted ||
            proposal.Review.ReviewState != OperationalContextReviewState.Accepted ||
            string.IsNullOrWhiteSpace(proposal.Review.ReviewedContentHash))
        {
            throw new InvalidOperationException($"Operational-context proposal cannot be promoted from status {proposal.Status}.");
        }

        OperationalContextProposal? latest = (await proposalStore.ListAsync(repository)).FirstOrDefault();
        if (latest is not null && !string.Equals(latest.ProposalId, proposal.ProposalId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cannot promote a superseded operational-context proposal.");
        }
    }

    private async Task<string?> ReadCurrentOperationalContextAsync(Repository repository)
    {
        return await artifactService.ExistsAsync(repository, CurrentOperationalContextPath)
            ? await artifactService.LoadAsync(repository, CurrentOperationalContextPath)
            : null;
    }

    private static string GetAcceptedContent(OperationalContextProposal proposal)
    {
        if (!string.IsNullOrWhiteSpace(proposal.EditedContentRelativePath))
        {
            return proposal.EditedContent
                ?? throw new InvalidOperationException("Cannot promote operational-context proposal because edited content is missing.");
        }

        return proposal.GeneratedContent
            ?? throw new InvalidOperationException("Cannot promote operational-context proposal because generated content is missing.");
    }

    private static string GetAcceptedContentSourceRelativePath(OperationalContextProposal proposal)
    {
        return !string.IsNullOrWhiteSpace(proposal.EditedContentRelativePath)
            ? proposal.EditedContentRelativePath
            : proposal.GeneratedContentRelativePath;
    }

    private static OperationalContextProposal WithReviewState(
        OperationalContextProposal proposal,
        OperationalContextReviewState reviewState,
        string staleReason)
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
            Review = new OperationalContextReview
            {
                ProposalId = proposal.ProposalId,
                ReviewState = reviewState,
                BaselineCurrentContextHash = proposal.Review.BaselineCurrentContextHash,
                ReviewedContentHash = proposal.Review.ReviewedContentHash,
                ReviewedAt = DateTimeOffset.UtcNow,
                ReviewNote = proposal.Review.ReviewNote,
                StaleReason = staleReason
            },
            Promotion = proposal.Promotion
        };
    }

    private static OperationalContextProposal WithPromotionFailure(
        OperationalContextProposal proposal,
        Artifact? archivedArtifact,
        string? archiveFailureReason,
        string? writeFailureReason)
    {
        return WithPromotion(
            proposal,
            proposal.Status,
            new OperationalContextPromotion
            {
                ProposalId = proposal.ProposalId,
                PromotedContentHash = proposal.Review.ReviewedContentHash,
                PromotedContentSourceRelativePath = GetAcceptedContentSourceRelativePath(proposal),
                RevisionNumber = TryParseRevisionNumber(archivedArtifact?.Name),
                ArchivedRelativePath = archivedArtifact?.RelativePath,
                ArchiveFailureReason = archiveFailureReason,
                WriteFailureReason = writeFailureReason
            });
    }

    private static OperationalContextProposal WithPromotionSuccess(
        OperationalContextProposal proposal,
        string promotedContentHash,
        string promotedContentSourceRelativePath,
        Artifact? archivedArtifact)
    {
        return WithPromotion(
            proposal,
            OperationalContextProposalStatus.Promoted,
            new OperationalContextPromotion
            {
                ProposalId = proposal.ProposalId,
                PromotedAt = DateTimeOffset.UtcNow,
                PromotedContentHash = promotedContentHash,
                PromotedContentSourceRelativePath = promotedContentSourceRelativePath,
                RevisionNumber = TryParseRevisionNumber(archivedArtifact?.Name),
                ArchivedRelativePath = archivedArtifact?.RelativePath
            });
    }

    private static OperationalContextProposal WithPromotion(
        OperationalContextProposal proposal,
        OperationalContextProposalStatus status,
        OperationalContextPromotion promotion)
    {
        return new OperationalContextProposal
        {
            ProposalId = proposal.ProposalId,
            RepositoryId = proposal.RepositoryId,
            GeneratedAt = proposal.GeneratedAt,
            Status = status,
            InputFingerprints = proposal.InputFingerprints,
            BaselineCurrentContextHash = proposal.BaselineCurrentContextHash,
            GeneratedContentHash = proposal.GeneratedContentHash,
            GeneratedContentRelativePath = proposal.GeneratedContentRelativePath,
            EditedContentRelativePath = proposal.EditedContentRelativePath,
            SemanticChanges = proposal.SemanticChanges,
            CompressionSummary = proposal.CompressionSummary,
            Review = proposal.Review,
            Promotion = promotion
        };
    }

    private static int? TryParseRevisionNumber(string? fileName)
    {
        const string prefix = "operational_context.";
        const string suffix = ".md";
        if (fileName is null ||
            !fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return int.TryParse(fileName[prefix.Length..^suffix.Length], out int revision)
            ? revision
            : null;
    }

    private static string HashOptionalContent(string? content)
    {
        return HashContent(content ?? "<absent>");
    }

    private static string HashContent(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }
}
