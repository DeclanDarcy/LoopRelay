namespace LoopRelay.Orchestration.Models.NonImplementationInsightSynthesis;

public sealed record NonImplementationInsightSynthesisResult(
    string SynthesisPath,
    string SynthesisPromptSourceHash,
    bool Generated,
    bool ReusedExisting,
    bool SkippedNoConfirmedEntries,
    bool PreviousSynthesisWasStale,
    IReadOnlyList<NonImplementationInsightSynthesisSource> SourceEntries,
    string? Content);
