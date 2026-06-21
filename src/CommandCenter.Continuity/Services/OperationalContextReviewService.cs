using System.Security.Cryptography;
using System.Text;
using CommandCenter.Continuity.Abstractions;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Continuity.Services;

public sealed class OperationalContextReviewService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IOperationalContextParser parser,
    IUnderstandingDiffService diffService,
    IUnderstandingCompressionService compressionService,
    IOperationalContextProposalStore proposalStore) : IOperationalContextReviewService
{
    private const string CurrentOperationalContextPath = ".agents/operational_context.md";

    public async Task<OperationalContextProposal> EditAsync(Guid repositoryId, string proposalId, string content)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        OperationalContextProposal proposal = await GetRequiredProposalAsync(repository, proposalId, includeContent: false);
        EnsureReviewable(proposal);

        string? currentContent = await ReadCurrentOperationalContextAsync(repository);
        OperationalContextDocument currentDocument = parser.Parse(currentContent ?? string.Empty);
        OperationalContextDocument editedDocument = parser.Parse(content);
        IReadOnlyList<OperationalContextSemanticChange> semanticChanges = diffService.Compare(currentDocument, editedDocument);
        OperationalContextCompressionResult compression = compressionService.Compress(currentDocument, editedDocument);
        string contentHash = HashContent(content);

        return await proposalStore.UpdateAsync(
            repository,
            WithReview(
                proposal,
                OperationalContextProposalStatus.Edited,
                OperationalContextReviewState.Edited,
                contentHash,
                reviewNote: null,
                staleReason: null,
                semanticChanges,
                compression.Summary),
            editedContent: content,
            includeContent: true);
    }

    public async Task<OperationalContextProposal> AcceptAsync(
        Guid repositoryId,
        string proposalId,
        string? reviewNote)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        OperationalContextProposal proposal = await GetRequiredProposalAsync(repository, proposalId, includeContent: true);
        await EnsureLatestReviewableAsync(repository, proposal);

        string currentHash = HashOptionalContent(await ReadCurrentOperationalContextAsync(repository));
        if (!string.Equals(currentHash, proposal.BaselineCurrentContextHash, StringComparison.Ordinal))
        {
            OperationalContextProposal staleProposal = WithReview(
                proposal,
                proposal.Status,
                OperationalContextReviewState.Stale,
                ReviewedContentHash(proposal),
                reviewNote,
                "Current operational context changed after this proposal was generated.",
                proposal.SemanticChanges,
                proposal.CompressionSummary);
            await proposalStore.UpdateAsync(repository, staleProposal);
            throw new InvalidOperationException("Cannot accept a stale operational-context proposal because current context changed after generation.");
        }

        return await proposalStore.UpdateAsync(
            repository,
            WithReview(
                proposal,
                OperationalContextProposalStatus.Accepted,
                OperationalContextReviewState.Accepted,
                ReviewedContentHash(proposal),
                reviewNote,
                staleReason: null,
                proposal.SemanticChanges,
                proposal.CompressionSummary),
            includeContent: true);
    }

    public async Task<OperationalContextProposal> RejectAsync(
        Guid repositoryId,
        string proposalId,
        string? reviewNote)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        OperationalContextProposal proposal = await GetRequiredProposalAsync(repository, proposalId, includeContent: true);
        await EnsureLatestReviewableAsync(repository, proposal);

        return await proposalStore.UpdateAsync(
            repository,
            WithReview(
                proposal,
                OperationalContextProposalStatus.Rejected,
                OperationalContextReviewState.Rejected,
                ReviewedContentHash(proposal),
                reviewNote,
                staleReason: null,
                proposal.SemanticChanges,
                proposal.CompressionSummary),
            includeContent: true);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<OperationalContextProposal> GetRequiredProposalAsync(
        Repository repository,
        string proposalId,
        bool includeContent)
    {
        return await proposalStore.GetAsync(repository, proposalId, includeContent)
            ?? throw new KeyNotFoundException($"Operational-context proposal was not found: {proposalId}");
    }

    private async Task EnsureLatestReviewableAsync(Repository repository, OperationalContextProposal proposal)
    {
        EnsureReviewable(proposal);
        OperationalContextProposal? latest = (await proposalStore.ListAsync(repository)).FirstOrDefault();
        if (latest is not null && !string.Equals(latest.ProposalId, proposal.ProposalId, StringComparison.Ordinal))
        {
            OperationalContextProposal staleProposal = WithReview(
                proposal,
                proposal.Status,
                OperationalContextReviewState.Stale,
                ReviewedContentHash(proposal),
                proposal.Review.ReviewNote,
                "Proposal was superseded by a newer generated proposal.",
                proposal.SemanticChanges,
                proposal.CompressionSummary);
            await proposalStore.UpdateAsync(repository, staleProposal);
            throw new InvalidOperationException("Cannot review a superseded operational-context proposal.");
        }
    }

    private static void EnsureReviewable(OperationalContextProposal proposal)
    {
        if (proposal.Status is not OperationalContextProposalStatus.Pending and not OperationalContextProposalStatus.Edited)
        {
            throw new InvalidOperationException($"Operational-context proposal is not reviewable from status {proposal.Status}.");
        }
    }

    private async Task<string?> ReadCurrentOperationalContextAsync(Repository repository)
    {
        return await artifactService.ExistsAsync(repository, CurrentOperationalContextPath)
            ? await artifactService.LoadAsync(repository, CurrentOperationalContextPath)
            : null;
    }

    private static OperationalContextProposal WithReview(
        OperationalContextProposal proposal,
        OperationalContextProposalStatus status,
        OperationalContextReviewState reviewState,
        string? reviewedContentHash,
        string? reviewNote,
        string? staleReason,
        IReadOnlyList<OperationalContextSemanticChange> semanticChanges,
        OperationalContextCompressionSummary compressionSummary)
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
            SemanticChanges = semanticChanges,
            CompressionSummary = compressionSummary,
            Promotion = proposal.Promotion,
            Review = new OperationalContextReview
            {
                ProposalId = proposal.ProposalId,
                ReviewState = reviewState,
                BaselineCurrentContextHash = proposal.BaselineCurrentContextHash,
                ReviewedContentHash = reviewedContentHash,
                ReviewedAt = DateTimeOffset.UtcNow,
                ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote,
                StaleReason = staleReason
            }
        };
    }

    private static string ReviewedContentHash(OperationalContextProposal proposal)
    {
        if (!string.IsNullOrWhiteSpace(proposal.EditedContent))
        {
            return HashContent(proposal.EditedContent);
        }

        return proposal.GeneratedContentHash;
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
