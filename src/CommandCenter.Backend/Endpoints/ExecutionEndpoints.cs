using CommandCenter.Execution;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;

namespace CommandCenter.Backend.Endpoints;

public static class ExecutionEndpoints
{
    public static IEndpointRouteBuilder MapExecutionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetExecutionContext();
        app.MapStartExecution();
        app.MapCancelExecution();
        app.MapGetActiveExecution();
        return app;
    }

    private static void MapGetExecutionContext(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/execution/context", async (
            Guid repositoryId,
            IExecutionContextService executionContextService) =>
        {
            try
            {
                return Results.Ok(await executionContextService.BuildContextAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapStartExecution(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/execution/start", async (
            Guid repositoryId,
            ExecutionStartRequest request,
            IExecutionSessionService executionSessionService) =>
        {
            try
            {
                return Results.Ok(await executionSessionService.StartAsync(repositoryId, request));
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

    private static void MapCancelExecution(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/execution/cancel", async (
            Guid repositoryId,
            ExecutionCancellationRequest request,
            IExecutionSessionService executionSessionService) =>
        {
            try
            {
                return Results.Ok(await executionSessionService.CancelAsync(repositoryId, request));
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

    private static void MapGetActiveExecution(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/execution/active", async (
            Guid repositoryId,
            IExecutionSessionService executionSessionService) =>
        {
            ExecutionSessionSummary? session = await executionSessionService.GetActiveSessionAsync(repositoryId);
            return session is null ? Results.NotFound(new { error = "No active execution session." }) : Results.Ok(session);
        });
}
