using CommandCenter.Agents.Abstractions;
using CommandCenter.Core.Artifacts;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Extensions;
using CommandCenter.Orchestration.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests.Orchestration;

public sealed class RepositoryOrchestratorRegistryTests
{
    [Fact]
    public async Task GetOrCreate_returns_the_same_orchestrator_for_one_repository()
    {
        RepositoryOrchestratorRegistry registry = OrchestrationTestFactory.Registry();
        string repositoryId = Guid.NewGuid().ToString("D");

        RepositoryOrchestrator first = await registry.GetOrCreateAsync(repositoryId);
        RepositoryOrchestrator second = await registry.GetOrCreateAsync(repositoryId);

        Assert.Same(first, second);
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public async Task Concurrent_GetOrCreate_for_one_repository_never_produces_duplicates()
    {
        // Certification: "Duplicate active orchestrators for one repository are impossible."
        RepositoryOrchestratorRegistry registry = OrchestrationTestFactory.Registry();
        string repositoryId = Guid.NewGuid().ToString("D");

        using var start = new Barrier(64);
        Task<RepositoryOrchestrator>[] races = Enumerable.Range(0, 64)
            .Select(_ => Task.Run(async () =>
            {
                start.SignalAndWait();
                return await registry.GetOrCreateAsync(repositoryId);
            }))
            .ToArray();

        RepositoryOrchestrator[] results = await Task.WhenAll(races);

        Assert.Single(results.Distinct());
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public async Task Different_repositories_get_distinct_orchestrators()
    {
        RepositoryOrchestratorRegistry registry = OrchestrationTestFactory.Registry();

        RepositoryOrchestrator a = await registry.GetOrCreateAsync(Guid.NewGuid().ToString("D"));
        RepositoryOrchestrator b = await registry.GetOrCreateAsync(Guid.NewGuid().ToString("D"));

        Assert.NotSame(a, b);
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public async Task RemoveAsync_disposes_the_orchestrator_and_its_live_sessions()
    {
        var runtime = new FakeAgentRuntime();
        RepositoryOrchestratorRegistry registry = OrchestrationTestFactory.Registry(runtime: runtime);
        string repositoryId = Guid.NewGuid().ToString("D");

        RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(repositoryId);
        await orchestrator.EnsurePlanningSessionAsync(OrchestrationTestFactory.Repository());

        bool removed = await registry.RemoveAsync(repositoryId);

        Assert.True(removed);
        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryGet(repositoryId, out _));
        Assert.All(runtime.Sessions, session => Assert.True(session.Disposed));
    }

    [Fact]
    public async Task RemoveAsync_of_an_unknown_repository_returns_false()
    {
        RepositoryOrchestratorRegistry registry = OrchestrationTestFactory.Registry();

        Assert.False(await registry.RemoveAsync(Guid.NewGuid().ToString("D")));
    }

    [Fact]
    public async Task A_teardown_in_flight_blocks_recreating_a_replacement_until_disposal_completes()
    {
        // Certification: EXACTLY ONE live orchestrator per repository "even under concurrency" — the
        // teardown path. Remove holds the registry gate across the orchestrator's full DisposeAsync,
        // so a concurrent GetOrCreate cannot publish a second live orchestrator for the same id while
        // the first is still tearing down its live Codex sessions.
        var disposeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime { SessionDisposeGate = disposeGate.Task };
        RepositoryOrchestratorRegistry registry = OrchestrationTestFactory.Registry(runtime: runtime);
        string repositoryId = Guid.NewGuid().ToString("D");

        RepositoryOrchestrator first = await registry.GetOrCreateAsync(repositoryId);
        await first.EnsurePlanningSessionAsync(OrchestrationTestFactory.Repository());

        Task<bool> removing = registry.RemoveAsync(repositoryId); // parks inside DisposeAsync on the gated session
        await Task.Delay(50);
        Assert.False(removing.IsCompleted);

        Task<RepositoryOrchestrator> recreating = registry.GetOrCreateAsync(repositoryId);
        await Task.Delay(50);
        Assert.False(recreating.IsCompleted); // blocked on the registry mutation gate held by RemoveAsync

        disposeGate.SetResult(); // let the old orchestrator finish disposing

        Assert.True(await removing);
        RepositoryOrchestrator second = await recreating;

        Assert.NotSame(first, second);
        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public async Task DisposeAsync_tears_down_every_orchestrator()
    {
        var runtime = new FakeAgentRuntime();
        RepositoryOrchestratorRegistry registry = OrchestrationTestFactory.Registry(runtime: runtime);

        RepositoryOrchestrator a = await registry.GetOrCreateAsync(Guid.NewGuid().ToString("D"));
        RepositoryOrchestrator b = await registry.GetOrCreateAsync(Guid.NewGuid().ToString("D"));
        await a.EnsureDecisionSessionAsync(OrchestrationTestFactory.Repository());
        await b.EnsurePlanningSessionAsync(OrchestrationTestFactory.Repository());

        await registry.DisposeAsync();

        Assert.Equal(0, registry.Count);
        Assert.All(runtime.Sessions, session => Assert.True(session.Disposed));
    }

    [Fact]
    public async Task AddOrchestration_registers_a_singleton_registry_and_memory_cache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntime>(new FakeAgentRuntime());
        services.AddSingleton<IArtifactStore>(new FakeArtifactStore());
        // The Git-backed publisher's IGitService lives in the Execution layer; in the full app
        // AddExecution provides it. Here a fake stands in so the test stays scoped to AddOrchestration's own
        // registrations (singleton registry + memory cache + the self-contained, registry-free router).
        services.AddSingleton<IPlanArtifactPublisher>(new FakePlanArtifactPublisher());
        services.AddOrchestration();

        // The registry is IAsyncDisposable-only; the container must be disposed asynchronously
        // (as the host does at shutdown), so a synchronous `using` would throw on dispose.
        await using ServiceProvider provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<RepositoryOrchestratorRegistry>();
        var second = provider.GetRequiredService<RepositoryOrchestratorRegistry>();

        Assert.Same(first, second);
        Assert.NotNull(provider.GetRequiredService<IMemoryCache>());
    }
}
