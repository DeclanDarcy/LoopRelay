using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.State;

namespace LoopRelay.Roadmap.Cli.Tests.Services.State;

public sealed class RoadmapStartupPlannerTests
{
    [Fact]
    public void No_persisted_state_requires_fresh_initialization_preflight()
    {
        RoadmapStartupPlan plan = new RoadmapStartupPlanner().Plan(null);

        Assert.Equal(RoadmapStartupAction.FreshInitialization, plan.Action);
        Assert.Equal(RoadmapPreflightRequirement.RequiredForInitialize, plan.PreflightRequirement);
        Assert.Equal(RoadmapState.CoreReady, plan.SourceState);
        Assert.Null(plan.ReportOutcome);
    }

    [Theory]
    [InlineData((int)RoadmapState.CoreReady)]
    [InlineData((int)RoadmapState.ActiveEpicReady)]
    [InlineData((int)RoadmapState.MilestoneSpecsReady)]
    public void Active_workflow_requires_resume_preflight(int stateValue)
    {
        RoadmapState state = (RoadmapState)stateValue;
        RoadmapStartupPlan plan = new RoadmapStartupPlanner().Plan(State(state));

        Assert.Equal(RoadmapStartupAction.ResumeActiveWorkflow, plan.Action);
        Assert.Equal(RoadmapPreflightRequirement.RequiredForResume, plan.PreflightRequirement);
        Assert.Equal(state, plan.SourceState);
        Assert.Null(plan.ReportOutcome);
    }

    [Fact]
    public void Blocked_workflow_is_reported_without_preflight()
    {
        RoadmapStartupPlan plan = new RoadmapStartupPlanner().Plan(State(RoadmapState.EvidenceBlocked));

        Assert.Equal(RoadmapStartupAction.ReportBlockedWorkflow, plan.Action);
        Assert.Equal(RoadmapPreflightRequirement.None, plan.PreflightRequirement);
        Assert.Equal(RoadmapOutcome.Paused, plan.ReportOutcome);
    }

    [Theory]
    [InlineData((int)RoadmapState.StrategicInvestigationRequired)]
    [InlineData((int)RoadmapState.RoadmapRevisionRequired)]
    [InlineData((int)RoadmapState.NoSuitableInitiative)]
    [InlineData((int)RoadmapState.EvidenceGathering)]
    [InlineData((int)RoadmapState.GenerateOperationalContext)]
    [InlineData((int)RoadmapState.OperationalContextReady)]
    [InlineData((int)RoadmapState.GenerateExecutionPrompt)]
    [InlineData((int)RoadmapState.ExecutionPromptReady)]
    [InlineData((int)RoadmapState.ExecutionLoop)]
    [InlineData((int)RoadmapState.ExecutionBlocked)]
    public void Paused_terminal_workflow_is_reported_without_preflight(int stateValue)
    {
        RoadmapState state = (RoadmapState)stateValue;
        RoadmapStartupPlan plan = new RoadmapStartupPlanner().Plan(State(state));

        Assert.Equal(RoadmapStartupAction.ReportTerminalWorkflow, plan.Action);
        Assert.Equal(RoadmapPreflightRequirement.None, plan.PreflightRequirement);
        Assert.Equal(RoadmapOutcome.Paused, plan.ReportOutcome);
    }

    [Fact]
    public void Completed_workflow_is_reported_without_preflight()
    {
        RoadmapStartupPlan plan = new RoadmapStartupPlanner().Plan(State(RoadmapState.Completed));

        Assert.Equal(RoadmapStartupAction.ReportCompletedWorkflow, plan.Action);
        Assert.Equal(RoadmapPreflightRequirement.None, plan.PreflightRequirement);
        Assert.Equal(RoadmapOutcome.Completed, plan.ReportOutcome);
    }

    [Fact]
    public void Failed_workflow_is_reported_without_preflight()
    {
        RoadmapStartupPlan plan = new RoadmapStartupPlanner().Plan(State(RoadmapState.Failed));

        Assert.Equal(RoadmapStartupAction.ReportFailedWorkflow, plan.Action);
        Assert.Equal(RoadmapPreflightRequirement.None, plan.PreflightRequirement);
        Assert.Equal(RoadmapOutcome.Failed, plan.ReportOutcome);
    }

    [Fact]
    public void Cancelled_workflow_requires_resume_preflight()
    {
        RoadmapStartupPlan plan = new RoadmapStartupPlanner().Plan(State(RoadmapState.Cancelled));

        Assert.Equal(RoadmapStartupAction.ResumeActiveWorkflow, plan.Action);
        Assert.Equal(RoadmapPreflightRequirement.RequiredForResume, plan.PreflightRequirement);
        Assert.Equal(RoadmapState.Cancelled, plan.SourceState);
    }

    private static RoadmapStateDocument State(RoadmapState state) =>
        new(
            state,
            [],
            new RoadmapTransitionSummary(state, state, "None", "None", "None", "Completed", TransitionStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            [],
            "None",
            0,
            0,
            new ProjectionManifestCounts(0, 0, 0),
            RoadmapTransitionIntent.Empty(state),
            [],
            []);
}
