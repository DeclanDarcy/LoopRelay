namespace LoopRelay.Roadmap.Cli;

internal sealed record RoadmapTransitionIntent(
    string Intent,
    RoadmapState DispatchState,
    IReadOnlyList<string> EvidencePaths)
{
    public static RoadmapTransitionIntent Empty(RoadmapState dispatchState) => new("None", dispatchState, []);
}
