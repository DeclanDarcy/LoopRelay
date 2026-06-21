using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Middle.Projections;

namespace CommandCenter.Backend.Endpoints;

public static class ArtifactsEndpoints
{
    public static IEndpointRouteBuilder MapArtifactsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetArtifactInventory();
        app.MapGetArtifactContent();
        app.MapSaveArtifactContent();
        app.MapRotateCurrentHandoff();
        app.MapRotateCurrentDecisions();
        return app;
    }

    private static void MapGetArtifactInventory(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/artifacts", async (
            Guid repositoryId,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(repositoryId);
                return Results.Ok(workspace.ArtifactInventory);
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });

    private static void MapGetArtifactContent(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/artifacts/content", async (
            Guid repositoryId,
            string relativePath,
            IRepositoryService repositoryService,
            IArtifactService artifactService) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                return Results.Text(await artifactService.LoadAsync(repository, relativePath), "text/markdown");
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (FileNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapSaveArtifactContent(this IEndpointRouteBuilder app) =>
        app.MapPut("/api/repositories/{repositoryId:guid}/artifacts/content", async (
            Guid repositoryId,
            SaveArtifactContentRequest request,
            IRepositoryService repositoryService,
            IArtifactService artifactService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                await artifactService.SaveAsync(repository, request.RelativePath, request.Content);
                await projectionService.RefreshWorkspaceAsync(repositoryId);
                return Results.NoContent();
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

    private static void MapRotateCurrentHandoff(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/artifacts/rotate-current-handoff", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            IArtifactRotationService rotationService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                await rotationService.RotateCurrentHandoffAsync(repository);
                return Results.Ok(await projectionService.RefreshWorkspaceAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (FileNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (IOException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });

    private static void MapRotateCurrentDecisions(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/artifacts/rotate-current-decisions", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            IArtifactRotationService rotationService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                await rotationService.RotateCurrentDecisionsAsync(repository);
                return Results.Ok(await projectionService.RefreshWorkspaceAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (FileNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (IOException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });
}
