using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using Microsoft.Extensions.Caching.Memory;

namespace CommandCenter.Orchestration.Tests;

public sealed class RepositoryOrchestratorTests
{
    [Fact]
    public async Task Plan_status_reports_PlanAuthoring_when_plan_is_missing()
    {
        var store = new FakeArtifactStore { ExistsResult = false };
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(store: store);
        Repository repository = OrchestrationTestFactory.Repository();

        PlanStatus status = await orchestrator.GetPlanStatusAsync(repository);

        Assert.False(status.PlanExists);
        Assert.Equal(PlanLifecycleState.PlanAuthoring, status.State);
    }

    [Fact]
    public async Task Plan_status_reports_ExecutingPlan_when_plan_exists()
    {
        var store = new FakeArtifactStore { ExistsResult = true };
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(store: store);
        Repository repository = OrchestrationTestFactory.Repository();

        PlanStatus status = await orchestrator.GetPlanStatusAsync(repository);

        Assert.True(status.PlanExists);
        Assert.Equal(PlanLifecycleState.ExecutingPlan, status.State);
    }

    [Fact]
    public async Task Plan_status_is_derived_from_the_durable_plan_artifact_not_a_live_handle()
    {
        // Certification: live process handles are not durable state; the durable projection
        // (plan existence) is reconstructed from the repository artifact, with no session open.
        var store = new FakeArtifactStore { ExistsResult = true };
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(store: store);
        Repository repository = OrchestrationTestFactory.Repository();

        await orchestrator.GetPlanStatusAsync(repository);

        Assert.False(orchestrator.HasPlanningSession);
        Assert.False(orchestrator.HasDecisionSession);
        Assert.Single(store.ExistsQueries);
        Assert.EndsWith(Path.Combine(".agents", "plan.md"), store.ExistsQueries[0]);
    }

    [Fact]
    public async Task Ensure_planning_session_opens_once_and_is_reused()
    {
        var runtime = new FakeAgentRuntime();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime);
        Repository repository = OrchestrationTestFactory.Repository();

        IAgentSession first = await orchestrator.EnsurePlanningSessionAsync(repository);
        IAgentSession second = await orchestrator.EnsurePlanningSessionAsync(repository);

        Assert.Same(first, second);
        Assert.Equal(1, runtime.OpenCount);
        Assert.True(orchestrator.HasPlanningSession);
    }

    [Fact]
    public async Task Planning_and_decision_sessions_carry_their_role_specific_sandbox_profiles()
    {
        var runtime = new FakeAgentRuntime();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime);
        Repository repository = OrchestrationTestFactory.Repository();

        await orchestrator.EnsurePlanningSessionAsync(repository);
        await orchestrator.EnsureDecisionSessionAsync(repository);

        AgentSessionSpec planning = runtime.OpenedSpecs.Single(spec => spec.Role == SessionRole.Planning);
        AgentSessionSpec decision = runtime.OpenedSpecs.Single(spec => spec.Role == SessionRole.Decision);

        Assert.True(planning.Sandbox.CanWriteWorkspace);
        Assert.False(planning.Sandbox.RequiresApproval);
        Assert.False(decision.Sandbox.CanWriteWorkspace);
        Assert.False(decision.Sandbox.RequiresApproval);
        Assert.Equal("xhigh", decision.Effort.Identifier);
    }

    [Fact]
    public void Advancing_iteration_reserves_the_plan_run_cache_key()
    {
        MemoryCache cache = OrchestrationTestFactory.Cache();
        string repositoryId = Guid.NewGuid().ToString("D");
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(repositoryId: repositoryId, cache: cache);

        orchestrator.RecordPlan("plan body");
        int iteration = orchestrator.AdvanceIteration();

        Assert.Equal(1, iteration);
        Assert.Equal("plan body", orchestrator.CachedPlan);
        Assert.True(cache.TryGetValue(OrchestrationCacheKeys.PlanRun(repositoryId), out object? snapshot));
        var run = Assert.IsType<ActiveRunSnapshot>(snapshot);
        Assert.Equal(1, run.Iteration);
        Assert.True(run.PlanCached);
    }

    [Fact]
    public async Task Dispose_completes_streams_disposes_sessions_and_clears_the_cache_slot()
    {
        var runtime = new FakeAgentRuntime();
        MemoryCache cache = OrchestrationTestFactory.Cache();
        string repositoryId = Guid.NewGuid().ToString("D");
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(repositoryId: repositoryId, runtime: runtime, cache: cache);
        Repository repository = OrchestrationTestFactory.Repository();

        await orchestrator.EnsurePlanningSessionAsync(repository);
        orchestrator.AdvanceIteration();

        await orchestrator.DisposeAsync();

        Assert.True(orchestrator.PlanningStream.IsCompleted);
        Assert.True(orchestrator.ExecutionStream.IsCompleted);
        Assert.True(orchestrator.DecisionStream.IsCompleted);
        Assert.All(runtime.Sessions, session => Assert.True(session.Disposed));
        Assert.False(cache.TryGetValue(OrchestrationCacheKeys.PlanRun(repositoryId), out _));
    }

    [Fact]
    public async Task Disposed_orchestrator_rejects_new_sessions()
    {
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator();
        await orchestrator.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => orchestrator.EnsurePlanningSessionAsync(OrchestrationTestFactory.Repository()));
    }

    [Fact]
    public async Task Dispose_is_idempotent()
    {
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator();

        await orchestrator.DisposeAsync();
        await orchestrator.DisposeAsync();
    }

    [Fact]
    public void The_three_lifecycle_streams_are_distinct_channels()
    {
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator();

        Assert.NotSame(orchestrator.PlanningStream, orchestrator.ExecutionStream);
        Assert.NotSame(orchestrator.PlanningStream, orchestrator.DecisionStream);
        Assert.NotSame(orchestrator.ExecutionStream, orchestrator.DecisionStream);
    }
}
