namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationReviewLedgerDocument(
    int SchemaVersion,
    IReadOnlyList<NonImplementationReviewLedgerEntry> Entries)
{
    public const int CurrentSchemaVersion = 1;

    public static NonImplementationReviewLedgerDocument Empty() =>
        new(CurrentSchemaVersion, Array.Empty<NonImplementationReviewLedgerEntry>());
}

public sealed record NonImplementationReviewLedgerEntry(
    string EntryId,
    string Path,
    NonImplementationArtifactRoute Route,
    NonImplementationSemanticDisposition? SemanticDisposition,
    NonImplementationResolutionState ResolutionState,
    NonImplementationHitlProvenanceKind HitlProvenanceKind);
