namespace LoopRelay.Roadmap.Cli.Models.RoadmapTracking;

internal sealed record RoadmapUnblockEvidence(
    string Path,
    string Kind,
    string Hash,
    string Status);
