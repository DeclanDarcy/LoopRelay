using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Services;

namespace CommandCenter.Backend.Endpoints;

/// <summary>
/// Conversation projection (m6). A read-only timeline of the Plan Authoring -> Execution -> Decision loop —
/// planning, operational output, decision output, submit, and continuation entries — scoped to THIS flow and
/// deliberately NOT a repository knowledge platform. Sourced from the orchestrator's append-only projection.
/// </summary>
public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/repositories/{repositoryId:guid}/conversation", async Task<IResult> (
            Guid repositoryId,
            IRepositoryService repositoryService,
            RepositoryOrchestratorRegistry registry) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                RepositoryOrchestrator orchestrator = await registry.GetOrCreateAsync(repository.Id.ToString("D"));
                return Results.Ok(orchestrator.Conversation);
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });

        return app;
    }
}
