namespace LoopRelay.Roadmap.Cli;

internal sealed class RoadmapStatusTransition(
    RoadmapStateStore stateStore,
    RoadmapStartupPlanner startupPlanner)
{
    public async Task<RoadmapStatusExecution> ExecuteAsync(
        ILoopConsole console,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RoadmapStateDocument? persistedState = await stateStore.LoadReadOnlyAsync();
        RoadmapStatusExecution execution = CreateExecution(persistedState, startupPlanner);
        execution.EmitTo(console);
        return execution;
    }

    public static RoadmapStatusExecution CreateExecution(
        RoadmapStateDocument? persistedState,
        RoadmapStartupPlanner startupPlanner)
    {
        if (persistedState is null)
        {
            RoadmapStartupPlan missingStatePlan = startupPlanner.Plan(null);
            return new RoadmapStatusExecution(
                missingStatePlan.ReportOutcome ?? RoadmapOutcome.Paused,
                new RoadmapStatusBehaviorFields(
                    "missing",
                    null,
                    missingStatePlan.Reason,
                    "None",
                    null,
                    [],
                    (missingStatePlan.ReportOutcome ?? RoadmapOutcome.Paused).ToString()),
                [new RoadmapStatusOutputLine("Info", "No persisted roadmap state exists.")]);
        }

        RoadmapStartupPlan startupPlan = startupPlanner.Plan(persistedState);
        RoadmapOutcome outcome = startupPlan.ReportOutcome ?? RoadmapOutcome.Paused;
        RoadmapStatusOutputLine[] lines =
        [
            new("Info", $"Status: {persistedState.CurrentState}. {startupPlan.Reason}"),
            new("Info", $"Transition intent: {persistedState.TransitionIntent.Intent} -> {persistedState.TransitionIntent.DispatchState}"),
            ..persistedState.Blockers.Select(blocker =>
                new RoadmapStatusOutputLine(
                    "Warn",
                    $"{blocker.Blocker} Required next step: {blocker.RequiredNextStep}")),
        ];

        return new RoadmapStatusExecution(
            outcome,
            new RoadmapStatusBehaviorFields(
                "present",
                persistedState.CurrentState.ToString(),
                startupPlan.Reason,
                persistedState.TransitionIntent.Intent,
                persistedState.TransitionIntent.DispatchState.ToString(),
                persistedState.Blockers.Select(blocker => new RoadmapStatusBlockerField(
                    blocker.Blocker,
                    blocker.RequiredNextStep)).ToArray(),
                outcome.ToString()),
            lines);
    }
}

internal sealed record RoadmapStatusExecution(
    RoadmapOutcome Outcome,
    RoadmapStatusBehaviorFields Behavior,
    IReadOnlyList<RoadmapStatusOutputLine> OutputLines)
{
    public void EmitTo(ILoopConsole console)
    {
        foreach (RoadmapStatusOutputLine line in OutputLines)
        {
            if (string.Equals(line.Level, "Warn", StringComparison.Ordinal))
            {
                console.Warn(line.Text);
            }
            else
            {
                console.Info(line.Text);
            }
        }
    }

    public string RenderConsoleOutput() =>
        string.Join(Environment.NewLine, OutputLines.Select(line => $"[{line.Level}] {line.Text}"));
}

internal sealed record RoadmapStatusOutputLine(
    string Level,
    string Text);

internal sealed record RoadmapStatusBehaviorFields(
    string StateCondition,
    string? CurrentState,
    string StartupPlanReason,
    string TransitionIntent,
    string? DispatchState,
    IReadOnlyList<RoadmapStatusBlockerField> Blockers,
    string TerminalOutcome);

internal sealed record RoadmapStatusBlockerField(
    string Blocker,
    string RequiredNextStep);
