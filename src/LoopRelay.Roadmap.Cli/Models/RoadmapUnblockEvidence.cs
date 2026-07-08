using LoopRelay.Completion;

namespace LoopRelay.Roadmap.Cli;

internal sealed record RoadmapUnblockEvidence(
    string Path,
    string Kind,
    string Hash,
    string Status);
