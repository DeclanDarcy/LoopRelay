using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record RoadmapTransitionIntent(
    string Intent,
    RoadmapState DispatchState,
    IReadOnlyList<string> EvidencePaths)
{
    public static RoadmapTransitionIntent Empty(RoadmapState dispatchState) => new("None", dispatchState, []);
}
