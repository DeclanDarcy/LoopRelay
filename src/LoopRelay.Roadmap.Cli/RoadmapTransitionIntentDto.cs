namespace LoopRelay.Roadmap.Cli;

internal sealed record RoadmapTransitionIntentDto(
    string Intent,
    RoadmapState DispatchState,
    IReadOnlyList<string> EvidencePaths)
{
    public static RoadmapTransitionIntentDto FromDomain(RoadmapTransitionIntent intent) =>
        new(intent.Intent, intent.DispatchState, intent.EvidencePaths.ToArray());

    public RoadmapTransitionIntent ToDomain() => new(Intent, DispatchState, EvidencePaths.ToArray());
}
