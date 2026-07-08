using LoopRelay.Orchestration.Models.NonImplementationInsightSynthesis;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Models.NonImplementationLedger;

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
