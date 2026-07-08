using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;

namespace LoopRelay.Roadmap.Cli.Models.Transitions;

internal sealed record RoadmapStateSummarySnapshot(
    IReadOnlyList<ArtifactStateRow> ActiveArtifacts,
    string LastDecisionId,
    int SplitFamiliesCount,
    ProjectionManifestCounts ProjectionManifestCounts);
