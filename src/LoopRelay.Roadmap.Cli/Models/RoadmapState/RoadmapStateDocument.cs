using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.Transitions;

namespace LoopRelay.Roadmap.Cli.Models.RoadmapState;

internal sealed record RoadmapStateDocument(
    Primitives.State.RoadmapState CurrentState,
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
