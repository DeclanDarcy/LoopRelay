namespace LoopRelay.Roadmap.Cli;

internal sealed record TransitionJournalRecord(
    string Event,
    string CorrelationId,
    DateTimeOffset Timestamp,
    RoadmapState PreviousState,
    RoadmapState AttemptedState,
    string Prompt,
    string Projection,
    string PromptContractKey,
    IReadOnlyDictionary<string, string> InputArtifactHashes,
    IReadOnlyList<string> OutputPaths,
    long DurationMilliseconds,
    string Result,
    string ParserDecision,
    string? ErrorMessage,
    TransitionInputSnapshot? InputSnapshot = null);
