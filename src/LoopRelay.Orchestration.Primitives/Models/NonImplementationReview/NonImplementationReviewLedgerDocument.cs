namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationReviewLedgerDocument(
    int SchemaVersion,
    IReadOnlyList<NonImplementationReviewLedgerEntry> Entries,
    IReadOnlyList<NonImplementationHitlRequestEntry> HitlRequests)
{
    public const int CurrentSchemaVersion = 1;

    public static NonImplementationReviewLedgerDocument Empty() =>
        new(
            CurrentSchemaVersion,
            Array.Empty<NonImplementationReviewLedgerEntry>(),
            Array.Empty<NonImplementationHitlRequestEntry>());
}

public sealed record NonImplementationReviewLedgerEntry(
    string EntryId,
    string Path,
    NonImplementationArtifactRoute Route,
    NonImplementationSemanticDisposition? SemanticDisposition,
    NonImplementationResolutionState ResolutionState,
    NonImplementationHitlProvenanceKind HitlProvenanceKind,
    string? HitlProvenanceEvidencePath = null,
    string? HitlProvenanceSourceHash = null,
    string? HitlProvenanceRationale = null);

public sealed record NonImplementationHitlRequestEntry(
    string DeliverablePathOrPattern,
    string SourceArtifactPath,
    string SourceHash,
    NonImplementationHitlProvenanceKind HitlProvenanceKind,
    string Rationale,
    DateTimeOffset FirstCapturedAtUtc);
