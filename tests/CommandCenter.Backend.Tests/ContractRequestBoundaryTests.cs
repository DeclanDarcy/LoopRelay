using System.Text.RegularExpressions;
using CommandCenter.Backend;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

[Collection("ProcessEnvironment")]
public sealed class ContractRequestBoundaryTests
{
    [Fact]
    public async Task RepositoryDashboardBackendEndpointHasNoRequestArguments()
    {
        await using WebApplication app = Program.CreateApp([]);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        RouteEndpoint endpoint = FindEndpoint(app, "/api/repositories", "GET");

        HttpMethodMetadata method = endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>();

        Assert.Equal(["GET"], method.HttpMethods);
        Assert.Empty(endpoint.RoutePattern.Parameters);
        Assert.DoesNotContain(endpoint.Metadata, metadata => metadata is IFromBodyMetadata);
    }

    [Fact]
    public async Task RepositoryWorkspaceBackendEndpointHasRepositoryIdRouteArgumentAndNoBody()
    {
        await using WebApplication app = Program.CreateApp([]);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        RouteEndpoint endpoint = FindEndpoint(app, "/api/repositories/{repositoryId:guid}/workspace", "GET");

        HttpMethodMetadata method = endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>();
        RoutePatternParameterPart parameter = Assert.Single(endpoint.RoutePattern.Parameters);

        Assert.Equal(["GET"], method.HttpMethods);
        Assert.Equal("repositoryId", parameter.Name);
        Assert.Equal("guid", Assert.Single(parameter.ParameterPolicies).Content);
        Assert.False(parameter.IsOptional);
        Assert.DoesNotContain(endpoint.Metadata, metadata => metadata is IFromBodyMetadata);
    }

    [Fact]
    public async Task WorkflowProjectionBackendEndpointHasRepositoryIdRouteArgumentAndNoBody()
    {
        await using WebApplication app = Program.CreateApp([]);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        RouteEndpoint endpoint = FindEndpoint(app, "/api/repositories/{repositoryId:guid}/workflow", "GET");

        HttpMethodMetadata method = endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>();
        RoutePatternParameterPart parameter = Assert.Single(endpoint.RoutePattern.Parameters);

        Assert.Equal(["GET"], method.HttpMethods);
        Assert.Equal("repositoryId", parameter.Name);
        Assert.Equal("guid", Assert.Single(parameter.ParameterPolicies).Content);
        Assert.False(parameter.IsOptional);
        Assert.DoesNotContain(endpoint.Metadata, metadata => metadata is IFromBodyMetadata);
    }

    [Fact]
    public void RepositoryDashboardRustCommandHasNoCommandArgumentsAndForwardsGetWithoutBody()
    {
        string source = File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.Shell", "src", "main.rs"));
        string body = ExtractRustFunctionBody(source, "list_repositories");

        Assert.Contains("fn list_repositories() -> Result<Vec<RepositoryDashboardProjection>, String>", source, StringComparison.Ordinal);
        Assert.Contains("reqwest::blocking::get(format!(\"{BACKEND_URL}/api/repositories\"))", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Client::new", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".post(", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".send(", body, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryWorkspaceRustCommandHasRepositoryIdArgumentAndForwardsGetWithoutBody()
    {
        string source = File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.Shell", "src", "main.rs"));
        string body = ExtractRustFunctionBody(source, "get_repository_workspace");

        Assert.Contains("fn get_repository_workspace(", source, StringComparison.Ordinal);
        Assert.Contains("repository_id: String", source, StringComparison.Ordinal);
        Assert.Contains("\"{BACKEND_URL}/api/repositories/{repository_id}/workspace\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Client::new", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".post(", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".json(&", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".send(", body, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowProjectionRustCommandHasRepositoryIdArgumentAndForwardsGetWithoutBody()
    {
        string source = File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.Shell", "src", "main.rs"));
        string body = ExtractRustFunctionBody(source, "get_workflow_projection");

        Assert.Contains("fn get_workflow_projection(repository_id: String) -> Result<Value, String>", source, StringComparison.Ordinal);
        Assert.Contains("\"/api/repositories/{repository_id}/workflow\"", body, StringComparison.Ordinal);
        Assert.Contains("backend_get_value(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Client::new", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".post(", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".json(&", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".send(", body, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryDashboardTypeScriptApiInvokesCommandWithoutArguments()
    {
        string source = File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.UI", "src", "api", "repositories.ts"));
        string body = ExtractTypeScriptFunctionBody(source, "listRepositories");

        Assert.Contains("invokeCommand<RepositoryDashboardProjection[]>('list_repositories')", body, StringComparison.Ordinal);
        Assert.DoesNotContain("invokeCommand<RepositoryDashboardProjection[]>('list_repositories',", body, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryWorkspaceTypeScriptApiInvokesCommandWithRepositoryIdArgument()
    {
        string source = File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.UI", "src", "api", "repositories.ts"));
        string body = ExtractTypeScriptFunctionBody(source, "getRepositoryWorkspace");

        Assert.Contains("function getRepositoryWorkspace(repositoryId: string)", source, StringComparison.Ordinal);
        Assert.Contains("invokeCommand<RepositoryWorkspaceProjection>('get_repository_workspace', { repositoryId })", body, StringComparison.Ordinal);
        Assert.DoesNotContain("'get_repository_workspace')", body, StringComparison.Ordinal);
        Assert.DoesNotContain("'get_repository_workspace', {})", body, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowProjectionTypeScriptApiInvokesCommandWithRepositoryIdArgument()
    {
        string source = File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.UI", "src", "api", "workflow.ts"));
        string body = ExtractTypeScriptFunctionBody(source, "getWorkflowProjection");

        Assert.Contains("function getWorkflowProjection(repositoryId: string)", source, StringComparison.Ordinal);
        Assert.Contains("invokeCommand<WorkflowInstance>('get_workflow_projection', { repositoryId })", body, StringComparison.Ordinal);
        Assert.DoesNotContain("'get_workflow_projection')", body, StringComparison.Ordinal);
        Assert.DoesNotContain("'get_workflow_projection', {})", body, StringComparison.Ordinal);
    }

    private static RouteEndpoint FindEndpoint(WebApplication app, string rawText, string method)
    {
        RouteEndpoint[] endpoints = app.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .ToArray();

        return Assert.Single(
            endpoints,
            endpoint => endpoint.RoutePattern.RawText == rawText &&
                endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods.SequenceEqual([method]));
    }

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
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[blockStart..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException("Could not parse balanced block.");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(directory.Combine("src", "CommandCenter.Shell", "src", "main.rs")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
