using System.Net;
using System.Net.Http.Json;
using CommandCenter.Backend;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionSessionEndpointTests
{
    [Fact]
    public async Task EndpointsReturnSessionsActiveAndDiagnostics()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        await using WebApplication app = Program.CreateApp(
            [],
            services =>
            {
                services.RemoveAll<IArtifactStore>();
                services.RemoveAll<IRepositoryService>();
                services.AddSingleton<IArtifactStore>(harness.Store);
                services.AddSingleton<IRepositoryService>(harness.RepositoryService);
            });
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);

        DecisionSessionProjection[]? sessions = await client.GetFromJsonAsync<DecisionSessionProjection[]>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionProjection? active = await client.GetFromJsonAsync<DecisionSessionProjection>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/active",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionDiagnostics? diagnostics = await client.GetFromJsonAsync<DecisionSessionDiagnostics>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/diagnostics",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionMetrics? metrics = await client.GetFromJsonAsync<DecisionSessionMetrics>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/analysis/metrics",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionEconomics? economics = await client.GetFromJsonAsync<DecisionSessionEconomics>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/analysis/economics",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionCoherence? coherence = await client.GetFromJsonAsync<DecisionSessionCoherence>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/analysis/coherence",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionAnalysisDiagnostics? analysisDiagnostics = await client.GetFromJsonAsync<DecisionSessionAnalysisDiagnostics>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/analysis/diagnostics",
            DecisionSessionTestHarness.CreateJsonOptions());

        Assert.NotNull(sessions);
        Assert.Single(sessions);
        Assert.NotNull(active);
        Assert.Equal(created.Id, active.Id);
        Assert.NotNull(diagnostics);
        Assert.True(diagnostics.IsValid);
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.DecisionCount);
        Assert.NotNull(economics);
        Assert.True(economics.EstimatedReuseValue >= 0m);
        Assert.NotNull(coherence);
        Assert.True(coherence.CoherenceScore >= 0m);
        Assert.NotNull(analysisDiagnostics);
        Assert.Equal(harness.Repository.Id, analysisDiagnostics.RepositoryId);
        Assert.NotNull(analysisDiagnostics.Metrics);
        Assert.NotNull(analysisDiagnostics.Economics);
        Assert.NotNull(analysisDiagnostics.Coherence);
    }

    [Fact]
    public async Task MissingRepositoryEndpointReturnsNotFound()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        await using WebApplication app = Program.CreateApp(
            [],
            services =>
            {
                services.RemoveAll<IArtifactStore>();
                services.RemoveAll<IRepositoryService>();
                services.AddSingleton<IArtifactStore>(harness.Store);
                services.AddSingleton<IRepositoryService>(harness.RepositoryService);
            });
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();

        HttpResponseMessage response = await client.GetAsync($"{root}/api/repositories/{Guid.NewGuid()}/decision-sessions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
