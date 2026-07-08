using LoopRelay.Roadmap.Cli.Primitives.Transitions;

namespace LoopRelay.Roadmap.Cli.Models.Transitions;

internal sealed record RoadmapTransitionSummaryDto(
    Primitives.State.RoadmapState From,
    Primitives.State.RoadmapState To,
    string Prompt,
    string Projection,
    string Output,
    string Decision,
    TransitionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt)
{
    public static RoadmapTransitionSummaryDto FromDomain(RoadmapTransitionSummary transition) =>
        new(
            transition.From,
            transition.To,
            transition.Prompt,
            transition.Projection,
            transition.Output,
            transition.Decision,
            transition.Status,
            transition.StartedAt,
            transition.CompletedAt);

    public RoadmapTransitionSummary ToDomain() =>
        new(From, To, Prompt, Projection, Output, Decision, Status, StartedAt, CompletedAt);
}
