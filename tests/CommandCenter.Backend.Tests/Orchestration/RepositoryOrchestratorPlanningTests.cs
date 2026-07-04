using System.Text.Json;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using CommandCenter.Orchestration.Streaming;

namespace CommandCenter.Backend.Tests.Orchestration;

public sealed class RepositoryOrchestratorPlanningTests
{
    [Fact]
    public async Task Write_plan_persists_epic_and_specs_before_the_planning_prompt_runs()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        string epicPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.SpecsEpic);
        string planPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan);

        bool epicExistedWhenTurnRan = false;
        runtime.OnTurn = async () =>
        {
            epicExistedWhenTurnRan = await store.ExistsAsync(epicPath);
            await store.WriteAsync(planPath, "PLAN BODY");
        };

        await orchestrator.BeginWritePlanAsync(
            repository,
            new PlanWriteRequest { Epic = "epic text", Specs = new[] { "spec one", "spec two" } });
        await orchestrator.PlanningTurnTask;

        Assert.True(epicExistedWhenTurnRan, "epic/specs must be written before the planning prompt runs");
        Assert.Equal("epic text", await store.ReadAsync(epicPath));
        Assert.Equal("spec one", await store.ReadAsync(ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Spec(1))));
        Assert.Equal("spec two", await store.ReadAsync(ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Spec(2))));
    }

    [Fact]
    public async Task Write_plan_selects_the_write_plan_prompt_when_not_a_new_codebase()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);
        runtime.OnTurn = () => store.WriteAsync(ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan), "PLAN");

        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r", NewCodebase = false });
        await orchestrator.PlanningTurnTask;

        Assert.Equal(WritePlan.Text, Assert.Single(runtime.Sessions).Prompts.Single());
    }

    [Fact]
    public async Task Write_plan_selects_the_write_plan_prompt_when_a_new_codebase()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);
        runtime.OnTurn = () => store.WriteAsync(ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan), "PLAN");

        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r", NewCodebase = true });
        await orchestrator.PlanningTurnTask;

        Assert.Equal(WritePlan.Text, Assert.Single(runtime.Sessions).Prompts.Single());
    }

    [Fact]
    public async Task Write_plan_streams_deltas_then_completes_with_the_rendered_plan()
    {
        var runtime = new FakeAgentRuntime { ScriptedChunks = new[] { "Plan part A ", "Plan part B" } };
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);
        runtime.OnTurn = () => store.WriteAsync(ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan), "RENDERED PLAN");

        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        await orchestrator.PlanningTurnTask;

        List<OrchestratorStreamEvent> events = await DrainAsync(orchestrator.PlanningStream, expectedCount: 4);

        Assert.Equal("turn-started", events[0].Type);
        Assert.Equal("WritePlan", Field(events[0], "phase"));
        Assert.Equal("delta", events[1].Type);
        Assert.Equal("Plan part A ", Field(events[1], "text"));
        Assert.Equal("delta", events[2].Type);
        Assert.Equal("Plan part B", Field(events[2], "text"));
        Assert.Equal("completed", events[3].Type);
        Assert.Equal("RENDERED PLAN", Field(events[3], "plan"));
        Assert.Equal("RENDERED PLAN", orchestrator.CachedPlan);
    }

    [Fact]
    public async Task Write_plan_publishes_failed_when_the_plan_artifact_is_not_written()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);
        // No OnTurn — the turn "completes" but Codex never writes .agents/plan.md.

        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        await orchestrator.PlanningTurnTask;

        List<OrchestratorStreamEvent> events = await DrainAsync(orchestrator.PlanningStream, expectedCount: 2);

        Assert.Equal("turn-started", events[0].Type);
        Assert.Equal("failed", events[1].Type);
        Assert.Null(orchestrator.CachedPlan);
    }

    [Fact]
    public async Task Write_plan_requires_a_non_empty_epic()
    {
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator();

        await Assert.ThrowsAsync<ArgumentException>(
            () => orchestrator.BeginWritePlanAsync(OrchestrationTestFactory.Repository(), new PlanWriteRequest { Epic = "   " }));
    }

    [Fact]
    public async Task Revise_plan_reuses_the_warm_planning_session()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);
        string planPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan);

        runtime.OnTurn = () => store.WriteAsync(planPath, "PLAN V1");
        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        await orchestrator.PlanningTurnTask;

        runtime.OnTurn = () => store.WriteAsync(planPath, "PLAN V2");
        await orchestrator.BeginRevisePlanAsync(repository, new PlanReviseRequest { Feedback = "tighten scope" });
        await orchestrator.PlanningTurnTask;

        FakeAgentSession session = Assert.Single(runtime.Sessions);
        Assert.Equal(1, runtime.OpenCount);
        Assert.Equal(2, session.Prompts.Count);
        Assert.Equal(RevisePlan.Render("tighten scope"), session.Prompts[1]);
        Assert.Equal("PLAN V2", orchestrator.CachedPlan);
    }

    [Fact]
    public async Task Revise_plan_without_a_warm_session_is_rejected()
    {
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.BeginRevisePlanAsync(OrchestrationTestFactory.Repository(), new PlanReviseRequest { Feedback = "x" }));
    }

    [Fact]
    public async Task Revise_plan_requires_non_empty_feedback()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);
        runtime.OnTurn = () => store.WriteAsync(ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan), "PLAN");
        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        await orchestrator.PlanningTurnTask;

        await Assert.ThrowsAsync<ArgumentException>(
            () => orchestrator.BeginRevisePlanAsync(repository, new PlanReviseRequest { Feedback = "" }));
    }

    [Fact]
    public async Task Planning_provenance_is_recorded_for_the_initial_plan_and_each_revision()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);
        string planPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan);
        runtime.OnTurn = () => store.WriteAsync(planPath, "PLAN");

        await orchestrator.BeginWritePlanAsync(
            repository,
            new PlanWriteRequest { Epic = "r", Specs = new[] { "a", "b" }, NewCodebase = true });
        await orchestrator.PlanningTurnTask;
        await orchestrator.BeginRevisePlanAsync(repository, new PlanReviseRequest { Feedback = "more detail" });
        await orchestrator.PlanningTurnTask;

        Assert.Equal(2, orchestrator.PlanningProvenance.Count);

        PromptProvenance write = orchestrator.PlanningProvenance[0];
        Assert.Equal(nameof(WritePlan), write.PromptName);
        Assert.Equal(WritePlan.SourceHash, write.SourceHash);
        Assert.Equal(PromptSessionRole.Planning, write.SessionRole);
        Assert.Equal("WritePlan", write.WorkflowPhase);
        Assert.Contains(OrchestrationArtifactPaths.SpecsEpic, write.InputArtifactIdentities);
        // Spec inputs are 1-based, mirroring the persisted s1.md/s2.md paths.
        Assert.Contains(OrchestrationArtifactPaths.Spec(1), write.InputArtifactIdentities);
        Assert.Contains(OrchestrationArtifactPaths.Spec(2), write.InputArtifactIdentities);
        Assert.Equal(OrchestrationArtifactPaths.Plan, Assert.Single(write.OutputArtifactIdentities));

        PromptProvenance revise = orchestrator.PlanningProvenance[1];
        Assert.Equal(nameof(RevisePlan), revise.PromptName);
        Assert.Equal(RevisePlan.SourceHash, revise.SourceHash);
        Assert.Equal("RevisePlan", revise.WorkflowPhase);
        Assert.Equal(OrchestrationArtifactPaths.Plan, Assert.Single(revise.InputArtifactIdentities));
        Assert.Equal(OrchestrationArtifactPaths.Plan, Assert.Single(revise.OutputArtifactIdentities));
    }

    [Fact]
    public async Task A_second_planning_turn_is_rejected_while_one_is_running()
    {
        // Certification: a second authoring command cannot race the warm process (ClaimPlanningTurn 409).
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime { TurnGate = gate.Task };
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);
        string planPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan);
        runtime.OnTurn = () => store.WriteAsync(planPath, "PLAN");

        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        Assert.True(orchestrator.IsPlanningTurnActive);

        // A concurrent write AND a concurrent revise are both rejected while the first turn is parked.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "again" }));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.BeginRevisePlanAsync(repository, new PlanReviseRequest { Feedback = "now" }));

        gate.SetResult();
        await orchestrator.PlanningTurnTask;
        Assert.False(orchestrator.IsPlanningTurnActive);

        // The claim is released after completion, so a fresh turn reuses the warm session.
        await orchestrator.BeginRevisePlanAsync(repository, new PlanReviseRequest { Feedback = "ok now" });
        await orchestrator.PlanningTurnTask;
        Assert.Equal(1, runtime.OpenCount);
        Assert.Equal(2, Assert.Single(runtime.Sessions).Prompts.Count);
    }

    [Fact]
    public async Task Dispose_drains_an_in_flight_planning_turn_before_completing_the_streams()
    {
        // Certification: no planning work outlives disposal — Dispose cancels, then DRAINS the in-flight
        // turn, then completes the streams (so a late delta can never hit a completed channel).
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime { TurnGate = gate.Task };
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        Assert.True(orchestrator.IsPlanningTurnActive);

        Task dispose = orchestrator.DisposeAsync().AsTask();
        await Task.Delay(50);
        Assert.False(dispose.IsCompleted); // parked: Dispose is draining the gated in-flight turn

        gate.SetResult();
        await dispose; // completes cleanly — no publish-after-complete throw

        Assert.True(orchestrator.IsDisposed);
        Assert.True(orchestrator.PlanningStream.IsCompleted);
        Assert.All(runtime.Sessions, session => Assert.True(session.Disposed));
    }

    [Fact]
    public async Task A_non_completed_turn_publishes_a_human_reason_and_the_output_as_detail()
    {
        var runtime = new FakeAgentRuntime { TurnState = AgentTurnState.Failed, TurnOutput = "exit code 1" };
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        await orchestrator.PlanningTurnTask;

        List<OrchestratorStreamEvent> events = await DrainAsync(orchestrator.PlanningStream, expectedCount: 2);

        Assert.Equal("failed", events[1].Type);
        Assert.Equal("The planning agent run failed.", Field(events[1], "reason"));
        Assert.Equal("exit code 1", Field(events[1], "detail"));
        Assert.Null(orchestrator.CachedPlan);
    }

    private static async Task<List<OrchestratorStreamEvent>> DrainAsync(OrchestratorStreamChannel stream, int expectedCount)
    {
        var events = new List<OrchestratorStreamEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (OrchestratorStreamEvent streamEvent in stream.SubscribeAsync(0, cts.Token))
        {
            events.Add(streamEvent);
            if (events.Count >= expectedCount)
            {
                break;
            }
        }

        return events;
    }

    private static string? Field(OrchestratorStreamEvent streamEvent, string property) =>
        JsonDocument.Parse(streamEvent.Data).RootElement.GetProperty(property).GetString();
}
