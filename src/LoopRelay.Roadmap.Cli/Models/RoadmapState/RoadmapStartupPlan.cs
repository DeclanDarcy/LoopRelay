using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record RoadmapStartupPlan(
    RoadmapStartupAction Action,
    RoadmapPreflightRequirement PreflightRequirement,
    RoadmapState SourceState,
    string Reason,
    RoadmapOutcome? ReportOutcome = null);
