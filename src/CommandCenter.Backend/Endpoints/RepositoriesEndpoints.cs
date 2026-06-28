using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Middle.Projections;
using CommandCenter.Orchestration.Services;

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
        app.MapDelete("/api/repositories/{repositoryId:guid}", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            RepositoryOrchestratorRegistry orchestratorRegistry) =>
        {
            // m10 removal teardown (additive): tear down any LIVE orchestrator for this repository — which disposes
            // its held-open planning/decision codex processes — BEFORE rewriting config, so removing a repository
            // never leaks a live process. RemoveAsync is best-effort/bounded (no-op if nothing is live), so the
            // endpoint keeps its existing NoContent contract and response shape unchanged. The registry is keyed by
            // the repository id formatted as "D", matching every other orchestrator endpoint.
            // NOTE on "deselection": there is intentionally NO backend deselection endpoint — a pure UI re-select
            // reuses the still-warm process by design. This teardown covers explicit removal (here) and app
            // shutdown (OrchestratorShutdownHostedService) only; it does not exist to support deselection.
            await orchestratorRegistry.RemoveAsync(repositoryId.ToString("D"));
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
