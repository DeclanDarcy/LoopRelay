namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record RoadmapUnblockEvidence(
    string Path,
    string Kind,
    string Hash,
    string Status);
