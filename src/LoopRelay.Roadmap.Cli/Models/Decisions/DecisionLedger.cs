namespace LoopRelay.Roadmap.Cli.Models.Decisions;

internal sealed record DecisionLedgerEntry(
    string DecisionId,
    DateTimeOffset Timestamp,
    Primitives.State.RoadmapState State,
    string Transition,
    string Prompt,
    string ProjectionPath,
    IReadOnlyList<string> InputArtifactPaths,
    IReadOnlyList<string> OutputArtifactPaths,
    string Decision,
    string Confidence,
    string RationaleExcerpt);
