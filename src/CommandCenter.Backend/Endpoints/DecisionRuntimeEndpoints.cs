using System.Globalization;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using CommandCenter.Orchestration.Streaming;

namespace CommandCenter.Backend.Endpoints;

/// <summary>
/// Decision Runtime (m5). <c>decision/run</c> drives the held-open zero-permission Decision process — it
/// seeds the session with the operational context (off-stream) then proposes decisions over the latest
/// execution handoff, streaming to <c>decision/stream</c>. <c>decision/submit</c> is the human review gate:
/// the only path by which the captured (reviewed/edited) decisions are persisted. The UI sends commands and
/// the reviewed text only — prompt selection and rendering stay server-side via <c>CommandCenter.Core.Prompts</c>.
/// </summary>
public static class DecisionRuntimeEndpoints
{
    public static IEndpointRouteBuilder MapDecisionRuntimeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapDecisionRun();
        app.MapSubmitDecision();
        app.MapStreamDecision();
        return app;
    }

    private static void MapDecisionRun(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decision/run", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            RepositoryOrchestratorRegistry registry) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(repository.Id.ToString("D"));
                await orchestrator.BeginDecisionRunAsync(repository);
                return Results.Accepted((string?)null, new PlanRunAcknowledgement("DecisionRun"));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });

    private static void MapSubmitDecision(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decision/submit", async (
            Guid repositoryId,
            DecisionSubmitRequest request,
            IRepositoryService repositoryService,
            RepositoryOrchestratorRegistry registry) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(repository.Id.ToString("D"));
                await orchestrator.BeginSubmitDecisionsAsync(repository, request.Decisions);
                return Results.Accepted((string?)null, new PlanRunAcknowledgement("SubmitDecisions"));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                // A disposed orchestrator (ObjectDisposedException : InvalidOperationException) surfaces as a
                // recoverable 409 — matching the sibling decision/run endpoint rather than an opaque 500.
                return Results.Conflict(new { error = exception.Message });
            }
        });

    private static void MapStreamDecision(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decision/stream", async Task<IResult> (
            Guid repositoryId,
            HttpContext httpContext,
            IRepositoryService repositoryService,
            RepositoryOrchestratorRegistry registry) =>
        {
            Repository repository;
            try
            {
                repository = await repositoryService.GetRepositoryAsync(repositoryId);
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }

            RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(repository.Id.ToString("D"));

            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.ContentType = "text/event-stream";

            long afterSequence = ParseLastEventId(httpContext.Request.Headers["Last-Event-ID"]);

            try
            {
                await foreach (OrchestratorStreamEvent streamEvent in
                    orchestrator.DecisionStream.SubscribeAsync(afterSequence, httpContext.RequestAborted))
                {
                    await httpContext.Response.WriteAsync($"id: {streamEvent.Sequence}\n", httpContext.RequestAborted);
                    await httpContext.Response.WriteAsync($"event: {streamEvent.Type}\n", httpContext.RequestAborted);
                    await httpContext.Response.WriteAsync($"data: {streamEvent.Data}\n\n", httpContext.RequestAborted);
                    await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
                }
            }
            catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
            {
            }

            return Results.Empty;
        });

    private static long ParseLastEventId(string? lastEventId) =>
        long.TryParse(lastEventId, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sequence) && sequence > 0
            ? sequence
            : 0;
}
