using CommandCenter.Continuity.Primitives;

namespace CommandCenter.Continuity.Models;

public sealed class OperationalContextProposal
{
    public string ProposalId { get; init; } = string.Empty;

    public Guid RepositoryId { get; init; }

    public DateTimeOffset GeneratedAt { get; init; }

    public OperationalContextProposalStatus Status { get; init; } = OperationalContextProposalStatus.Pending;

    public IReadOnlyList<OperationalContextInputFingerprint> InputFingerprints { get; init; } = [];

    public string BaselineCurrentContextHash { get; init; } = string.Empty;

    public string GeneratedContentHash { get; init; } = string.Empty;

    public string GeneratedContentRelativePath { get; init; } = string.Empty;

    public string? EditedContentRelativePath { get; init; }

    public IReadOnlyList<OperationalContextSemanticChange> SemanticChanges { get; init; } = [];

    public OperationalContextCompressionSummary CompressionSummary { get; init; } = new();

    public OperationalContextReview Review { get; init; } = new();

    public OperationalContextPromotion Promotion { get; init; } = new();

    public string? GeneratedContent { get; init; }

    public string? EditedContent { get; init; }
}
