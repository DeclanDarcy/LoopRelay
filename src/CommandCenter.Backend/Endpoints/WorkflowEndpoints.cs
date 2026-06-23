using CommandCenter.Workflow.Abstractions;

namespace CommandCenter.Backend.Endpoints;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetWorkflow();
        app.MapGetWorkflowDiagnostics();
        app.MapGetWorkflowTimeline();
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
}
