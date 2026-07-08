namespace LoopRelay.Roadmap.Cli.Models.Transitions;

internal sealed record RoadmapTransitionIntent(
    string Intent,
    Primitives.State.RoadmapState DispatchState,
    IReadOnlyList<string> EvidencePaths)
{
    public static RoadmapTransitionIntent Empty(Primitives.State.RoadmapState dispatchState) => new("None", dispatchState, []);
}
