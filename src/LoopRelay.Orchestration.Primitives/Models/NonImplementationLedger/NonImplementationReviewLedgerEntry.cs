using LoopRelay.Orchestration.Primitives.NonImplementationReview;

namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationReviewLedgerEntry
{
    public string EntryId { get; init; } = string.Empty;

    public string? ExecutionSliceId { get; init; }

    public string? DiscoveryContext { get; init; }

    public string Path { get; init; } = string.Empty;

    public string? PreviousPath { get; init; }

    public string? BaselineStatus { get; init; }

    public string? PostStatus { get; init; }

    public string? ReviewedContentSha256 { get; init; }

    public bool ReviewedFileDeleted { get; init; }

    public string? BaselineContentSha256 { get; init; }

    public bool PreExisted { get; init; }

    public NonImplementationArtifactRoute Route { get; init; }

    public string ClassificationRuleId { get; init; } = string.Empty;

    public string ClassificationRationale { get; init; } = string.Empty;

    public IReadOnlyList<string> ClassificationPathFacts { get; init; } = Array.Empty<string>();

    public string ClassifierVersion { get; init; } = string.Empty;

    public NonImplementationSemanticDisposition? SemanticDisposition { get; init; }

    public string? SemanticRationale { get; init; }

    public IReadOnlyList<string> SemanticEvidence { get; init; } = Array.Empty<string>();

    public string? SemanticUncertaintyNote { get; init; }

    public string ConfirmationPromptSourceHash { get; init; } = string.Empty;

    public DateTimeOffset FirstSeenAtUtc { get; init; }

    public DateTimeOffset LastSeenAtUtc { get; init; }

    public NonImplementationHitlProvenanceKind HitlProvenanceKind { get; init; }

    public string? HitlProvenanceEvidencePath { get; init; }

    public string? HitlProvenanceEvidenceExcerpt { get; init; }

    public string? HitlProvenanceSourceHash { get; init; }

    public string? HitlProvenanceRationale { get; init; }

    public NonImplementationResolutionState ResolutionState { get; init; }

    public NonImplementationHumanDecisionMetadata? HumanDecision { get; init; }
}
