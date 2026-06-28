using System.Net;
using System.Net.Http.Json;
using System.Text;
using CommandCenter.Backend;
using CommandCenter.Core.Configuration;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests.Orchestration;

/// <summary>
/// Endpoint-level coverage for the Plan Authoring routes along the paths that reject BEFORE any Codex
/// session opens — unknown repository (404), empty roadmap (400), and revise-without-a-warm-process
/// (409). The streaming happy path is unit-tested in <see cref="RepositoryOrchestratorPlanningTests"/>;
/// route registration is asserted by the endpoint-disposition governance test.
/// </summary>
public sealed class PlanAuthoringEndpointTests
{
    [Fact]
    public async Task Write_plan_for_an_unknown_repository_returns_not_found()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            server.PlanRoute(Guid.NewGuid(), "write"),
            new { roadmap = "roadmap", specs = Array.Empty<string>(), newCodebase = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Write_plan_with_an_empty_roadmap_is_rejected()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            server.PlanRoute(server.RegisteredRepositoryId, "write"),
            new { roadmap = "   ", specs = Array.Empty<string>(), newCodebase = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Revise_plan_without_a_warm_planning_session_is_a_conflict()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            server.PlanRoute(server.RegisteredRepositoryId, "revise"),
            new { feedback = "tighten scope" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Revise_plan_for_an_unknown_repository_returns_not_found()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            server.PlanRoute(Guid.NewGuid(), "revise"),
            new { feedback = "tighten" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Execute_plan_for_an_unknown_repository_returns_not_found()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.PostAsync(server.PlanRoute(Guid.NewGuid(), "execute"), content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Plan_stream_for_an_unknown_repository_returns_not_found()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.GetAsync(server.PlanRoute(Guid.NewGuid(), "stream"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Plan_stream_replays_only_events_after_the_last_event_id()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        // Publish three frames into the registered repository's planning stream, then reconnect with
        // Last-Event-ID: 1 — only seq 2 and 3 must replay, and each SSE block must carry an id: line.
        RepositoryOrchestratorRegistry registry = server.Services.GetRequiredService<RepositoryOrchestratorRegistry>();
        RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(server.RegisteredRepositoryId.ToString("D"));
        orchestrator.PlanningStream.Publish("delta", "{\"text\":\"one\"}");
        orchestrator.PlanningStream.Publish("delta", "{\"text\":\"two\"}");
        orchestrator.PlanningStream.Publish("delta", "{\"text\":\"three\"}");

        using var request = new HttpRequestMessage(HttpMethod.Get, server.PlanRoute(server.RegisteredRepositoryId, "stream"));
        request.Headers.TryAddWithoutValidation("Last-Event-ID", "1");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using HttpResponseMessage response =
            await server.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await ReadStreamUntilAsync(
            response,
            text => text.Contains("id: 2") && text.Contains("id: 3"),
            cts.Token);

        Assert.Contains("id: 2", body);
        Assert.Contains("id: 3", body);
        Assert.Contains("\"two\"", body);
        Assert.Contains("\"three\"", body);
        Assert.DoesNotContain("\"one\"", body); // seq 1 was acknowledged, never replayed
    }

    [Fact]
    public async Task Execution_stream_for_an_unknown_repository_returns_not_found()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.GetAsync(
            $"/api/repositories/{Guid.NewGuid():D}/execution/stream");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Execution_stream_replays_only_events_after_the_last_event_id()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        RepositoryOrchestratorRegistry registry = server.Services.GetRequiredService<RepositoryOrchestratorRegistry>();
        RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(server.RegisteredRepositoryId.ToString("D"));
        orchestrator.ExecutionStream.Publish("delta", "{\"text\":\"one\"}");
        orchestrator.ExecutionStream.Publish("delta", "{\"text\":\"two\"}");
        orchestrator.ExecutionStream.Publish("delta", "{\"text\":\"three\"}");

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/repositories/{server.RegisteredRepositoryId:D}/execution/stream");
        request.Headers.TryAddWithoutValidation("Last-Event-ID", "1");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using HttpResponseMessage response =
            await server.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await ReadStreamUntilAsync(
            response,
            text => text.Contains("id: 2") && text.Contains("id: 3"),
            cts.Token);

        Assert.Contains("id: 2", body);
        Assert.Contains("id: 3", body);
        Assert.Contains("\"two\"", body);
        Assert.Contains("\"three\"", body);
        Assert.DoesNotContain("\"one\"", body); // seq 1 was acknowledged, never replayed
    }

    private static async Task<string> ReadStreamUntilAsync(
        HttpResponseMessage response,
        Func<string, bool> done,
        CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var builder = new StringBuilder();
        var buffer = new char[256];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                builder.Append(buffer, 0, read);
                if (done(builder.ToString()))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        return builder.ToString();
    }

    private sealed class PlanAuthoringTestServer : IAsyncDisposable
    {
        private readonly WebApplication app;

        private PlanAuthoringTestServer(WebApplication app, Guid registeredRepositoryId)
        {
            this.app = app;
            RegisteredRepositoryId = registeredRepositoryId;
            Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        }

        public HttpClient Client { get; }

        public Guid RegisteredRepositoryId { get; }

        public IServiceProvider Services => app.Services;

        public static async Task<PlanAuthoringTestServer> StartAsync()
        {
            var registered = new Repository
            {
                Id = Guid.NewGuid(),
                Name = "fixture",
                Path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            };

            var configurationStore = new InMemoryConfigurationStore(
                new ApplicationConfiguration { Repositories = new[] { registered } });

            WebApplication app = Program.CreateApp(
                [],
                services => services.AddSingleton<IApplicationConfigurationStore>(configurationStore));
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();

            return new PlanAuthoringTestServer(app, registered.Id);
        }

        public string PlanRoute(Guid repositoryId, string verb) =>
            $"/api/repositories/{repositoryId:D}/plan/{verb}";

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class InMemoryConfigurationStore : IApplicationConfigurationStore
    {
        private ApplicationConfiguration configuration;

        public InMemoryConfigurationStore(ApplicationConfiguration configuration) =>
            this.configuration = configuration;

        public Task<ApplicationConfiguration> LoadAsync() => Task.FromResult(configuration);

        public Task SaveAsync(ApplicationConfiguration value)
        {
            configuration = value;
            return Task.CompletedTask;
        }
    }
}
