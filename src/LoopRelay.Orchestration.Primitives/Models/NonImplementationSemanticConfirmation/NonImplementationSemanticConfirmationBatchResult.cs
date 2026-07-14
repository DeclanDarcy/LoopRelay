using LoopRelay.Orchestration.Models.NonImplementationLedger;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Models.NonImplementationSemanticConfirmation;

public sealed record NonImplementationSemanticConfirmationBatchResult(
    IReadOnlyList<NonImplementationReviewLedgerEntry> ConfirmedEntries,
    IReadOnlyList<NonImplementationReviewLedgerEntry> SkippedEntries,
    IReadOnlyList<NonImplementationArtifactClassification> IgnoredClassifications)
{
    public int ConfirmedCount => ConfirmedEntries.Count;

    public int SkippedCount => SkippedEntries.Count;

    public int IgnoredCount => IgnoredClassifications.Count;
}
