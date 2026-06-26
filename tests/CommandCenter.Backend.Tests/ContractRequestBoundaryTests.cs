using System.Text.RegularExpressions;
using CommandCenter.Backend;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class ContractRequestBoundaryTests
{
    [Fact]
    public async Task RepositoryDashboardBackendEndpointHasNoRequestArguments()
    {
        await using WebApplication app = Program.CreateApp([]);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        RouteEndpoint[] endpoints = app.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .ToArray();
        RouteEndpoint endpoint = Assert.Single(
            endpoints,
            endpoint => endpoint.RoutePattern.RawText == "/api/repositories" &&
                endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods.SequenceEqual(["GET"]));

        HttpMethodMetadata method = endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>();

        Assert.Equal(["GET"], method.HttpMethods);
        Assert.Empty(endpoint.RoutePattern.Parameters);
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
    public void RepositoryDashboardTypeScriptApiInvokesCommandWithoutArguments()
    {
        string source = File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.UI", "src", "api", "repositories.ts"));
        string body = ExtractTypeScriptFunctionBody(source, "listRepositories");

        Assert.Contains("invokeCommand<RepositoryDashboardProjection[]>('list_repositories')", body, StringComparison.Ordinal);
        Assert.DoesNotContain("invokeCommand<RepositoryDashboardProjection[]>('list_repositories',", body, StringComparison.Ordinal);
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
