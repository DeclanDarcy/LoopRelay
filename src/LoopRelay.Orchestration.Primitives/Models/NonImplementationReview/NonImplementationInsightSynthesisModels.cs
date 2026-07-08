namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationInsightSynthesisSource(
    string EntryId,
    string Path,
    string? ReviewedContentSha256,
    bool ReviewedFileDeleted,
    string? DeletedReviewedIdentity,
    NonImplementationSemanticDisposition SemanticDisposition);

public sealed record NonImplementationInsightSynthesisResult(
    string SynthesisPath,
    string SynthesisPromptSourceHash,
    bool Generated,
    bool ReusedExisting,
    bool SkippedNoConfirmedEntries,
    bool PreviousSynthesisWasStale,
    IReadOnlyList<NonImplementationInsightSynthesisSource> SourceEntries,
    string? Content);
