using LoopRelay.Roadmap.Cli.Models.TransitionInputs;

namespace LoopRelay.Roadmap.Cli.Models.Transitions;

internal sealed record TransitionJournalRecord(
    string Event,
    string CorrelationId,
    DateTimeOffset Timestamp,
    Primitives.State.RoadmapState PreviousState,
    Primitives.State.RoadmapState AttemptedState,
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
