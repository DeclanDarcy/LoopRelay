using LoopRelay.Roadmap.Cli.Primitives.Transitions;

namespace LoopRelay.Roadmap.Cli.Models.Transitions;

internal sealed record RoadmapTransitionSummary(
    Primitives.State.RoadmapState From,
    Primitives.State.RoadmapState To,
    string Prompt,
    string Projection,
    string Output,
    string Decision,
    TransitionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);
