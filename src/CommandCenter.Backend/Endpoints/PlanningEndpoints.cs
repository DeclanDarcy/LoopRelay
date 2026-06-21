using CommandCenter.Core.Planning;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Backend.Endpoints;

public static class PlanningEndpoints
{
    public static IEndpointRouteBuilder MapPlanningEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetPlanning();
        return app;
    }

    private static void MapGetPlanning(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/planning", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            IPlanningService planningService) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                IReadOnlyList<Milestone> milestones = await planningService.GetMilestonesAsync(repository);
                return Results.Ok(new PlanningProjection
                {
                    HasPlan = await planningService.HasPlanAsync(repository),
                    Milestones = milestones,
                    Readiness = await planningService.DetermineReadinessAsync(repository)
                });
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });
}
