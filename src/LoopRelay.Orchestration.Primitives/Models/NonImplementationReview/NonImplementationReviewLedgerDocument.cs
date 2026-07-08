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

    public NonImplementationSynthesisDecisionMetadata? SynthesisDecision { get; init; }
}
