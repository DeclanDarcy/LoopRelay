namespace LoopRelay.Roadmap.Cli.Models.Transitions;

internal sealed record RoadmapTransitionIntentDto(
    string Intent,
    Primitives.State.RoadmapState DispatchState,
    IReadOnlyList<string> EvidencePaths)
{
    public static RoadmapTransitionIntentDto FromDomain(RoadmapTransitionIntent intent) =>
        new(intent.Intent, intent.DispatchState, intent.EvidencePaths.ToArray());

    public RoadmapTransitionIntent ToDomain() => new(Intent, DispatchState, EvidencePaths.ToArray());
}
