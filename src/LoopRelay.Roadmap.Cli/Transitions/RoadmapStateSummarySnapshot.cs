namespace LoopRelay.Roadmap.Cli;

internal sealed record RoadmapStateSummarySnapshot(
    IReadOnlyList<ArtifactStateRow> ActiveArtifacts,
    string LastDecisionId,
    int SplitFamiliesCount,
    ProjectionManifestCounts ProjectionManifestCounts);
