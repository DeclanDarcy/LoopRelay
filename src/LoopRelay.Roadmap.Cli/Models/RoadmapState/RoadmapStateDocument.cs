using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record RoadmapStateDocument(
    RoadmapState CurrentState,
    IReadOnlyList<ArtifactStateRow> ActiveArtifacts,
    RoadmapTransitionSummary LastTransition,
    IReadOnlyList<BlockerRow> Blockers,
    string LastDecisionId,
    int RetiredEpicsCount,
    int SplitFamiliesCount,
    ProjectionManifestCounts ProjectionManifestCounts,
    RoadmapTransitionIntent TransitionIntent,
    IReadOnlyList<string> NextValidTransitions,
    IReadOnlyList<RetiredEpic> RetiredEpics);
