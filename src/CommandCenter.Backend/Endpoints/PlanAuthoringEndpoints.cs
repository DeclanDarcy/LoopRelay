using System.Globalization;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using CommandCenter.Orchestration.Streaming;

namespace CommandCenter.Backend.Endpoints;

/// <summary>
/// Plan Authoring workflow (m3). Write/Revise drive a held-open Operational planning process whose
/// turn streams over <c>plan/stream</c>; Execute is the handoff seam to Phase 4. The UI sends inputs
/// only — prompt selection and rendering stay server-side via <c>CommandCenter.Core.Prompts</c>.
/// </summary>
public static class PlanAuthoringEndpoints
{
    public static IEndpointRouteBuilder MapPlanAuthoringEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapWritePlan();
        app.MapRevisePlan();
        app.MapStreamPlan();
        app.MapExecutePlan();
        app.MapStreamExecution();
        return app;
    }

    private static void MapWritePlan(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/plan/write", async (
            Guid repositoryId,
            PlanWriteRequest request,
            IRepositoryService repositoryService,
            RepositoryOrchestratorRegistry registry) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(repository.Id.ToString("D"));
                await orchestrator.BeginWritePlanAsync(repository, request);
                return Results.Accepted((string?)null, new PlanRunAcknowledgement("WritePlan"));
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
                return Results.Conflict(new { error = exception.Message });
            }
        });

    private static void MapRevisePlan(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/plan/revise", async (
            Guid repositoryId,
            PlanReviseRequest request,
            IRepositoryService repositoryService,
            RepositoryOrchestratorRegistry registry) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(repository.Id.ToString("D"));
                await orchestrator.BeginRevisePlanAsync(repository, request);
                return Results.Accepted((string?)null, new PlanRunAcknowledgement("RevisePlan"));
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
                return Results.Conflict(new { error = exception.Message });
            }
        });

    private static void MapExecutePlan(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/plan/execute", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            RepositoryOrchestratorRegistry registry) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(repository.Id.ToString("D"));
                await orchestrator.BeginExecutePlanAsync(repository);
                return Results.Accepted((string?)null, new PlanRunAcknowledgement("ExecutePlan"));
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

    private static void MapStreamPlan(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/plan/stream", async Task<IResult> (
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
                    orchestrator.PlanningStream.SubscribeAsync(afterSequence, httpContext.RequestAborted))
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

    private static void MapStreamExecution(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/execution/stream", async Task<IResult> (
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
                    orchestrator.ExecutionStream.SubscribeAsync(afterSequence, httpContext.RequestAborted))
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
