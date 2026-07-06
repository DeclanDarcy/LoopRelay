using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Services;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Orchestration.Tests;

/// <summary>
/// m10 (B) process-leak certification — the DIRECT live-process asserter the gap analysis calls for. Drives a REAL
/// <see cref="AgentRuntime"/> + REAL <see cref="AgentSessionRegistry"/> through a <see cref="CountingProcessLauncher"/>
/// whose <see cref="CountingAgentProcess"/> instances bump a shared <see cref="LiveProcessCounter"/> on construction
/// and DisposeAsync. The certification claim: "no failed or cancelled run leaves orphaned Codex processes" — so
/// LiveProcessCount must return to 0 after EVERY terminal path. All current tests check a per-session boolean; NONE
/// counts live processes across terminal paths until now.
/// </summary>
public sealed class ProcessLeakDetectionTests
{
    private static AgentSessionSpec Spec(SessionRole role = SessionRole.Planning) => new(
        SessionIdentity.New(),
        Guid.NewGuid().ToString("D"),
        role,
        new SandboxProfile("workspace-write", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false),
        new EffortProfile(AgentEffortLevel.High, Identifier: "xhigh"),
        System.IO.Path.GetTempPath());

    private static (AgentRuntime Runtime, AgentSessionRegistry Registry, CountingProcessLauncher Launcher, LiveProcessCounter Counter)
        NewRuntime(string turnStatus = "completed")
    {
        var counter = new LiveProcessCounter();
        var launcher = new CountingProcessLauncher(counter, turnStatus);
        var registry = new AgentSessionRegistry();
        var runtime = new AgentRuntime(
            launcher, new CodexEventTurnBoundaryDetector(), new DeterministicAgentTokenEstimator(), registry);
        return (runtime, registry, launcher, counter);
    }

    // The orchestrator over a REAL IAgentRuntime (OrchestrationTestFactory only accepts the FakeAgentRuntime).
    private static RepositoryOrchestrator OrchestratorOver(IAgentRuntime runtime, FakeArtifactStore store) =>
        new(
            Guid.NewGuid().ToString("D"),
            runtime,
            store,
            OrchestrationTestFactory.Cache(),
            new FakePlanArtifactPublisher(),
            new FakeDecisionSessionRouter(),
            new OrchestrationFeatureFlags());

    // ---------------------------------------------------------------------------------------------------------
    // Held-open session terminal paths (open a real CodexAppServerSession over a counting process, run a turn,
    // then reach the terminal path; assert the live process count returns to 0).
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_failed_turn_then_close_returns_the_live_count_to_zero()
    {
        var (runtime, _, _, counter) = NewRuntime(turnStatus: "failed");

        IAgentSession session = await runtime.OpenSessionAsync(Spec());
        Assert.Equal(1, counter.Live);
        AgentTurnResult result = await session.RunTurnAsync("do work");
        Assert.Equal(AgentTurnState.Failed, result.State); // a failed turn does NOT auto-dispose the process
        Assert.Equal(1, counter.Live);

        await runtime.CloseSessionAsync(session);

        Assert.Equal(0, counter.Live);
        Assert.Equal(1, counter.Killed); // the entire-process-tree reap ran exactly once
    }

    [Fact]
    public async Task An_external_cancellation_token_does_not_strand_the_process_after_close()
    {
        var (runtime, _, _, counter) = NewRuntime();
        IAgentSession session = await runtime.OpenSessionAsync(Spec());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.RunTurnAsync("work", cancellationToken: cts.Token));

        // A turn cancel does not kill the process; closing the session must.
        Assert.Equal(1, counter.Live);
        await runtime.CloseSessionAsync(session);
        Assert.Equal(0, counter.Live);
    }

    [Fact]
    public async Task CancelAsync_kills_the_process_so_the_live_count_returns_to_zero()
    {
        var (runtime, _, _, counter) = NewRuntime();
        IAgentSession session = await runtime.OpenSessionAsync(Spec());
        await session.RunTurnAsync("work");
        Assert.Equal(1, counter.Live);

        await session.CancelAsync();   // CancelAsync disposes the process
        await runtime.CloseSessionAsync(session); // single-sited teardown is still idempotent

        Assert.Equal(0, counter.Live);
    }

    [Fact]
    public async Task AgentSessionRegistry_dispose_reaps_every_open_process()
    {
        var (runtime, registry, _, counter) = NewRuntime();
        await runtime.OpenSessionAsync(Spec(SessionRole.Planning));
        await runtime.OpenSessionAsync(Spec(SessionRole.Decision));
        Assert.Equal(2, counter.Live);

        await registry.DisposeAsync();

        Assert.Equal(0, counter.Live); // registry teardown reaped both
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public async Task The_duplicate_open_failure_window_disposes_the_just_launched_process()
    {
        // Pre-seed the registry key so the SECOND OpenSessionAsync's TryAdd fails; AgentRuntime must dispose the
        // process it just launched (so the live count returns to its prior value), then throw.
        var (runtime, registry, _, counter) = NewRuntime();
        AgentSessionSpec spec = Spec();
        var key = new AgentSessionKey(spec.RepositoryId, spec.SessionId);

        // First open succeeds and registers (1 live process).
        IAgentSession first = await runtime.OpenSessionAsync(spec);
        Assert.Equal(1, counter.Live);
        int before = counter.Live;

        // Second open with the SAME key: TryAdd fails -> the just-launched process must be disposed and the open throws.
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.OpenSessionAsync(spec));

        Assert.Equal(before, counter.Live); // the failed-open process was reaped; no net leak
        Assert.Equal(2, counter.Constructed); // two processes were launched
        Assert.Equal(1, counter.Killed);      // exactly one was killed (the failed-open one)

        await runtime.CloseSessionAsync(first);
        Assert.Equal(0, counter.Live);
        _ = registry; _ = key;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Orchestrator terminal paths: the orchestrator owns held-open planning + decision processes. Each terminal
    // path (ClosePlanningSession via Execute, CloseDecisionSession via Transfer recycle, orchestrator DisposeAsync)
    // must reap its processes. Driven through the REAL runtime so the orchestrator's CloseSessionAsync teardown is
    // exercised end-to-end against the counting processes.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ClosePlanningSession_reaps_the_held_open_planning_process()
    {
        var (runtime, _, _, counter) = NewRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        await using RepositoryOrchestrator orchestrator = OrchestratorOver(runtime, store);

        await orchestrator.EnsurePlanningSessionAsync(repository);
        Assert.Equal(1, counter.Live);

        // Execute Plan closes the planning session before the operational turns. A plan must exist on disk first.
        await store.WriteAsync(ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan), "PLAN");
        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;

        // The planning process was reaped by ClosePlanningSession (the execution one-shots launch + reap their own).
        Assert.False(orchestrator.HasPlanningSession);
        Assert.Equal(0, counter.Live);
    }

    [Fact]
    public async Task Orchestrator_dispose_reaps_held_open_planning_and_decision_processes()
    {
        var (runtime, _, _, counter) = NewRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestratorOver(runtime, store);

        await orchestrator.EnsurePlanningSessionAsync(repository);
        await orchestrator.EnsureDecisionSessionAsync(repository);
        Assert.Equal(2, counter.Live);

        await orchestrator.DisposeAsync();

        Assert.Equal(0, counter.Live); // both held-open processes reaped on dispose
    }

    [Fact]
    public async Task A_faulted_prompt_mid_turn_then_dispose_returns_the_live_count_to_zero()
    {
        // A prompt that throws mid-turn (the process dies) must not strand the process: the orchestrator's dispose
        // drain + single-sited teardown reaps it.
        var (runtime, _, launcher, counter) = NewRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestratorOver(runtime, store);

        IAgentSession session = await orchestrator.EnsurePlanningSessionAsync(repository);
        Assert.Equal(1, counter.Live);

        // Kill the process's output stream mid-flight, then run a turn that consequently faults.
        launcher.Last!.KillOutputStream();
        try
        {
            await session.RunTurnAsync("work").WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // The faulted turn surfaces an exception; the process handle is still owned by the orchestrator.
        }

        await orchestrator.DisposeAsync();

        Assert.Equal(0, counter.Live);
    }
}
