using CommandCenter.Backend;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class BackendEndpointDispositionTests
{
    [Fact]
    public async Task RegisteredBackendRoutesHaveMilestoneNineDisposition()
    {
        await using WebApplication app = Program.CreateApp([]);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        RouteDisposition[] dispositions = app.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(CreateDispositions)
            .OrderBy(disposition => disposition.Pattern, StringComparer.Ordinal)
            .ThenBy(disposition => disposition.Method, StringComparer.Ordinal)
            .ToArray();

        Assert.DoesNotContain(dispositions, disposition => disposition.Disposition == EndpointDisposition.Unknown);
        Assert.DoesNotContain(dispositions, disposition => disposition.Disposition == EndpointDisposition.Remove);
        Assert.DoesNotContain(dispositions, disposition => disposition.Disposition == EndpointDisposition.Redirect);

        var duplicates = dispositions
            .GroupBy(disposition => (disposition.Method, disposition.Pattern))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.Method} {group.Key.Pattern}")
            .ToArray();

        Assert.Empty(duplicates);
        Assert.Contains(dispositions, disposition => disposition.Family == EndpointFamily.Workflow);
        Assert.Contains(dispositions, disposition => disposition.Family == EndpointFamily.DecisionSessions);
        Assert.Contains(dispositions, disposition => disposition.Family == EndpointFamily.Decisions);
        Assert.Contains(dispositions, disposition => disposition.Family == EndpointFamily.DecisionRuntime);
        Assert.Contains(dispositions, disposition => disposition.Family == EndpointFamily.Execution);
        Assert.Contains(dispositions, disposition => disposition.Family == EndpointFamily.Reasoning);
        Assert.Contains(dispositions, disposition => disposition.Family == EndpointFamily.Continuity);
        Assert.Contains(dispositions, disposition => disposition.Family == EndpointFamily.Artifacts);
    }

    [Fact]
    public async Task InternalAndCompatibilityRoutesRemainExplicitlyBounded()
    {
        await using WebApplication app = Program.CreateApp([]);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        RouteDisposition[] dispositions = app.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(CreateDispositions)
            .ToArray();

        RouteDisposition[] internalRoutes = dispositions
            .Where(disposition => disposition.Disposition == EndpointDisposition.Internal)
            .OrderBy(disposition => disposition.Pattern, StringComparer.Ordinal)
            .ThenBy(disposition => disposition.Method, StringComparer.Ordinal)
            .ToArray();
        RouteDisposition[] compatibilityRoutes = dispositions
            .Where(disposition => disposition.Disposition == EndpointDisposition.Compatibility)
            .OrderBy(disposition => disposition.Pattern, StringComparer.Ordinal)
            .ThenBy(disposition => disposition.Method, StringComparer.Ordinal)
            .ToArray();

        Assert.All(internalRoutes, route =>
            Assert.StartsWith("/api/repositories/{repositoryId:guid}/decision-sessions/analysis/", route.Pattern, StringComparison.Ordinal));
        Assert.Equal(
            [
                "GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/coherence",
                "GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/diagnostics",
                "GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/economics",
                "GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/metrics",
                "GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/statistics"
            ],
            internalRoutes.Select(route => $"{route.Method} {route.Pattern}").ToArray());
        Assert.Equal(
            ["GET /api/ping", "GET /api/repositories/{repositoryId:guid}/planning"],
            compatibilityRoutes.Select(route => $"{route.Method} {route.Pattern}").ToArray());
    }

    private static IEnumerable<RouteDisposition> CreateDispositions(RouteEndpoint endpoint)
    {
        string pattern = endpoint.RoutePattern.RawText ?? string.Empty;
        HttpMethodMetadata metadata = endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>();

        foreach (string method in metadata.HttpMethods)
        {
            yield return new RouteDisposition(method, pattern, ClassifyFamily(pattern), ClassifyDisposition(pattern));
        }
    }

    private static EndpointFamily ClassifyFamily(string pattern)
    {
        if (pattern == "/api/ping")
        {
            return EndpointFamily.Diagnostics;
        }

        if (pattern is "/api/repositories" or "/api/repositories/{repositoryId:guid}/workspace" or
            "/api/repositories/{repositoryId:guid}/refresh" or "/api/repositories/{repositoryId:guid}")
        {
            return EndpointFamily.Repositories;
        }

        if (pattern.StartsWith("/api/repositories/{repositoryId:guid}/artifacts", StringComparison.Ordinal))
        {
            return EndpointFamily.Artifacts;
        }

        if (pattern == "/api/repositories/{repositoryId:guid}/planning")
        {
            return EndpointFamily.Planning;
        }

        if (pattern.StartsWith("/api/repositories/{repositoryId:guid}/plan/", StringComparison.Ordinal))
        {
            return EndpointFamily.Plan;
        }

        if (pattern.StartsWith("/api/repositories/{repositoryId:guid}/workflow", StringComparison.Ordinal))
        {
            return EndpointFamily.Workflow;
        }

        if (pattern.StartsWith("/api/repositories/{repositoryId:guid}/decision-sessions", StringComparison.Ordinal))
        {
            return EndpointFamily.DecisionSessions;
        }

        if (pattern.StartsWith("/api/repositories/{repositoryId:guid}/decisions", StringComparison.Ordinal))
        {
            return EndpointFamily.Decisions;
        }

        if (pattern.StartsWith("/api/repositories/{repositoryId:guid}/decision/", StringComparison.Ordinal))
        {
            return EndpointFamily.DecisionRuntime;
        }

        if (pattern.StartsWith("/api/repositories/{repositoryId:guid}/execution", StringComparison.Ordinal) ||
            pattern.StartsWith("/api/execution-sessions", StringComparison.Ordinal) ||
            pattern.StartsWith("/api/repositories/{repositoryId:guid}/git", StringComparison.Ordinal))
        {
            return EndpointFamily.Execution;
        }

        if (pattern.StartsWith("/api/repositories/{repositoryId:guid}/operational-context", StringComparison.Ordinal))
        {
            return EndpointFamily.OperationalContext;
        }

        if (pattern.StartsWith("/api/repositories/{repositoryId:guid}/continuity", StringComparison.Ordinal))
        {
            return EndpointFamily.Continuity;
        }

        if (pattern.StartsWith("/api/repositories/{repositoryId:guid}/reasoning", StringComparison.Ordinal))
        {
            return EndpointFamily.Reasoning;
        }

        return EndpointFamily.Unknown;
    }

    private static EndpointDisposition ClassifyDisposition(string pattern)
    {
        if (pattern == "/api/ping" || pattern == "/api/repositories/{repositoryId:guid}/planning")
        {
            return EndpointDisposition.Compatibility;
        }

        if (pattern.StartsWith("/api/repositories/{repositoryId:guid}/decision-sessions/analysis/", StringComparison.Ordinal))
        {
            return EndpointDisposition.Internal;
        }

        return ClassifyFamily(pattern) == EndpointFamily.Unknown
            ? EndpointDisposition.Unknown
            : EndpointDisposition.Keep;
    }

    private sealed record RouteDisposition(
        string Method,
        string Pattern,
        EndpointFamily Family,
        EndpointDisposition Disposition);

    private enum EndpointFamily
    {
        Unknown,
        Diagnostics,
        Repositories,
        Artifacts,
        Planning,
        Plan,
        Workflow,
        DecisionSessions,
        Decisions,
        DecisionRuntime,
        Execution,
        OperationalContext,
        Continuity,
        Reasoning
    }

    private enum EndpointDisposition
    {
        Unknown,
        Keep,
        Compatibility,
        Internal,
        Redirect,
        Remove
    }
}
