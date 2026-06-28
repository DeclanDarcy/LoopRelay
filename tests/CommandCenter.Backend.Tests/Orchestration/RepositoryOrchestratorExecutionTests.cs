using System.Linq;
using System.Text.Json;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using CommandCenter.Orchestration.Streaming;
using Microsoft.Extensions.Caching.Memory;

namespace CommandCenter.Backend.Tests.Orchestration;

/// <summary>
/// Execute Plan (m4): the bridge from authoring into operational execution. These cover the ordered
/// multi-write run — close planning, copy operational context, cache the plan, extract + verify
/// milestones, commit/push, start execution, and rotate the first handoff — plus every terminal-state
/// boundary the certification calls out (missing plan, failed extraction, no milestones, failed
/// commit/push, missing handoff) and the dispose/concurrency guarantees.
/// </summary>
public sealed class RepositoryOrchestratorExecutionTests
{
    private const string PlanBody = "PLAN BODY";

    [Fact]
    public async Task Execute_plan_runs_the_full_bridge_in_order()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var cache = OrchestrationTestFactory.Cache();
        var publisher = new FakePlanArtifactPublisher();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator =
            OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, cache: cache, publisher: publisher);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        Assert.True(orchestrator.HasPlanningSession);

        ScriptMilestoneExtraction(runtime, store, repository, "m1.md", "m2.md");
        ScriptStartExecution(runtime, store, repository, "HANDOFF V1");

        Task<List<OrchestratorStreamEvent>> drain = DrainUntilTerminalAsync(orchestrator.ExecutionStream);
        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;
        List<OrchestratorStreamEvent> events = await drain;

        // Planning process closed before the operational turns.
        Assert.False(orchestrator.HasPlanningSession);
        Assert.All(runtime.Sessions, session => Assert.True(session.Disposed));

        // Plan copied to operational context and cached under the active-run slot.
        Assert.Equal(PlanBody, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        Assert.True(cache.TryGetValue(OrchestrationCacheKeys.PlanRun(orchestrator.RepositoryId), out object? snapshot));
        Assert.Equal(PlanBody, Assert.IsType<ActiveRunSnapshot>(snapshot).Plan);

        // The two operational one-shots: ExtractMilestones (xhigh) then StartExecution (medium), both Operational + workspace-write.
        Assert.Equal(ExtractMilestones.Text, runtime.OneShotPrompts[0]);
        Assert.Equal(StartExecution.Render(PlanBody), runtime.OneShotPrompts[1]);
        Assert.Equal(SessionRole.OperationalExecution, runtime.OneShotSpecs[0].Role);
        Assert.True(runtime.OneShotSpecs[0].Sandbox.CanWriteWorkspace);
        Assert.Equal("xhigh", runtime.OneShotSpecs[0].Effort.Identifier);
        Assert.Equal(AgentEffortLevel.Medium, runtime.OneShotSpecs[1].Effort.Level);

        // Commit/push happened once with the planning + milestone artifacts.
        Assert.Equal(1, publisher.PublishCount);
        Assert.Equal("Author plan and extract milestones", publisher.Publications[0].Message);
        Assert.Contains(OrchestrationArtifactPaths.Plan, publisher.Publications[0].Paths);

        // Handoff rotated to handoff.0001.md; the live handoff is gone.
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff)));
        Assert.Equal("HANDOFF V1", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
        Assert.Equal("HANDOFF V1", orchestrator.CurrentHandoff);

        // Stream told the ordered story and ended with completed.
        Assert.Equal("run-started", events[0].Type);
        Assert.Contains(events, e => e.Type == "milestones-extracted");
        Assert.Contains(events, e => e.Type == "committed" && Field(e, "commitSha") == "commit-sha");
        Assert.Contains(events, e => e.Type == "lifecycle" && Field(e, "state") == nameof(PlanLifecycleState.ExecutingPlan));
        OrchestratorStreamEvent rotated = events.Single(e => e.Type == "handoff-rotated");
        Assert.Equal(OrchestrationArtifactPaths.HistoricalHandoff(1), Field(rotated, "path"));
        Assert.Equal("completed", events[^1].Type);
        Assert.Equal(OrchestrationArtifactPaths.HistoricalHandoff(1), Field(events[^1], "handoffPath"));
    }

    [Fact]
    public async Task Execute_plan_is_rejected_when_no_plan_exists()
    {
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.BeginExecutePlanAsync(OrchestrationTestFactory.Repository()));
    }

    // m10 (A): StartExecution opens its operational one-shot at Effort.Level == Medium with NO identifier override
    // (the governed medium tier), captured from the FakeAgentRuntime one-shot specs. ExtractMilestones stays xhigh.
    [Fact]
    public async Task Start_execution_one_shot_opens_with_medium_effort_and_no_identifier()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        ScriptMilestoneExtraction(runtime, store, repository, "m1.md");
        ScriptStartExecution(runtime, store, repository, "HANDOFF V1");

        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;

        // [0] = ExtractMilestones (xhigh), [1] = StartExecution (medium, no identifier).
        Assert.Equal("xhigh", runtime.OneShotSpecs[0].Effort.Identifier);
        Assert.Equal(AgentEffortLevel.Medium, runtime.OneShotSpecs[1].Effort.Level);
        Assert.Null(runtime.OneShotSpecs[1].Effort.Identifier);
    }

    // m10 (A): the ContinueExecution continuation one-shot likewise opens at Effort.Level == Medium with no
    // identifier — the same governed medium tier StartExecution uses.
    [Fact]
    public async Task Continue_execution_one_shot_opens_with_medium_effort_and_no_identifier()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        // Minimal continuation pre-state: cached plan, operational context (decision gate), first rotated handoff.
        orchestrator.RecordPlan(PlanBody);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), "CONTEXT");
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), "HANDOFF ONE");
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO")));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;

        // The continuation one-shot (the first/only one-shot here) opened at medium with no identifier.
        Assert.Equal(AgentEffortLevel.Medium, runtime.OneShotSpecs[0].Effort.Level);
        Assert.Null(runtime.OneShotSpecs[0].Effort.Identifier);
        Assert.Equal(ContinueExecution.Render(PlanBody, "HANDOFF ONE", "DECISIONS ONE"), runtime.OneShotPrompts[0]);
    }

    [Fact]
    public async Task Execute_plan_is_rejected_while_a_planning_turn_is_running()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime { TurnGate = gate.Task };
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);
        string planPath = Resolve(repository, OrchestrationArtifactPaths.Plan);
        await store.WriteAsync(planPath, PlanBody); // a plan exists on disk
        runtime.OnTurn = () => store.WriteAsync(planPath, PlanBody);

        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Roadmap = "r" });
        Assert.True(orchestrator.IsPlanningTurnActive);

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.BeginExecutePlanAsync(repository));

        gate.SetResult();
        await orchestrator.PlanningTurnTask;
    }

    [Fact]
    public async Task Execute_plan_publishes_failed_when_milestone_extraction_does_not_complete()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "extraction exit code 1"));

        OrchestratorStreamEvent terminal = await RunAndAwaitTerminalAsync(orchestrator, repository);

        Assert.Equal("failed", terminal.Type);
        // The failure sentence names the OPERATIONAL activity, not "planning" (the execution path has its
        // own phase-aware wording).
        Assert.Equal("The milestone extraction run failed.", Field(terminal, "reason"));
        Assert.Equal("extraction exit code 1", Field(terminal, "detail"));
    }

    [Fact]
    public async Task Execute_plan_publishes_failed_when_no_milestone_files_are_produced()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn()); // completes but writes no milestone files

        OrchestratorStreamEvent terminal = await RunAndAwaitTerminalAsync(orchestrator, repository);

        Assert.Equal("failed", terminal.Type);
        Assert.Contains("no .agents/milestones", Field(terminal, "reason"));
    }

    [Fact]
    public async Task Execute_plan_publishes_failed_when_commit_or_push_fails()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var publisher = new FakePlanArtifactPublisher { Result = PlanPublicationResult.Failed("remote rejected the push") };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator =
            OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, publisher: publisher);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        ScriptMilestoneExtraction(runtime, store, repository, "m1.md");
        ScriptStartExecution(runtime, store, repository, "HANDOFF");

        OrchestratorStreamEvent terminal = await RunAndAwaitTerminalAsync(orchestrator, repository);

        Assert.Equal("failed", terminal.Type);
        Assert.Contains("remote rejected the push", Field(terminal, "reason"));
        // Start execution never ran — only the extraction one-shot was issued.
        Assert.Single(runtime.OneShotPrompts);
    }

    [Fact]
    public async Task Execute_plan_publishes_failed_when_the_handoff_is_not_written()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        ScriptMilestoneExtraction(runtime, store, repository, "m1.md");
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn()); // start execution completes but writes no handoff

        OrchestratorStreamEvent terminal = await RunAndAwaitTerminalAsync(orchestrator, repository);

        Assert.Equal("failed", terminal.Type);
        Assert.Contains("handoff.md was not written", Field(terminal, "reason"));
    }

    [Fact]
    public async Task Execution_provenance_is_recorded_for_extract_milestones_and_start_execution()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        ScriptMilestoneExtraction(runtime, store, repository, "m1.md");
        ScriptStartExecution(runtime, store, repository, "HANDOFF");

        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;

        Assert.Equal(2, orchestrator.ExecutionProvenance.Count);

        PromptProvenance extract = orchestrator.ExecutionProvenance[0];
        Assert.Equal(nameof(ExtractMilestones), extract.PromptName);
        Assert.Equal(ExtractMilestones.SourceHash, extract.SourceHash);
        Assert.Equal(PromptSessionRole.OperationalExecution, extract.SessionRole);
        Assert.Equal("ExtractMilestones", extract.WorkflowPhase);
        Assert.Contains(OrchestrationArtifactPaths.Plan, extract.InputArtifactIdentities);
        Assert.Contains(OrchestrationArtifactPaths.OperationalContext, extract.InputArtifactIdentities);
        Assert.Equal(OrchestrationArtifactPaths.MilestonesDirectory, Assert.Single(extract.OutputArtifactIdentities));

        PromptProvenance start = orchestrator.ExecutionProvenance[1];
        Assert.Equal(nameof(StartExecution), start.PromptName);
        Assert.Equal(StartExecution.SourceHash, start.SourceHash);
        Assert.Equal("StartExecution", start.WorkflowPhase);
        Assert.Equal(OrchestrationArtifactPaths.OperationalContext, Assert.Single(start.InputArtifactIdentities));
        Assert.Equal(OrchestrationArtifactPaths.LiveHandoff, Assert.Single(start.OutputArtifactIdentities));
    }

    [Fact]
    public async Task A_planning_command_is_rejected_while_an_execution_run_is_active()
    {
        // The single repository-wide runState gate makes planning and execution mutually exclusive: a
        // write/revise cannot be claimed while an execution run holds the gate. This closes the
        // planning/execution TOCTOU where two separate flags let both run at once.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        runtime.OneShotGate = gate.Task; // park the execution run on its extraction one-shot

        await orchestrator.BeginExecutePlanAsync(repository);
        Assert.True(orchestrator.IsExecutionRunActive);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Roadmap = "again" }));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.BeginRevisePlanAsync(repository, new PlanReviseRequest { Feedback = "now" }));

        gate.SetResult();
        await orchestrator.ExecutionRunTask;
        Assert.False(orchestrator.IsExecutionRunActive);
    }

    [Fact]
    public async Task Execute_plan_is_rejected_after_a_run_has_rotated_a_handoff()
    {
        // Execute Plan is one-way: once a run has rotated a handoff into history, a second execute is
        // rejected (so it cannot re-commit, re-run operational turns, or clobber rotated handoffs).
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        ScriptMilestoneExtraction(runtime, store, repository, "m1.md");
        ScriptStartExecution(runtime, store, repository, "HANDOFF");
        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;

        Assert.True(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.BeginExecutePlanAsync(repository));
    }

    [Fact]
    public async Task A_second_execute_is_rejected_while_one_is_running()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        runtime.OneShotGate = gate.Task; // park the first run on its extraction one-shot

        await orchestrator.BeginExecutePlanAsync(repository);
        Assert.True(orchestrator.IsExecutionRunActive);

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.BeginExecutePlanAsync(repository));

        gate.SetResult();
        await orchestrator.ExecutionRunTask;
        Assert.False(orchestrator.IsExecutionRunActive);
    }

    [Fact]
    public async Task Dispose_drains_an_in_flight_execution_run_before_completing_the_streams()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        runtime.OneShotGate = gate.Task;

        await orchestrator.BeginExecutePlanAsync(repository);
        Assert.True(orchestrator.IsExecutionRunActive);

        Task dispose = orchestrator.DisposeAsync().AsTask();
        await Task.Delay(50);
        Assert.False(dispose.IsCompleted); // parked: Dispose is draining the gated in-flight run

        gate.SetResult();
        await dispose; // completes cleanly — no publish-after-complete throw

        Assert.True(orchestrator.IsDisposed);
        Assert.True(orchestrator.ExecutionStream.IsCompleted);
    }

    // ---- helpers ----

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    private static async Task WritePlanAsync(
        RepositoryOrchestrator orchestrator,
        FakeAgentRuntime runtime,
        FakeArtifactStore store,
        Repository repository)
    {
        string planPath = Resolve(repository, OrchestrationArtifactPaths.Plan);
        runtime.OnTurn = () => store.WriteAsync(planPath, PlanBody);
        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Roadmap = "r" });
        await orchestrator.PlanningTurnTask;
        runtime.OnTurn = null; // operational one-shots are driven by the OneShotTurns queue, not OnTurn
    }

    private static void ScriptMilestoneExtraction(
        FakeAgentRuntime runtime,
        FakeArtifactStore store,
        Repository repository,
        params string[] milestoneFileNames) =>
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: async () =>
        {
            foreach (string name in milestoneFileNames)
            {
                await store.WriteAsync(Resolve(repository, $"{OrchestrationArtifactPaths.MilestonesDirectory}/{name}"), "milestone");
            }
        }));

    private static void ScriptStartExecution(
        FakeAgentRuntime runtime,
        FakeArtifactStore store,
        Repository repository,
        string handoff) =>
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), handoff)));

    private static async Task<OrchestratorStreamEvent> RunAndAwaitTerminalAsync(
        RepositoryOrchestrator orchestrator,
        Repository repository)
    {
        Task<List<OrchestratorStreamEvent>> drain = DrainUntilTerminalAsync(orchestrator.ExecutionStream);
        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;
        List<OrchestratorStreamEvent> events = await drain;
        return events[^1];
    }

    private static async Task<List<OrchestratorStreamEvent>> DrainUntilTerminalAsync(OrchestratorStreamChannel stream)
    {
        var events = new List<OrchestratorStreamEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (OrchestratorStreamEvent streamEvent in stream.SubscribeAsync(0, cts.Token))
        {
            events.Add(streamEvent);
            if (streamEvent.Type is "completed" or "failed")
            {
                break;
            }
        }

        return events;
    }

    private static string? Field(OrchestratorStreamEvent streamEvent, string property) =>
        JsonDocument.Parse(streamEvent.Data).RootElement.GetProperty(property).GetString();
}
