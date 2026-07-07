using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapStartupPlannerTests
{
    [Fact]
    public void No_persisted_state_requires_fresh_initialization_preflight()
    {
        Cli.RoadmapStartupPlan plan = new Cli.RoadmapStartupPlanner().Plan(null);

        Assert.Equal(Cli.RoadmapStartupAction.FreshInitialization, plan.Action);
        Assert.Equal(Cli.RoadmapPreflightRequirement.RequiredForInitialize, plan.PreflightRequirement);
        Assert.Equal(Cli.RoadmapState.CoreReady, plan.SourceState);
        Assert.Null(plan.ReportOutcome);
    }

    [Theory]
    [InlineData((int)Cli.RoadmapState.CoreReady)]
    [InlineData((int)Cli.RoadmapState.ActiveEpicReady)]
    [InlineData((int)Cli.RoadmapState.MilestoneSpecsReady)]
    public void Active_workflow_requires_resume_preflight(int stateValue)
    {
        Cli.RoadmapState state = (Cli.RoadmapState)stateValue;
        Cli.RoadmapStartupPlan plan = new Cli.RoadmapStartupPlanner().Plan(State(state));

        Assert.Equal(Cli.RoadmapStartupAction.ResumeActiveWorkflow, plan.Action);
        Assert.Equal(Cli.RoadmapPreflightRequirement.RequiredForResume, plan.PreflightRequirement);
        Assert.Equal(state, plan.SourceState);
        Assert.Null(plan.ReportOutcome);
    }

    [Fact]
    public void Blocked_workflow_is_reported_without_preflight()
    {
        Cli.RoadmapStartupPlan plan = new Cli.RoadmapStartupPlanner().Plan(State(Cli.RoadmapState.EvidenceBlocked));

        Assert.Equal(Cli.RoadmapStartupAction.ReportBlockedWorkflow, plan.Action);
        Assert.Equal(Cli.RoadmapPreflightRequirement.None, plan.PreflightRequirement);
        Assert.Equal(Cli.RoadmapOutcome.Paused, plan.ReportOutcome);
    }

    [Theory]
    [InlineData((int)Cli.RoadmapState.StrategicInvestigationRequired)]
    [InlineData((int)Cli.RoadmapState.RoadmapRevisionRequired)]
    [InlineData((int)Cli.RoadmapState.NoSuitableInitiative)]
    [InlineData((int)Cli.RoadmapState.EvidenceGathering)]
    [InlineData((int)Cli.RoadmapState.GenerateOperationalContext)]
    [InlineData((int)Cli.RoadmapState.OperationalContextReady)]
    [InlineData((int)Cli.RoadmapState.GenerateExecutionPrompt)]
    [InlineData((int)Cli.RoadmapState.ExecutionPromptReady)]
    [InlineData((int)Cli.RoadmapState.ExecutionLoop)]
    [InlineData((int)Cli.RoadmapState.ExecutionBlocked)]
    public void Paused_terminal_workflow_is_reported_without_preflight(int stateValue)
    {
        Cli.RoadmapState state = (Cli.RoadmapState)stateValue;
        Cli.RoadmapStartupPlan plan = new Cli.RoadmapStartupPlanner().Plan(State(state));

        Assert.Equal(Cli.RoadmapStartupAction.ReportTerminalWorkflow, plan.Action);
        Assert.Equal(Cli.RoadmapPreflightRequirement.None, plan.PreflightRequirement);
        Assert.Equal(Cli.RoadmapOutcome.Paused, plan.ReportOutcome);
    }

    [Fact]
    public void Completed_workflow_is_reported_without_preflight()
    {
        Cli.RoadmapStartupPlan plan = new Cli.RoadmapStartupPlanner().Plan(State(Cli.RoadmapState.Completed));

        Assert.Equal(Cli.RoadmapStartupAction.ReportCompletedWorkflow, plan.Action);
        Assert.Equal(Cli.RoadmapPreflightRequirement.None, plan.PreflightRequirement);
        Assert.Equal(Cli.RoadmapOutcome.Completed, plan.ReportOutcome);
    }

    [Fact]
    public void Failed_workflow_is_reported_without_preflight()
    {
        Cli.RoadmapStartupPlan plan = new Cli.RoadmapStartupPlanner().Plan(State(Cli.RoadmapState.Failed));

        Assert.Equal(Cli.RoadmapStartupAction.ReportFailedWorkflow, plan.Action);
        Assert.Equal(Cli.RoadmapPreflightRequirement.None, plan.PreflightRequirement);
        Assert.Equal(Cli.RoadmapOutcome.Failed, plan.ReportOutcome);
    }

    [Fact]
    public void Cancelled_workflow_requires_resume_preflight()
    {
        Cli.RoadmapStartupPlan plan = new Cli.RoadmapStartupPlanner().Plan(State(Cli.RoadmapState.Cancelled));

        Assert.Equal(Cli.RoadmapStartupAction.ResumeActiveWorkflow, plan.Action);
        Assert.Equal(Cli.RoadmapPreflightRequirement.RequiredForResume, plan.PreflightRequirement);
        Assert.Equal(Cli.RoadmapState.Cancelled, plan.SourceState);
    }

    private static Cli.RoadmapStateDocument State(Cli.RoadmapState state) =>
        new(
            state,
            [],
            new Cli.RoadmapTransitionSummary(state, state, "None", "None", "None", "Completed", Cli.TransitionStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            [],
            "None",
            0,
            0,
            new Cli.ProjectionManifestCounts(0, 0, 0),
            Cli.RoadmapTransitionIntent.Empty(state),
            [],
            []);
}
