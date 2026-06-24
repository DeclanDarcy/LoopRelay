using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.Backend.Endpoints;

public static class DecisionSessionEndpoints
{
    public static IEndpointRouteBuilder MapDecisionSessionEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/repositories/{repositoryId:guid}/decision-sessions");

        group.MapGet("", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            IDecisionSessionRepository sessionRepository) =>
            await HandleAsync(async () =>
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                return (await sessionRepository.ListAsync(repository))
                    .Select(DecisionSessionProjection.FromSession)
                    .ToArray();
            }));

        group.MapGet("/active", async (
            Guid repositoryId,
            IDecisionSessionRegistry registry) =>
            await HandleAsync(async () =>
            {
                DecisionSession? session = await registry.GetActiveSessionAsync(repositoryId);
                return session is null ? null : DecisionSessionProjection.FromSession(session);
            }));

        group.MapGet("/diagnostics", async (
            Guid repositoryId,
            IDecisionSessionRecoveryService recoveryService) =>
            await HandleAsync(() => recoveryService.GetDiagnosticsAsync(repositoryId)));

        group.MapGet("/analysis/metrics", async (
            Guid repositoryId,
            IDecisionSessionMetricsService metricsService) =>
            await HandleAsync(async () => (await metricsService.GetMetricsAsync(repositoryId)).Metrics));

        group.MapGet("/analysis/statistics", async (
            Guid repositoryId,
            IDecisionSessionMetricsService metricsService) =>
            await HandleAsync(async () => (await metricsService.GetMetricsAsync(repositoryId)).Statistics));

        group.MapGet("/analysis/economics", async (
            Guid repositoryId,
            IDecisionSessionEconomicsService economicsService) =>
            await HandleAsync(async () => (await economicsService.GetEconomicsAsync(repositoryId)).Economics));

        group.MapGet("/analysis/coherence", async (
            Guid repositoryId,
            IDecisionSessionCoherenceService coherenceService) =>
            await HandleAsync(async () => (await coherenceService.GetCoherenceAsync(repositoryId)).Coherence));

        group.MapGet("/analysis/diagnostics", async (
            Guid repositoryId,
            IDecisionSessionMetricsService metricsService,
            IDecisionSessionEconomicsService economicsService,
            IDecisionSessionCoherenceService coherenceService) =>
            await HandleAsync(async () =>
            {
                DecisionSessionMetricsSnapshot metrics = await metricsService.GetMetricsAsync(repositoryId);
                DecisionSessionEconomicsSnapshot economics = await economicsService.GetEconomicsAsync(repositoryId);
                DecisionSessionCoherenceSnapshot coherence = await coherenceService.GetCoherenceAsync(repositoryId);
                return new DecisionSessionAnalysisDiagnostics(
                    repositoryId,
                    coherence.GeneratedAt,
                    metrics.Diagnostics,
                    economics.Diagnostics,
                    coherence.Diagnostics,
                    metrics.Diagnostics.Warnings
                        .Concat(economics.Diagnostics.Warnings)
                        .Concat(coherence.Diagnostics.Warnings)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray());
            }));

        group.MapGet("/lifecycle/policy", async (
            Guid repositoryId,
            IDecisionSessionLifecyclePolicy lifecyclePolicy) =>
            await HandleAsync(async () => (await lifecyclePolicy.EvaluateAsync(repositoryId)).Evaluation));

        group.MapGet("/lifecycle/policy/diagnostics", async (
            Guid repositoryId,
            IDecisionSessionLifecyclePolicy lifecyclePolicy) =>
            await HandleAsync(async () => (await lifecyclePolicy.EvaluateAsync(repositoryId)).Diagnostics));

        group.MapGet("/lifecycle/eligibility", async (
            Guid repositoryId,
            IDecisionSessionTransferEligibilityService eligibilityService) =>
            await HandleAsync(async () => (await eligibilityService.CheckAsync(repositoryId)).Eligibility));

        group.MapGet("/lifecycle/eligibility/diagnostics", async (
            Guid repositoryId,
            IDecisionSessionTransferEligibilityService eligibilityService) =>
            await HandleAsync(async () => (await eligibilityService.CheckAsync(repositoryId)).Diagnostics));

        group.MapGet("/continuity-artifacts", async (
            Guid repositoryId,
            IDecisionSessionContinuityArtifactService artifactService) =>
            await HandleAsync(() => artifactService.ListAsync(repositoryId)));

        group.MapGet("/continuity-artifacts/{artifactId}", async (
            Guid repositoryId,
            string artifactId,
            IDecisionSessionContinuityArtifactService artifactService) =>
            await HandleAsync(async () =>
            {
                DecisionSessionContinuityArtifact? artifact = await artifactService.GetAsync(repositoryId, artifactId);
                return artifact ?? throw new KeyNotFoundException($"Decision session continuity artifact was not found: {artifactId}");
            }));

        return app;
    }

    private static async Task<IResult> HandleAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DecisionSessionConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
        catch (DecisionSessionValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}
