using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CommandCenter.Backend;
using CommandCenter.Core.Configuration;
using CommandCenter.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

/// <summary>
/// Request-boundary contracts for the orchestration loop commands (m8 slice "Snapshot + command contracts").
/// For each command this pins three independently-authored views of the SAME request shape so they cannot
/// drift apart silently: (a) the ASP.NET backend endpoint (method + route template + <c>repositoryId:guid</c>
/// route argument + presence/absence of a request body), (b) the Tauri proxy command in
/// <c>src/CommandCenter.Shell/src/main.rs</c>, and (c) the TypeScript api wrapper that invokes it.
/// </summary>
/// <remarks>
/// The Rust commands forward through the shared <c>backend_get_value</c> / <c>backend_post_value</c> /
/// <c>backend_post_json_value</c> helpers, so the HTTP verb (<c>.post(</c>/<c>.send(</c>) and body
/// forwarding (<c>.json(</c>) live in those helper bodies, not in each command body. The command body
/// therefore carries: the repository-relative path <c>format!</c> (WITHOUT the <c>{BACKEND_URL}</c> prefix,
/// which the helper prepends), the helper it delegates to, and — for body-carrying POSTs — the
/// <c>json!({ ... })</c> request body it composes. These tests assert exactly that real shape.
/// </remarks>
public sealed class OrchestrationRequestBoundaryContractTests
{
    // ------------------------------------------------------------------ GET plan/status

    [Fact]
    public async Task PlanStatusBackendEndpointHasRepositoryIdRouteArgumentAndNoBody()
    {
        await using WebApplication app = CreateInspectableApp();
        await app.StartAsync();

        RouteEndpoint endpoint = FindEndpoint(app, "/api/repositories/{repositoryId:guid}/plan/status", "GET");

        AssertRepositoryIdGuidRoute(endpoint, "GET");
        Assert.DoesNotContain(endpoint.Metadata, metadata => metadata is IFromBodyMetadata);
    }

    [Fact]
    public void PlanStatusRustCommandHasRepositoryIdArgumentAndForwardsGetWithoutBody()
    {
        string source = ReadMainRs();
        string body = ExtractRustFunctionBody(source, "get_plan_status");

        Assert.Contains("fn get_plan_status(repository_id: String)", source, StringComparison.Ordinal);
        Assert.Contains("\"/api/repositories/{repository_id}/plan/status\"", body, StringComparison.Ordinal);
        Assert.Contains("backend_get_value(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("backend_post", body, StringComparison.Ordinal);
        Assert.DoesNotContain("json!(", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanStatusTypeScriptApiInvokesCommandWithRepositoryIdArgument()
    {
        string source = ReadTypeScriptApi("planning.ts");
        string body = ExtractTypeScriptFunctionBody(source, "getPlanStatus");

        Assert.Contains("function getPlanStatus(repositoryId: string)", source, StringComparison.Ordinal);
        Assert.Contains("invokeCommand<PlanStatus>('get_plan_status', { repositoryId })", body, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ POST plan/write (body)

    [Fact]
    public async Task PlanWriteBackendEndpointHasRepositoryIdRouteArgumentAndAcceptsBody()
    {
        await using WebApplication app = CreateInspectableApp();
        await app.StartAsync();

        RouteEndpoint endpoint = FindEndpoint(app, "/api/repositories/{repositoryId:guid}/plan/write", "POST");

        AssertRepositoryIdGuidRoute(endpoint, "POST");
    }

    [Fact]
    public void PlanWriteRustCommandForwardsPostWithBody()
    {
        string source = ReadMainRs();
        string body = ExtractRustFunctionBody(source, "write_plan");

        Assert.Contains("fn write_plan(", source, StringComparison.Ordinal);
        Assert.Contains("repository_id: String", source, StringComparison.Ordinal);
        Assert.Contains("\"/api/repositories/{repository_id}/plan/write\"", body, StringComparison.Ordinal);
        Assert.Contains("backend_post_json_value(", body, StringComparison.Ordinal);
        Assert.Contains("\"roadmap\": roadmap", body, StringComparison.Ordinal);
        Assert.Contains("\"specs\": specs", body, StringComparison.Ordinal);
        Assert.Contains("\"newCodebase\": new_codebase", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanWriteTypeScriptApiInvokesCommandWithPayload()
    {
        string source = ReadTypeScriptApi("planning.ts");
        string body = ExtractTypeScriptFunctionBody(source, "writePlan");

        Assert.Contains(
            "function writePlan(repositoryId: string, roadmap: string, specs: string[], newCodebase: boolean)",
            source,
            StringComparison.Ordinal);
        Assert.Contains("invokeCommand<{ phase: string }>('write_plan', {", body, StringComparison.Ordinal);
        Assert.Contains("repositoryId", body, StringComparison.Ordinal);
        Assert.Contains("roadmap", body, StringComparison.Ordinal);
        Assert.Contains("specs", body, StringComparison.Ordinal);
        Assert.Contains("newCodebase", body, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ POST plan/revise (body)

    [Fact]
    public async Task PlanReviseBackendEndpointHasRepositoryIdRouteArgumentAndAcceptsBody()
    {
        await using WebApplication app = CreateInspectableApp();
        await app.StartAsync();

        RouteEndpoint endpoint = FindEndpoint(app, "/api/repositories/{repositoryId:guid}/plan/revise", "POST");

        AssertRepositoryIdGuidRoute(endpoint, "POST");
    }

    [Fact]
    public void PlanReviseRustCommandForwardsPostWithBody()
    {
        string source = ReadMainRs();
        string body = ExtractRustFunctionBody(source, "revise_plan");

        Assert.Contains("fn revise_plan(repository_id: String, feedback: String)", source, StringComparison.Ordinal);
        Assert.Contains("\"/api/repositories/{repository_id}/plan/revise\"", body, StringComparison.Ordinal);
        Assert.Contains("backend_post_json_value(", body, StringComparison.Ordinal);
        Assert.Contains("\"feedback\": feedback", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanReviseTypeScriptApiInvokesCommandWithPayload()
    {
        string source = ReadTypeScriptApi("planning.ts");
        string body = ExtractTypeScriptFunctionBody(source, "revisePlan");

        Assert.Contains("function revisePlan(repositoryId: string, feedback: string)", source, StringComparison.Ordinal);
        Assert.Contains("invokeCommand<{ phase: string }>('revise_plan', { repositoryId, feedback })", body, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ POST plan/execute (no body)

    [Fact]
    public async Task PlanExecuteBackendEndpointHasRepositoryIdRouteArgumentAndNoBody()
    {
        await using WebApplication app = CreateInspectableApp();
        await app.StartAsync();

        RouteEndpoint endpoint = FindEndpoint(app, "/api/repositories/{repositoryId:guid}/plan/execute", "POST");

        AssertRepositoryIdGuidRoute(endpoint, "POST");
        Assert.DoesNotContain(endpoint.Metadata, metadata => metadata is IFromBodyMetadata);
    }

    [Fact]
    public void PlanExecuteRustCommandForwardsPostWithoutBody()
    {
        string source = ReadMainRs();
        string body = ExtractRustFunctionBody(source, "execute_plan");

        Assert.Contains("fn execute_plan(repository_id: String)", source, StringComparison.Ordinal);
        Assert.Contains("\"/api/repositories/{repository_id}/plan/execute\"", body, StringComparison.Ordinal);
        Assert.Contains("backend_post_value(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("backend_post_json_value(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("json!(", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanExecuteTypeScriptApiInvokesCommandWithRepositoryIdArgument()
    {
        string source = ReadTypeScriptApi("planning.ts");
        string body = ExtractTypeScriptFunctionBody(source, "executePlan");

        Assert.Contains("function executePlan(repositoryId: string)", source, StringComparison.Ordinal);
        Assert.Contains("invokeCommand<{ phase: string }>('execute_plan', { repositoryId })", body, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ POST decision/run (no body)

    [Fact]
    public async Task DecisionRunBackendEndpointHasRepositoryIdRouteArgumentAndNoBody()
    {
        await using WebApplication app = CreateInspectableApp();
        await app.StartAsync();

        RouteEndpoint endpoint = FindEndpoint(app, "/api/repositories/{repositoryId:guid}/decision/run", "POST");

        AssertRepositoryIdGuidRoute(endpoint, "POST");
        Assert.DoesNotContain(endpoint.Metadata, metadata => metadata is IFromBodyMetadata);
    }

    [Fact]
    public void DecisionRunRustCommandForwardsPostWithoutBody()
    {
        string source = ReadMainRs();
        string body = ExtractRustFunctionBody(source, "decision_run");

        Assert.Contains("fn decision_run(repository_id: String)", source, StringComparison.Ordinal);
        Assert.Contains("\"/api/repositories/{repository_id}/decision/run\"", body, StringComparison.Ordinal);
        Assert.Contains("backend_post_value(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("backend_post_json_value(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("json!(", body, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionRunTypeScriptApiInvokesCommandWithRepositoryIdArgument()
    {
        string source = ReadTypeScriptApi("decisionRuntime.ts");
        string body = ExtractTypeScriptFunctionBody(source, "startDecisionRun");

        Assert.Contains("function startDecisionRun(repositoryId: string)", source, StringComparison.Ordinal);
        Assert.Contains("invokeCommand<{ phase: string }>('decision_run', { repositoryId })", body, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ POST decision/submit (body)

    [Fact]
    public async Task DecisionSubmitBackendEndpointHasRepositoryIdRouteArgumentAndAcceptsBody()
    {
        await using WebApplication app = CreateInspectableApp();
        await app.StartAsync();

        RouteEndpoint endpoint = FindEndpoint(app, "/api/repositories/{repositoryId:guid}/decision/submit", "POST");

        AssertRepositoryIdGuidRoute(endpoint, "POST");
    }

    [Fact]
    public void DecisionSubmitRustCommandForwardsPostWithBody()
    {
        string source = ReadMainRs();
        string body = ExtractRustFunctionBody(source, "decision_submit");

        Assert.Contains("fn decision_submit(repository_id: String, decisions: String)", source, StringComparison.Ordinal);
        Assert.Contains("\"/api/repositories/{repository_id}/decision/submit\"", body, StringComparison.Ordinal);
        Assert.Contains("backend_post_json_value(", body, StringComparison.Ordinal);
        Assert.Contains("\"decisions\": decisions", body, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionSubmitTypeScriptApiInvokesCommandWithPayload()
    {
        string source = ReadTypeScriptApi("decisionRuntime.ts");
        string body = ExtractTypeScriptFunctionBody(source, "submitDecisions");

        Assert.Contains("function submitDecisions(repositoryId: string, decisions: string)", source, StringComparison.Ordinal);
        Assert.Contains("invokeCommand<{ phase: string }>('decision_submit', { repositoryId, decisions })", body, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ helpers

    private static void AssertRepositoryIdGuidRoute(RouteEndpoint endpoint, string method)
    {
        HttpMethodMetadata methodMetadata = endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>();
        RoutePatternParameterPart parameter = Assert.Single(endpoint.RoutePattern.Parameters);

        Assert.Equal([method], methodMetadata.HttpMethods);
        Assert.Equal("repositoryId", parameter.Name);
        Assert.Equal("guid", Assert.Single(parameter.ParameterPolicies).Content);
        Assert.False(parameter.IsOptional);
    }

    // Endpoints only materialize in the EndpointDataSource after StartAsync builds the routing pipeline, so
    // these tests must start the app — but an in-memory config store keeps that startup free of the shared
    // configuration.json read that otherwise contends with other app-booting tests in the parallel run.
    private static WebApplication CreateInspectableApp()
    {
        WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IApplicationConfigurationStore>(
                new InMemoryConfigurationStore(new ApplicationConfiguration())));
        app.Urls.Add("http://127.0.0.1:0");
        return app;
    }

    private static RouteEndpoint FindEndpoint(WebApplication app, string rawText, string method)
    {
        RouteEndpoint[] endpoints = app.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>().ToArray();
        return Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == rawText &&
            endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods.SequenceEqual([method]));
    }

    private static string ReadMainRs() =>
        File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.Shell", "src", "main.rs"));

    private static string ReadTypeScriptApi(string fileName) =>
        File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.UI", "src", "api", fileName));

    private static string ExtractRustFunctionBody(string source, string functionName)
    {
        Match function = Regex.Match(source, $@"fn\s+{Regex.Escape(functionName)}\s*\(");
        Assert.True(function.Success, $"Rust function {functionName} should exist.");
        int bodyStart = source.IndexOf('{', function.Index);
        Assert.True(bodyStart >= 0, $"Rust function {functionName} should have a body.");
        return ExtractBalancedBlock(source, bodyStart);
    }

    private static string ExtractTypeScriptFunctionBody(string source, string functionName)
    {
        Match function = Regex.Match(source, $@"function\s+{Regex.Escape(functionName)}\s*\(");
        Assert.True(function.Success, $"TypeScript function {functionName} should exist.");
        int bodyStart = source.IndexOf('{', function.Index);
        Assert.True(bodyStart >= 0, $"TypeScript function {functionName} should have a body.");
        return ExtractBalancedBlock(source, bodyStart);
    }

    private static string ExtractBalancedBlock(string source, int blockStart)
    {
        int depth = 0;
        for (int index = blockStart; index < source.Length; index++)
        {
            char character = source[index];
            if (character == '{')
            {
                depth++;
            }
            else if (character == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[blockStart..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException("Unbalanced block.");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "CommandCenter.Shell", "src", "main.rs")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}

/// <summary>
/// Live structured-error contract tests for the orchestration commands (m8 slice). These boot the real app
/// via <see cref="Program.CreateApp(string[], System.Action{IServiceCollection})"/> with a single registered
/// repository, issue real HTTP requests, and assert the on-the-wire structured-error shape <c>{ error }</c>:
/// an unknown repository GUID returns 404 and plan/write with a blank roadmap returns 400 — each with a JSON
/// body carrying a non-empty string <c>error</c> field. The harness mirrors
/// <c>Orchestration.PlanAuthoringEndpointTests</c>.
/// </summary>
public sealed class OrchestrationErrorContractTests
{
    [Fact]
    public async Task PlanStatusForUnknownRepositoryReturnsStructuredNotFoundError()
    {
        await using OrchestrationErrorTestServer server = await OrchestrationErrorTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.GetAsync(
            $"/api/repositories/{Guid.NewGuid():D}/plan/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertStructuredErrorAsync(response);
    }

    [Fact]
    public async Task PlanWriteForUnknownRepositoryReturnsStructuredNotFoundError()
    {
        await using OrchestrationErrorTestServer server = await OrchestrationErrorTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            $"/api/repositories/{Guid.NewGuid():D}/plan/write",
            new { roadmap = "roadmap", specs = Array.Empty<string>(), newCodebase = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertStructuredErrorAsync(response);
    }

    [Fact]
    public async Task PlanWriteWithBlankRoadmapReturnsStructuredBadRequestError()
    {
        await using OrchestrationErrorTestServer server = await OrchestrationErrorTestServer.StartAsync();

        HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            $"/api/repositories/{server.RegisteredRepositoryId:D}/plan/write",
            new { roadmap = "   ", specs = Array.Empty<string>(), newCodebase = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertStructuredErrorAsync(response);
    }

    private static async Task AssertStructuredErrorAsync(HttpResponseMessage response)
    {
        using System.Text.Json.JsonDocument document =
            System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(System.Text.Json.JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.True(
            document.RootElement.TryGetProperty("error", out System.Text.Json.JsonElement error),
            "structured error body must carry an 'error' field");
        Assert.Equal(System.Text.Json.JsonValueKind.String, error.ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(error.GetString()));
    }

    private sealed class OrchestrationErrorTestServer : IAsyncDisposable
    {
        private readonly WebApplication app;

        private OrchestrationErrorTestServer(WebApplication app, Guid registeredRepositoryId)
        {
            this.app = app;
            RegisteredRepositoryId = registeredRepositoryId;
            Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        }

        public HttpClient Client { get; }

        public Guid RegisteredRepositoryId { get; }

        public static async Task<OrchestrationErrorTestServer> StartAsync()
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

            return new OrchestrationErrorTestServer(app, registered.Id);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }
}

/// <summary>
/// File-backed config is shared across every <see cref="Program.CreateApp(string[], System.Action{IServiceCollection})"/>
/// boot in the test run (one <c>COMMAND_CENTER_CONFIGURATION_PATH</c>), so app-booting tests that let the
/// <c>DecisionSessionRecoveryHostedService</c> read it on startup contend on the file. Tests that boot the app
/// inject this in-memory store instead so startup performs no shared-file I/O.
/// </summary>
internal sealed class InMemoryConfigurationStore : IApplicationConfigurationStore
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
