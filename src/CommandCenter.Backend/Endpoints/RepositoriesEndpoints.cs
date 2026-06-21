using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Middle.Projections;

namespace CommandCenter.Backend.Endpoints;

public static class RepositoriesEndpoints
{
    public static IEndpointRouteBuilder MapRepositoriesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetRepositories();
        app.MapRegisterRepository();
        app.MapRemoveRepository();
        app.MapGetWorkspace();
        app.MapRefreshWorkspace();
        return app;
    }

    private static void MapGetRepositories(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories", async (IRepositoryProjectionService projectionService) =>
            await projectionService.GetDashboardAsync());

    private static void MapRegisterRepository(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories", async (RegisterRepositoryRequest request, IRepositoryService repositoryService) =>
        {
            try
            {
                Repository repository = await repositoryService.RegisterAsync(request.Path);
                return Results.Created($"/api/repositories/{repository.Id}", repository);
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (DirectoryNotFoundException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (UnauthorizedAccessException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (IOException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapRemoveRepository(this IEndpointRouteBuilder app) =>
        app.MapDelete("/api/repositories/{repositoryId:guid}", async (Guid repositoryId, IRepositoryService repositoryService) =>
        {
            await repositoryService.RemoveAsync(repositoryId);
            return Results.NoContent();
        });

    private static void MapGetWorkspace(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workspace", async (
            Guid repositoryId,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                return Results.Ok(await projectionService.GetWorkspaceAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });

    private static void MapRefreshWorkspace(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/refresh", async (
            Guid repositoryId,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                return Results.Ok(await projectionService.RefreshWorkspaceAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });
}
