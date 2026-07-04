using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using CommandCenter.Backend;
using CommandCenter.Core.Configuration;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests.Orchestration;

/// <summary>
/// Endpoint-level coverage for the Plan Authoring routes along the paths that reject BEFORE any Codex
/// session opens — unknown repository (404), empty epic (400), and revise-without-a-warm-process
/// (409). The streaming happy path is unit-tested in <see cref="RepositoryOrchestratorPlanningTests"/>;
/// route registration is asserted by the endpoint-disposition governance test.
/// </summary>
[Collection("ProcessEnvironment")]
public sealed class PlanAuthoringEndpointTests
{
    [Fact]
    public async Task Write_plan_for_an_unknown_repository_returns_not_found()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            server.PlanRoute(Guid.NewGuid(), "write"),
            new { epic = "epic", specs = Array.Empty<string>(), newCodebase = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Write_plan_with_an_empty_epic_is_rejected()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            server.PlanRoute(server.RegisteredRepositoryId, "write"),
            new { epic = "   ", specs = Array.Empty<string>(), newCodebase = false });

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
        // 60s (not a tight behavioural bound — a healthy replay completes in <100ms): cold-start
        // headroom so the very first full-suite run's JIT/thread-pool warmup cannot trip the live-HTTP
        // SSE-replay read before the deterministic frames arrive. Assertions are unchanged.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using HttpResponseMessage response =
            await server.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await ReadStreamUntilAsync(
            response,
            ReplayFramesTwoAndThreeComplete,
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
        // 60s (not a tight behavioural bound — a healthy replay completes in <100ms): cold-start
        // headroom so the very first full-suite run's JIT/thread-pool warmup cannot trip the live-HTTP
        // SSE-replay read before the deterministic frames arrive. Assertions are unchanged.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using HttpResponseMessage response =
            await server.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await ReadStreamUntilAsync(
            response,
            ReplayFramesTwoAndThreeComplete,
            cts.Token);

        Assert.Contains("id: 2", body);
        Assert.Contains("id: 3", body);
        Assert.Contains("\"two\"", body);
        Assert.Contains("\"three\"", body);
        Assert.DoesNotContain("\"one\"", body); // seq 1 was acknowledged, never replayed
    }

    [Fact]
    public async Task Decision_run_for_an_unknown_repository_returns_not_found()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.PostAsync(server.DecisionRoute(Guid.NewGuid(), "run"), content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Decision_run_without_operational_context_is_a_conflict()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        // The registered repository has no .agents/operational_context.md on disk, so a decision run cannot
        // seed — it is rejected before any Codex session opens.
        HttpResponseMessage response =
            await server.Client.PostAsync(server.DecisionRoute(server.RegisteredRepositoryId, "run"), content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Submit_decisions_for_an_unknown_repository_returns_not_found()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            server.DecisionRoute(Guid.NewGuid(), "submit"),
            new { decisions = "approved" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Submit_empty_decisions_is_rejected()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            server.DecisionRoute(server.RegisteredRepositoryId, "submit"),
            new { decisions = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Decision_stream_for_an_unknown_repository_returns_not_found()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.GetAsync(server.DecisionRoute(Guid.NewGuid(), "stream"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Decision_stream_replays_only_events_after_the_last_event_id()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        RepositoryOrchestratorRegistry registry = server.Services.GetRequiredService<RepositoryOrchestratorRegistry>();
        RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(server.RegisteredRepositoryId.ToString("D"));
        orchestrator.DecisionStream.Publish("delta", "{\"text\":\"one\"}");
        orchestrator.DecisionStream.Publish("delta", "{\"text\":\"two\"}");
        orchestrator.DecisionStream.Publish("delta", "{\"text\":\"three\"}");

        using var request = new HttpRequestMessage(HttpMethod.Get, server.DecisionRoute(server.RegisteredRepositoryId, "stream"));
        request.Headers.TryAddWithoutValidation("Last-Event-ID", "1");
        // 60s (not a tight behavioural bound — a healthy replay completes in <100ms): cold-start
        // headroom so the very first full-suite run's JIT/thread-pool warmup cannot trip the live-HTTP
        // SSE-replay read before the deterministic frames arrive. Assertions are unchanged.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using HttpResponseMessage response =
            await server.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await ReadStreamUntilAsync(
            response,
            ReplayFramesTwoAndThreeComplete,
            cts.Token);

        Assert.Contains("id: 2", body);
        Assert.Contains("id: 3", body);
        Assert.Contains("\"two\"", body);
        Assert.Contains("\"three\"", body);
        Assert.DoesNotContain("\"one\"", body); // seq 1 was acknowledged, never replayed
    }

    [Fact]
    public async Task Conversation_for_an_unknown_repository_returns_not_found()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.GetAsync(server.ConversationRoute(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Conversation_for_a_known_repository_returns_an_empty_timeline()
    {
        await using PlanAuthoringTestServer server = await PlanAuthoringTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.GetAsync(server.ConversationRoute(server.RegisteredRepositoryId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"entries\":[]", body); // camelCase projection with no turns yet
    }

    /// <summary>
    /// Reads an SSE response, accumulating frames until <paramref name="done"/> is satisfied or the
    /// caller's <paramref name="cancellationToken"/> (the 20s test deadline) fires. A single
    /// <see cref="StreamReader.ReadAsync(Memory{char},CancellationToken)"/> can split anywhere — including
    /// immediately after a frame's <c>id:</c> header but before its <c>data:</c> line — so the
    /// <paramref name="done"/> predicate MUST key off a fully terminated frame (its trailing blank line),
    /// never a bare <c>id:</c> marker, or the loop can stop with a half-received final frame under load.
    /// The returned body is unchanged on the happy path; assertions against it are untouched.
    /// </summary>
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

    // True once the SSE body carries the fully terminated frames for sequences 2 and 3 — i.e. each frame's
    // id/event/data block ends with the blank-line separator. Keying off the blank-line terminator (not a
    // bare "id: 3") guarantees the reader does not stop after a read that split between the id: header and
    // the data: payload of the final frame, which is what produced the load-only "three"-missing flake.
    private static bool ReplayFramesTwoAndThreeComplete(string body) =>
        FrameComplete(body, 2) && FrameComplete(body, 3);

    private static bool FrameComplete(string body, long sequence) =>
        Regex.IsMatch(
            body,
            $@"(?ms)^id:\s*{sequence}\nevent:[^\n]*\ndata:[^\n]*\n\n",
            RegexOptions.None);

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

        public string DecisionRoute(Guid repositoryId, string verb) =>
            $"/api/repositories/{repositoryId:D}/decision/{verb}";

        public string ConversationRoute(Guid repositoryId) =>
            $"/api/repositories/{repositoryId:D}/conversation";

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
