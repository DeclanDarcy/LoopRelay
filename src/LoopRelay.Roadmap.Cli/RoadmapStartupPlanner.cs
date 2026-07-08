namespace LoopRelay.Roadmap.Cli;

internal sealed class RoadmapStartupPlanner
{
    public RoadmapStartupPlan Plan(RoadmapStateDocument? persistedState)
    {
        if (persistedState is null)
        {
            return new RoadmapStartupPlan(
                RoadmapStartupAction.FreshInitialization,
                RoadmapPreflightRequirement.RequiredForInitialize,
                RoadmapState.CoreReady,
                "No persisted roadmap state exists; startup must initialize the workflow.");
        }

        RoadmapState state = persistedState.CurrentState;
        if (state == RoadmapState.EvidenceBlocked)
        {
            return Report(
                RoadmapStartupAction.ReportBlockedWorkflow,
                state);
        }

        if (RoadmapWorkflowStateClassifier.IsTerminalPauseState(state))
        {
            return Report(
                RoadmapStartupAction.ReportTerminalWorkflow,
                state);
        }

        if (state == RoadmapState.Completed)
        {
            return Report(
                RoadmapStartupAction.ReportCompletedWorkflow,
                state);
        }

        if (state == RoadmapState.Failed)
        {
            return Report(
                RoadmapStartupAction.ReportFailedWorkflow,
                state);
        }

        return new RoadmapStartupPlan(
            RoadmapStartupAction.ResumeActiveWorkflow,
            RoadmapPreflightRequirement.RequiredForResume,
            state,
            state == RoadmapState.Cancelled
                ? "Persisted roadmap state is cancelled; recovery requires execution preflight before resume planning."
                : $"Persisted roadmap state {state} can resume after execution preflight.");
    }

    private static RoadmapStartupPlan Report(RoadmapStartupAction action, RoadmapState state) =>
        new(
            action,
            RoadmapPreflightRequirement.None,
            state,
            RoadmapWorkflowStateClassifier.ReportReason(state),
            RoadmapWorkflowStateClassifier.ReportOutcome(state));
}
