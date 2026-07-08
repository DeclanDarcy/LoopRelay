namespace LoopRelay.Roadmap.Cli;

internal sealed record DecisionLedgerEntry(
    string DecisionId,
    DateTimeOffset Timestamp,
    RoadmapState State,
    string Transition,
    string Prompt,
    string ProjectionPath,
    IReadOnlyList<string> InputArtifactPaths,
    IReadOnlyList<string> OutputArtifactPaths,
    string Decision,
    string Confidence,
    string RationaleExcerpt);
