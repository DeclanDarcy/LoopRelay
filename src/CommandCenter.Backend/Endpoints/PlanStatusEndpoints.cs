using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;

namespace CommandCenter.Backend.Endpoints;

/// <summary>
/// Repository visibility gate (m2). <c>GET /api/repositories/{id}/plan/status</c> returns
/// <c>{ planExists, state }</c>; <c>planExists == false</c> drives the Plan Authoring screen.
/// </summary>
public static class PlanStatusEndpoints
{
    public static IEndpointRouteBuilder MapPlanStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetPlanStatus();
        return app;
    }

    private static void MapGetPlanStatus(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/plan/status", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            RepositoryOrchestratorRegistry registry) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(repository.Id.ToString("D"));
                PlanStatus status = await orchestrator.GetPlanStatusAsync(repository);
                return Results.Ok(status);
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });
}
