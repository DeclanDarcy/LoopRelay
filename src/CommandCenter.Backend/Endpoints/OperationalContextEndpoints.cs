using CommandCenter.Continuity;
using CommandCenter.Continuity.Abstractions;
using CommandCenter.Continuity.Models;
using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Backend.Services;
using CommandCenter.Middle.Projections;

namespace CommandCenter.Backend.Endpoints;

public static class OperationalContextEndpoints
{
    public static IEndpointRouteBuilder MapOperationalContextEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGenerateOperationalContext();
        app.MapListProposals();
        app.MapGetProposal();
        app.MapEditProposalContent();
        app.MapAcceptProposal();
        app.MapRejectProposal();
        app.MapPromoteProposal();
        return app;
    }

    private static void MapGenerateOperationalContext(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/operational-context/generate", async (
            Guid repositoryId,
            IOperationalContextGenerationService generationService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                OperationalContextProposal proposal = await generationService.GenerateAsync(repositoryId);
                await projectionService.RefreshWorkspaceAsync(repositoryId);
                return Results.Ok(proposal);
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

    private static void MapListProposals(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/operational-context/proposals", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            IOperationalContextProposalStore proposalStore) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                return Results.Ok(await proposalStore.ListAsync(repository));
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

    private static void MapGetProposal(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/operational-context/proposals/{proposalId}", async (
            Guid repositoryId,
            string proposalId,
            IRepositoryService repositoryService,
            IOperationalContextProposalStore proposalStore) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                OperationalContextProposal? proposal = await proposalStore.GetAsync(repository, proposalId, includeContent: true);
                return proposal is null
                    ? Results.NotFound(new { error = $"Operational-context proposal was not found: {proposalId}" })
                    : Results.Ok(proposal);
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

    private static void MapEditProposalContent(this IEndpointRouteBuilder app) =>
        app.MapPut("/api/repositories/{repositoryId:guid}/operational-context/proposals/{proposalId}/content", async (
            Guid repositoryId,
            string proposalId,
            OperationalContextProposalContentRequest request,
            IOperationalContextReviewService reviewService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                OperationalContextProposal proposal = await reviewService.EditAsync(repositoryId, proposalId, request.Content);
                await projectionService.RefreshWorkspaceAsync(repositoryId);
                return Results.Ok(proposal);
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

    private static void MapAcceptProposal(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/operational-context/proposals/{proposalId}/accept", async (
            Guid repositoryId,
            string proposalId,
            OperationalContextProposalReviewRequest request,
            IOperationalContextReviewService reviewService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                OperationalContextProposal proposal = await reviewService.AcceptAsync(repositoryId, proposalId, request.ReviewNote);
                await projectionService.RefreshWorkspaceAsync(repositoryId);
                return Results.Ok(proposal);
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

    private static void MapRejectProposal(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/operational-context/proposals/{proposalId}/reject", async (
            Guid repositoryId,
            string proposalId,
            OperationalContextProposalReviewRequest request,
            IOperationalContextReviewService reviewService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                OperationalContextProposal proposal = await reviewService.RejectAsync(repositoryId, proposalId, request.ReviewNote);
                await projectionService.RefreshWorkspaceAsync(repositoryId);
                return Results.Ok(proposal);
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

    private static void MapPromoteProposal(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/operational-context/proposals/{proposalId}/promote", async (
            Guid repositoryId,
            string proposalId,
            IOperationalContextLifecycleService lifecycleService,
            IDecisionReasoningCaptureService reasoningCaptureService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                OperationalContextProposal proposal = await lifecycleService.PromoteAsync(repositoryId, proposalId);
                await reasoningCaptureService.CaptureOperationalContextPromotionAsync(repositoryId, proposal);
                await projectionService.RefreshWorkspaceAsync(repositoryId);
                return Results.Ok(proposal);
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
