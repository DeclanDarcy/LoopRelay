using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;

namespace CommandCenter.Backend.Endpoints;

public static class DecisionEndpoints
{
    public static IEndpointRouteBuilder MapDecisionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetDecisionContext();
        app.MapCreateDecisionContextSnapshot();
        app.MapListDecisionContextSnapshots();
        return app;
    }

    private static void MapGetDecisionContext(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/context", async (
            Guid repositoryId,
            IDecisionContextService contextService) =>
        {
            try
            {
                return Results.Ok(await contextService.BuildContextAsync(repositoryId));
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

    private static void MapCreateDecisionContextSnapshot(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/context", async (
            Guid repositoryId,
            IDecisionContextService contextService) =>
        {
            try
            {
                DecisionContextSnapshot snapshot = await contextService.CreateSnapshotAsync(repositoryId);
                return Results.Ok(snapshot);
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

    private static void MapListDecisionContextSnapshots(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/context/snapshots", async (
            Guid repositoryId,
            IDecisionContextService contextService) =>
        {
            try
            {
                return Results.Ok(await contextService.ListSnapshotsAsync(repositoryId));
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
}
