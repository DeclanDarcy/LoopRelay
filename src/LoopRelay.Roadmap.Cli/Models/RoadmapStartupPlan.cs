namespace LoopRelay.Roadmap.Cli;

internal sealed record RoadmapStartupPlan(
    RoadmapStartupAction Action,
    RoadmapPreflightRequirement PreflightRequirement,
    RoadmapState SourceState,
    string Reason,
    RoadmapOutcome? ReportOutcome = null);
