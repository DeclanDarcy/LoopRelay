using System.Net;
using System.Net.Http.Json;
using CommandCenter.Backend;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.Workflow.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionSessionEndpointTests
{
    [Fact]
    public async Task WorkflowDecisionSessionEndpointsAreReadOnlyAndDoNotMutateLifecycleState()
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

        string registryBefore = await ReadRequiredAsync(harness, DecisionSessionArtifactPaths.RegistryJson());
        IReadOnlyList<DecisionSessionTransfer> transfersBefore = await harness.RepositoryStore.ListTransfersAsync(harness.Repository);
        IReadOnlyList<DecisionSessionContinuityArtifact> artifactsBefore =
            await harness.RepositoryStore.ListContinuityArtifactsAsync(harness.Repository);
        IReadOnlyList<string> decisionSessionArtifactsBefore = await harness.Store.ListAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, Path.Combine(".agents", "decision-sessions")),
            "*");

        HttpResponseMessage projectionResponse = await client.GetAsync(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/workflow");
        HttpResponseMessage healthResponse = await client.GetAsync(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/workflow/health");
        HttpResponseMessage influenceResponse = await client.GetAsync(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/workflow/influence");
        HttpResponseMessage summaryResponse = await client.GetAsync(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/workflow/summary");

        string registryAfter = await ReadRequiredAsync(harness, DecisionSessionArtifactPaths.RegistryJson());
        IReadOnlyList<DecisionSessionTransfer> transfersAfter = await harness.RepositoryStore.ListTransfersAsync(harness.Repository);
        IReadOnlyList<DecisionSessionContinuityArtifact> artifactsAfter =
            await harness.RepositoryStore.ListContinuityArtifactsAsync(harness.Repository);
        IReadOnlyList<string> decisionSessionArtifactsAfter = await harness.Store.ListAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, Path.Combine(".agents", "decision-sessions")),
            "*");

        Assert.True(projectionResponse.IsSuccessStatusCode);
        Assert.True(healthResponse.IsSuccessStatusCode);
        Assert.True(influenceResponse.IsSuccessStatusCode);
        Assert.True(summaryResponse.IsSuccessStatusCode);
        Assert.Equal(registryBefore, registryAfter);
        Assert.Equal(transfersBefore, transfersAfter);
        Assert.Equal(artifactsBefore, artifactsAfter);
        Assert.Equal(decisionSessionArtifactsBefore, decisionSessionArtifactsAfter);
    }

    [Fact]
    public async Task BackendDoesNotExposeWorkflowLifecycleMutationRoutes()
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

        RouteEndpoint[] workflowDecisionSessionEndpoints = app.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Contains("/decision-sessions/workflow", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Equal(4, workflowDecisionSessionEndpoints.Length);
        Assert.All(workflowDecisionSessionEndpoints, endpoint =>
        {
            HttpMethodMetadata metadata = endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>();
            Assert.Equal(["GET"], metadata.HttpMethods);
            Assert.DoesNotContain("transfer", endpoint.RoutePattern.RawText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("activate", endpoint.RoutePattern.RawText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("retire", endpoint.RoutePattern.RawText, StringComparison.OrdinalIgnoreCase);
        });
    }

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
        HttpResponseMessage blockedTransferResponse = await client.PostAsync(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/transfers",
            null);
        DecisionSessionTransfer[]? transferHistory = await client.GetFromJsonAsync<DecisionSessionTransfer[]>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/transfers/history",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionTransferDiagnostics? transferDiagnostics = await client.GetFromJsonAsync<DecisionSessionTransferDiagnostics>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/transfers/diagnostics",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionRecoveryResult? recovery = await client.GetFromJsonAsync<DecisionSessionRecoveryResult>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/recovery",
            DecisionSessionTestHarness.CreateJsonOptions());
        HttpResponseMessage persistedRecoveryResponse = await client.PostAsync(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/recovery",
            null);
        DecisionSessionRecoveryResult? persistedRecovery = await persistedRecoveryResponse.Content.ReadFromJsonAsync<DecisionSessionRecoveryResult>(
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionRecoveryHistory? recoveryHistory = await client.GetFromJsonAsync<DecisionSessionRecoveryHistory>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/recovery/history",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionRecoveryDiagnostics? recoveryDiagnostics = await client.GetFromJsonAsync<DecisionSessionRecoveryDiagnostics>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/recovery/diagnostics",
            DecisionSessionTestHarness.CreateJsonOptions());
        WorkflowDecisionSessionProjection? workflowProjection = await client.GetFromJsonAsync<WorkflowDecisionSessionProjection>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/workflow",
            DecisionSessionTestHarness.CreateJsonOptions());
        WorkflowGovernanceHealthProjection? workflowHealth = await client.GetFromJsonAsync<WorkflowGovernanceHealthProjection>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/workflow/health",
            DecisionSessionTestHarness.CreateJsonOptions());
        WorkflowGovernanceInfluenceProjection? workflowInfluence = await client.GetFromJsonAsync<WorkflowGovernanceInfluenceProjection>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/workflow/influence",
            DecisionSessionTestHarness.CreateJsonOptions());
        WorkflowGovernanceSummary? workflowSummary = await client.GetFromJsonAsync<WorkflowGovernanceSummary>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/workflow/summary",
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionCertificationReport? currentCertification = await client.GetFromJsonAsync<DecisionSessionCertificationReport>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/certification/report",
            DecisionSessionTestHarness.CreateJsonOptions());
        HttpResponseMessage runCertificationResponse = await client.PostAsync(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/certification",
            null);
        DecisionSessionCertificationReport? persistedCertification = await runCertificationResponse.Content.ReadFromJsonAsync<DecisionSessionCertificationReport>(
            DecisionSessionTestHarness.CreateJsonOptions());
        DecisionSessionCertificationReport? latestCertification = await client.GetFromJsonAsync<DecisionSessionCertificationReport>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/certification",
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
        Assert.Equal(HttpStatusCode.Conflict, blockedTransferResponse.StatusCode);
        Assert.NotNull(transferHistory);
        Assert.Empty(transferHistory);
        Assert.NotNull(transferDiagnostics);
        Assert.Equal(harness.Repository.Id, transferDiagnostics.RepositoryId);
        Assert.NotNull(recovery);
        Assert.Equal(created.Id, recovery.ActiveSessionId);
        Assert.True(persistedRecoveryResponse.IsSuccessStatusCode);
        Assert.NotNull(persistedRecovery);
        Assert.Equal(created.Id, persistedRecovery.ActiveSessionId);
        Assert.NotNull(recoveryHistory);
        Assert.Equal(harness.Repository.Id, recoveryHistory.RepositoryId);
        Assert.Contains(recoveryHistory.Results, result => result.RecoveryId == persistedRecovery.RecoveryId);
        Assert.NotNull(recoveryDiagnostics);
        Assert.Equal(harness.Repository.Id, recoveryDiagnostics.RepositoryId);
        Assert.NotNull(workflowProjection);
        Assert.Equal(harness.Repository.Id, workflowProjection.RepositoryId);
        Assert.Equal(created.Id.ToString(), workflowProjection.DecisionSessionId);
        Assert.NotNull(workflowProjection.Summary);
        Assert.NotNull(workflowProjection.Readiness);
        Assert.NotNull(workflowProjection.Diagnostics);
        Assert.NotNull(workflowHealth);
        Assert.Equal("Decision sessions", workflowHealth.Name);
        Assert.NotNull(workflowInfluence);
        Assert.Equal(harness.Repository.Id, workflowInfluence.RepositoryId);
        Assert.NotNull(workflowSummary);
        Assert.Equal(created.Id.ToString(), workflowSummary.DecisionSessionId);
        Assert.NotNull(currentCertification);
        Assert.Equal(harness.Repository.Id, currentCertification.RepositoryId);
        Assert.Contains(currentCertification.Result.Findings, finding => finding.Id == "registry-single-active-session");
        Assert.True(runCertificationResponse.IsSuccessStatusCode);
        Assert.NotNull(persistedCertification);
        Assert.Equal(harness.Repository.Id, persistedCertification.RepositoryId);
        Assert.NotNull(latestCertification);
        Assert.Equal(persistedCertification.ReportId, latestCertification.ReportId);
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

    private static async Task<string> ReadRequiredAsync(DecisionSessionTestHarness harness, string relativePath)
    {
        string? content = await harness.Store.ReadAsync(DecisionSessionArtifactPaths.Resolve(harness.Repository, relativePath));
        return content ?? throw new InvalidOperationException($"Expected artifact was missing: {relativePath}");
    }
}
