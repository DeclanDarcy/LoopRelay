namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationInsightSynthesisResult(
    string SynthesisPath,
    string SynthesisPromptSourceHash,
    bool Generated,
    bool ReusedExisting,
    bool SkippedNoConfirmedEntries,
    bool PreviousSynthesisWasStale,
    IReadOnlyList<NonImplementationInsightSynthesisSource> SourceEntries,
    string? Content);
