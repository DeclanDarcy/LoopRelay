namespace CommandCenter.Roadmap.Cli;

internal static class RoadmapWorkflowStateClassifier
{
    public static bool IsReportOnlyState(RoadmapState state) =>
        state == RoadmapState.EvidenceBlocked ||
        IsTerminalPauseState(state) ||
        state is RoadmapState.Completed or RoadmapState.Failed;

    public static bool IsTerminalPauseState(RoadmapState state) =>
        state is RoadmapState.StrategicInvestigationRequired
            or RoadmapState.RoadmapRevisionRequired
            or RoadmapState.NoSuitableInitiative
            or RoadmapState.EvidenceGathering
            or RoadmapState.ExecutionBlocked;

    public static RoadmapOutcome ReportOutcome(RoadmapState state) =>
        state switch
        {
            RoadmapState.Completed => RoadmapOutcome.Completed,
            RoadmapState.Failed => RoadmapOutcome.Failed,
            RoadmapState.EvidenceBlocked => RoadmapOutcome.Paused,
            _ when IsTerminalPauseState(state) => RoadmapOutcome.Paused,
            _ => RoadmapOutcome.Paused,
        };

    public static string ReportReason(RoadmapState state) =>
        state switch
        {
            RoadmapState.EvidenceBlocked => "Persisted roadmap state is blocked and requires intervention.",
            RoadmapState.Completed => "Persisted roadmap state is already completed.",
            RoadmapState.Failed => "Persisted roadmap state is failed and requires repair.",
            _ when IsTerminalPauseState(state) => $"Persisted roadmap state is paused at {state}.",
            _ => $"Persisted roadmap state {state} does not require execution preparation.",
        };
}
