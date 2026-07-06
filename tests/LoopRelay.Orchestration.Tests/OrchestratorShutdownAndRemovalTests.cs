using LoopRelay.Agents.Abstractions;
using LoopRelay.Orchestration.Extensions;
using LoopRelay.Orchestration.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LoopRelay.Orchestration.Tests;

/// <summary>
/// m10 app-shutdown hook + repository-removal teardown. The shutdown hosted service disposes the orchestrator
/// registry on a graceful stop (releasing held-open codex processes), and DELETE /api/repositories/{id} now tears
/// down any live orchestrator before rewriting config (so removing a repository with a live process does not leak
/// it). Both are additive and best-effort.
/// </summary>
[Collection("ProcessEnvironment")]
public sealed class OrchestratorShutdownAndRemovalTests
{
    // ---- (4) App-shutdown hook ----

    [Fact]
    public async Task StopAsync_disposes_the_orchestrator_registry_and_its_live_sessions()
    {
        var runtime = new FakeAgentRuntime();
        RepositoryOrchestratorRegistry registry = OrchestrationTestFactory.Registry(runtime: runtime);
        var hostedService = new OrchestratorShutdownHostedService(registry);

        RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(Guid.NewGuid().ToString("D"));
        await orchestrator.EnsurePlanningSessionAsync(OrchestrationTestFactory.Repository());
        Assert.Equal(1, registry.Count);

        await hostedService.StartAsync(CancellationToken.None); // no-op
        await hostedService.StopAsync(CancellationToken.None);

        Assert.Equal(0, registry.Count);
        Assert.True(orchestrator.IsDisposed);
        Assert.All(runtime.Sessions, session => Assert.True(session.Disposed));
    }

    [Fact]
    public async Task StopAsync_is_best_effort_and_does_not_throw_when_the_registry_is_empty()
    {
        RepositoryOrchestratorRegistry registry = OrchestrationTestFactory.Registry();
        var hostedService = new OrchestratorShutdownHostedService(registry);

        await hostedService.StopAsync(CancellationToken.None); // no live orchestrators -> clean no-op
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public async Task AddOrchestration_registers_the_shutdown_hosted_service()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntime>(new FakeAgentRuntime());
        services.AddSingleton<LoopRelay.Core.Artifacts.IArtifactStore>(new FakeArtifactStore());
        services.AddSingleton<LoopRelay.Orchestration.Abstractions.IPlanArtifactPublisher>(new FakePlanArtifactPublisher());
        services.AddOrchestration();

        await using ServiceProvider provider = services.BuildServiceProvider();

        IHostedService hostedService = Assert.Single(
            provider.GetServices<IHostedService>(), s => s is OrchestratorShutdownHostedService);
        Assert.IsType<OrchestratorShutdownHostedService>(hostedService);
    }
}
