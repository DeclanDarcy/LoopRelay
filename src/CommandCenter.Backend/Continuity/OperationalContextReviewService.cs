using System.Security.Cryptography;
using System.Text;
using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Continuity;

public sealed class OperationalContextReviewService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IOperationalContextParser parser,
    IUnderstandingDiffService diffService,
    IOperationalContextProposalStore proposalStore) : IOperationalContextReviewService
{
    private const string CurrentOperationalContextPath = ".agents/operational_context.md";

    public async Task<OperationalContextProposal> EditAsync(Guid repositoryId, string proposalId, string content)
    {
        var repository = await GetRepositoryAsync(repositoryId);
        var proposal = await GetRequiredProposalAsync(repository, proposalId, includeContent: false);
        EnsureReviewable(proposal);

        var currentContent = await ReadCurrentOperationalContextAsync(repository);
        var semanticChanges = diffService.Compare(
            parser.Parse(currentContent ?? string.Empty),
            parser.Parse(content));
        var contentHash = HashContent(content);

        return await proposalStore.UpdateAsync(
            repository,
            WithReview(
                proposal,
                OperationalContextProposalStatus.Edited,
                OperationalContextReviewState.Edited,
                contentHash,
                reviewNote: null,
                staleReason: null,
                semanticChanges),
            editedContent: content,
            includeContent: true);
    }

    public async Task<OperationalContextProposal> AcceptAsync(
        Guid repositoryId,
        string proposalId,
        string? reviewNote)
    {
        var repository = await GetRepositoryAsync(repositoryId);
        var proposal = await GetRequiredProposalAsync(repository, proposalId, includeContent: true);
        await EnsureLatestReviewableAsync(repository, proposal);

        var currentHash = HashOptionalContent(await ReadCurrentOperationalContextAsync(repository));
        if (!string.Equals(currentHash, proposal.BaselineCurrentContextHash, StringComparison.Ordinal))
        {
            var staleProposal = WithReview(
                proposal,
                proposal.Status,
                OperationalContextReviewState.Stale,
                ReviewedContentHash(proposal),
                reviewNote,
                "Current operational context changed after this proposal was generated.",
                proposal.SemanticChanges);
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
                proposal.SemanticChanges),
            includeContent: true);
    }

    public async Task<OperationalContextProposal> RejectAsync(
        Guid repositoryId,
        string proposalId,
        string? reviewNote)
    {
        var repository = await GetRepositoryAsync(repositoryId);
        var proposal = await GetRequiredProposalAsync(repository, proposalId, includeContent: true);
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
                proposal.SemanticChanges),
            includeContent: true);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        var repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
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
        var latest = (await proposalStore.ListAsync(repository)).FirstOrDefault();
        if (latest is not null && !string.Equals(latest.ProposalId, proposal.ProposalId, StringComparison.Ordinal))
        {
            var staleProposal = WithReview(
                proposal,
                proposal.Status,
                OperationalContextReviewState.Stale,
                ReviewedContentHash(proposal),
                proposal.Review.ReviewNote,
                "Proposal was superseded by a newer generated proposal.",
                proposal.SemanticChanges);
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
        IReadOnlyList<OperationalContextSemanticChange> semanticChanges)
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
            CompressionSummary = proposal.CompressionSummary,
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
