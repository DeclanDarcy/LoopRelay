using LoopRelay.Roadmap.Cli.Primitives.State;

namespace LoopRelay.Roadmap.Cli.Models.RoadmapState;

internal sealed record RoadmapStartupPlan(
    RoadmapStartupAction Action,
    RoadmapPreflightRequirement PreflightRequirement,
    Primitives.State.RoadmapState SourceState,
    string Reason,
    RoadmapOutcome? ReportOutcome = null);
