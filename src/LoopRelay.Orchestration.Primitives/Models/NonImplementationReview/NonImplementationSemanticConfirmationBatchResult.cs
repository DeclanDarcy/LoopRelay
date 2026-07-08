namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationSemanticConfirmationBatchResult(
    IReadOnlyList<NonImplementationReviewLedgerEntry> ConfirmedEntries,
    IReadOnlyList<NonImplementationReviewLedgerEntry> SkippedEntries,
    IReadOnlyList<NonImplementationArtifactClassification> IgnoredClassifications)
{
    public int ConfirmedCount => ConfirmedEntries.Count;

    public int SkippedCount => SkippedEntries.Count;

    public int IgnoredCount => IgnoredClassifications.Count;
}
