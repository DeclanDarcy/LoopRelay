namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationSemanticConfirmation(
    string LedgerEntryId,
    string CandidatePath,
    string? ReviewedContentSha256,
    bool ReviewedFileDeleted,
    string? DeletedReviewedIdentity,
    NonImplementationSemanticDisposition Disposition,
    string Rationale,
    IReadOnlyList<string> EvidenceExcerptsOrPathFacts,
    string? UncertaintyNote = null);

public sealed record NonImplementationSemanticConfirmationBatchResult(
    IReadOnlyList<NonImplementationReviewLedgerEntry> ConfirmedEntries,
    IReadOnlyList<NonImplementationReviewLedgerEntry> SkippedEntries,
    IReadOnlyList<NonImplementationArtifactClassification> IgnoredClassifications)
{
    public int ConfirmedCount => ConfirmedEntries.Count;

    public int SkippedCount => SkippedEntries.Count;

    public int IgnoredCount => IgnoredClassifications.Count;
}
