using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record RoadmapTransitionIntentDto(
    string Intent,
    RoadmapState DispatchState,
    IReadOnlyList<string> EvidencePaths)
{
    public static RoadmapTransitionIntentDto FromDomain(RoadmapTransitionIntent intent) =>
        new(intent.Intent, intent.DispatchState, intent.EvidencePaths.ToArray());

    public RoadmapTransitionIntent ToDomain() => new(Intent, DispatchState, EvidencePaths.ToArray());
}
