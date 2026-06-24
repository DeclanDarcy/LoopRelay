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
        DecisionSessionLifecycleEvaluation? lifecyclePolicy = await client.GetFromJsonAsync<DecisionSessionLifecycleEvaluation>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/lifecycle/policy",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionLifecycleDiagnostics? lifecyclePolicyDiagnostics = await client.GetFromJsonAsync<DecisionSessionLifecycleDiagnostics>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/lifecycle/policy/diagnostics",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionTransferEligibility? transferEligibility = await client.GetFromJsonAsync<DecisionSessionTransferEligibility>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/lifecycle/eligibility",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionTransferEligibilityDiagnostics? transferEligibilityDiagnostics = await client.GetFromJsonAsync<DecisionSessionTransferEligibilityDiagnostics>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/lifecycle/eligibility/diagnostics",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionLifecycleProjection? lifecycleProjection = await client.GetFromJsonAsync<DecisionSessionLifecycleProjection>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/lifecycle/projection",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionLifecycleHistory? lifecycleHistory = await client.GetFromJsonAsync<DecisionSessionLifecycleHistory>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/lifecycle/history",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionInfluenceTrace? influenceTrace = await client.GetFromJsonAsync<DecisionSessionInfluenceTrace>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/lifecycle/influence",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionHealthAssessment? health = await client.GetFromJsonAsync<DecisionSessionHealthAssessment>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/lifecycle/health",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionContinuityArtifact[]? continuityArtifacts = await client.GetFromJsonAsync<DecisionSessionContinuityArtifact[]>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/continuity-artifacts",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionTransfer[]? transfers = await client.GetFromJsonAsync<DecisionSessionTransfer[]>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/transfers",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionTransfer[]? transferHistory = await client.GetFromJsonAsync<DecisionSessionTransfer[]>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/transfers/history",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionTransferDiagnostics? transferDiagnostics = await client.GetFromJsonAsync<DecisionSessionTransferDiagnostics>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/transfers/diagnostics",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionRecoveryResult? recovery = await client.GetFromJsonAsync<DecisionSessionRecoveryResult>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/recovery",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionRecoveryHistory? recoveryHistory = await client.GetFromJsonAsync<DecisionSessionRecoveryHistory>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/recovery/history",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionRecoveryDiagnostics? recoveryDiagnostics = await client.GetFromJsonAsync<DecisionSessionRecoveryDiagnostics>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/recovery/diagnostics",
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
        Assert.NotNull(lifecyclePolicy);
        Assert.True(lifecyclePolicy.ReuseScore >= 0m);
        Assert.True(lifecyclePolicy.TransferScore >= 0m);
        Assert.NotNull(lifecyclePolicyDiagnostics);
        Assert.Equal(harness.Repository.Id, lifecyclePolicyDiagnostics.RepositoryId);
        Assert.NotNull(transferEligibility);
        Assert.Contains(transferEligibility.Status, new[]
        {
            DecisionSessionTransferEligibilityStatus.NotApplicable,
            DecisionSessionTransferEligibilityStatus.Eligible,
            DecisionSessionTransferEligibilityStatus.Blocked,
            DecisionSessionTransferEligibilityStatus.Deferred
        });
        Assert.NotNull(transferEligibilityDiagnostics);
        Assert.Equal(harness.Repository.Id, transferEligibilityDiagnostics.RepositoryId);
        Assert.NotNull(lifecycleProjection);
        Assert.Equal(harness.Repository.Id, lifecycleProjection.RepositoryId);
        Assert.NotNull(lifecycleProjection.ActiveSession);
        Assert.Equal(created.Id, lifecycleProjection.ActiveSession.Id);
        Assert.NotNull(lifecycleHistory);
        Assert.Equal(harness.Repository.Id, lifecycleHistory.RepositoryId);
        Assert.Contains(lifecycleHistory.Events, entry => entry.SessionId == created.Id && entry.EventType == DecisionSessionLifecycleHistoryEventType.Activated);
        Assert.NotNull(influenceTrace);
        Assert.Equal(harness.Repository.Id, influenceTrace.RepositoryId);
        Assert.Equal(created.Id, influenceTrace.ActiveSessionId);
        Assert.NotNull(health);
        Assert.Equal(harness.Repository.Id, health.RepositoryId);
        Assert.Contains(health.Dimensions, dimension => dimension.Name == "Registry");
        Assert.NotNull(continuityArtifacts);
        Assert.Empty(continuityArtifacts);
        Assert.NotNull(transfers);
        Assert.Empty(transfers);
        Assert.NotNull(transferHistory);
        Assert.Empty(transferHistory);
        Assert.NotNull(transferDiagnostics);
        Assert.Equal(harness.Repository.Id, transferDiagnostics.RepositoryId);
        Assert.NotNull(recovery);
        Assert.Equal(created.Id, recovery.ActiveSessionId);
        Assert.NotNull(recoveryHistory);
        Assert.Equal(harness.Repository.Id, recoveryHistory.RepositoryId);
        Assert.NotNull(recoveryDiagnostics);
        Assert.Equal(harness.Repository.Id, recoveryDiagnostics.RepositoryId);
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
