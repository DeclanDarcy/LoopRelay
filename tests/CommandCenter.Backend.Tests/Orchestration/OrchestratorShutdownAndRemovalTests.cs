using System.Net;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Backend;
using CommandCenter.Core.Configuration;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Extensions;
using CommandCenter.Orchestration.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CommandCenter.Backend.Tests.Orchestration;

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
        services.AddSingleton<Core.Artifacts.IArtifactStore>(new FakeArtifactStore());
        services.AddSingleton<CommandCenter.Orchestration.Abstractions.IPlanArtifactPublisher>(new FakePlanArtifactPublisher());
        services.AddOrchestration();

        await using ServiceProvider provider = services.BuildServiceProvider();

        IHostedService hostedService = Assert.Single(
            provider.GetServices<IHostedService>(), s => s is OrchestratorShutdownHostedService);
        Assert.IsType<OrchestratorShutdownHostedService>(hostedService);
    }

    // ---- (5) Repository-removal teardown ----

    [Fact]
    public async Task Deleting_a_repository_tears_down_its_live_orchestrator_and_session()
    {
        var runtime = new FakeAgentRuntime();
        await using RemovalTestServer server = await RemovalTestServer.StartAsync(runtime);

        // Open a live planning session for the registered repository (simulates a warm process from prior use).
        RepositoryOrchestratorRegistry registry = server.Services.GetRequiredService<RepositoryOrchestratorRegistry>();
        RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(server.RegisteredRepositoryId.ToString("D"));
        await orchestrator.EnsurePlanningSessionAsync(OrchestrationTestFactory.Repository());
        Assert.True(registry.TryGet(server.RegisteredRepositoryId.ToString("D"), out _));

        HttpResponseMessage response =
            await server.Client.DeleteAsync($"/api/repositories/{server.RegisteredRepositoryId:D}");

        // The endpoint keeps its NoContent contract...
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // ...AND the orchestrator (with its session) was torn down — no leaked live process.
        Assert.False(registry.TryGet(server.RegisteredRepositoryId.ToString("D"), out _));
        Assert.Equal(0, registry.Count);
        Assert.True(orchestrator.IsDisposed);
        Assert.All(runtime.Sessions, session => Assert.True(session.Disposed));
    }

    [Fact]
    public async Task Deleting_a_repository_with_no_live_orchestrator_still_returns_no_content()
    {
        var runtime = new FakeAgentRuntime();
        await using RemovalTestServer server = await RemovalTestServer.StartAsync(runtime);

        // No orchestrator was ever created for this repository — teardown is a no-op, the contract is unchanged.
        HttpResponseMessage response =
            await server.Client.DeleteAsync($"/api/repositories/{server.RegisteredRepositoryId:D}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private sealed class RemovalTestServer : IAsyncDisposable
    {
        private readonly WebApplication app;

        private RemovalTestServer(WebApplication app, Guid registeredRepositoryId)
        {
            this.app = app;
            RegisteredRepositoryId = registeredRepositoryId;
            Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        }

        public HttpClient Client { get; }

        public Guid RegisteredRepositoryId { get; }

        public IServiceProvider Services => app.Services;

        public static async Task<RemovalTestServer> StartAsync(FakeAgentRuntime runtime)
        {
            var registered = new Repository
            {
                Id = Guid.NewGuid(),
                Name = "fixture",
                Path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            };

            var configurationStore = new InMemoryConfigurationStore(
                new ApplicationConfiguration { Repositories = new[] { registered } });

            WebApplication app = Program.CreateApp([], services =>
            {
                services.AddSingleton<IApplicationConfigurationStore>(configurationStore);
                // Override the real Codex runtime with the fake so no real process launches (last registration wins
                // for resolution). This keeps the removal test hermetic.
                services.AddSingleton<IAgentRuntime>(runtime);
            });
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();

            return new RemovalTestServer(app, registered.Id);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class InMemoryConfigurationStore(ApplicationConfiguration configuration) : IApplicationConfigurationStore
    {
        private ApplicationConfiguration current = configuration;

        public Task<ApplicationConfiguration> LoadAsync() => Task.FromResult(current);

        public Task SaveAsync(ApplicationConfiguration configurationToSave)
        {
            current = configurationToSave;
            return Task.CompletedTask;
        }
    }
}
