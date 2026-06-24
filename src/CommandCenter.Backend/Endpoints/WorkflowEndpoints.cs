using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;

namespace CommandCenter.Backend.Endpoints;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetWorkflow();
        app.MapGetWorkflowDiagnostics();
        app.MapGetWorkflowTimeline();
        app.MapGetWorkflowHistory();
        app.MapGetWorkflowTransitions();
        app.MapGetWorkflowGates();
        app.MapGetWorkflowGateHistory();
        app.MapGetWorkflowRecovery();
        app.MapPostWorkflowRecover();
        return app;
    }

    private static void MapGetWorkflow(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow", async (
            Guid repositoryId,
            IWorkflowProjectionService workflowProjectionService) =>
        {
            try
            {
                return Results.Ok(await workflowProjectionService.ProjectAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapGetWorkflowHistory(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/history", async (
            Guid repositoryId,
            IWorkflowProjectionService workflowProjectionService,
            IWorkflowRecoveryService workflowRecoveryService) =>
        {
            try
            {
                var projection = await workflowProjectionService.ProjectAsync(repositoryId);
                var recovery = await workflowRecoveryService.RecoverCurrentWorkflowAsync(repositoryId);
                return Results.Ok(new WorkflowHistoryProjection(
                    repositoryId,
                    recovery.Timeline,
                    projection.BlockedTransitions
                        .Where(transition => transition.GateResolution is not null)
                        .Select(transition => $"{transition.GateResolution!.GateType}: {transition.GateResolution.RequiredHumanAction}")
                        .ToArray(),
                    projection.Diagnostics.Reasoning,
                    [recovery.Diagnostics]));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapGetWorkflowDiagnostics(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/diagnostics", async (
            Guid repositoryId,
            IWorkflowProjectionService workflowProjectionService) =>
        {
            try
            {
                return Results.Ok(await workflowProjectionService.GetDiagnosticsAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapGetWorkflowTimeline(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/timeline", async (
            Guid repositoryId,
            IWorkflowProjectionService workflowProjectionService) =>
        {
            try
            {
                return Results.Ok(await workflowProjectionService.GetTimelineAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapGetWorkflowTransitions(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/transitions", async (
            Guid repositoryId,
            IWorkflowProjectionService workflowProjectionService) =>
        {
            try
            {
                var projection = await workflowProjectionService.ProjectAsync(repositoryId);
                return Results.Ok(projection.Diagnostics.StateMachine);
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapGetWorkflowGates(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/gates", async (
            Guid repositoryId,
            IWorkflowGateCatalogService workflowGateCatalogService) =>
        {
            try
            {
                return Results.Ok(await workflowGateCatalogService.GetGatesAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapGetWorkflowGateHistory(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/gates/history", async (
            Guid repositoryId,
            IWorkflowGateCatalogService workflowGateCatalogService) =>
        {
            try
            {
                return Results.Ok(await workflowGateCatalogService.GetGateHistoryAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapGetWorkflowRecovery(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/recovery", async (
            Guid repositoryId,
            IWorkflowRecoveryService workflowRecoveryService) =>
        {
            try
            {
                return Results.Ok(await workflowRecoveryService.ValidateRecoveredWorkflowAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapPostWorkflowRecover(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/workflow/recover", async (
            Guid repositoryId,
            IWorkflowRecoveryService workflowRecoveryService) =>
        {
            try
            {
                return Results.Ok(await workflowRecoveryService.RecoverCurrentWorkflowAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });
}
