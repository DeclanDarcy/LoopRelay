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
        app.MapListDecisionCandidates();
        app.MapDiscoverDecisions();
        app.MapPromoteDecisionCandidate();
        app.MapDismissDecisionCandidate();
        app.MapExpireDecisionCandidate();
        app.MapMarkDecisionCandidateDuplicate();
        app.MapListDecisionProposals();
        app.MapGetDecisionProposal();
        app.MapGetDecisionProposalReviewWorkspace();
        app.MapGenerateDecisionProposal();
        app.MapMarkDecisionProposalViewed();
        app.MapMarkDecisionProposalNeedsRefinement();
        app.MapMarkDecisionProposalReadyForResolution();
        app.MapListDecisionReviewNotes();
        app.MapAddDecisionReviewNote();
        app.MapRefineDecisionProposal();
        app.MapListDecisionProposalRevisions();
        app.MapResolveDecisionProposal();
        app.MapExpireDecisionProposal();
        app.MapDiscardDecisionProposal();
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

    private static void MapListDecisionCandidates(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/candidates", async (
            Guid repositoryId,
            IDecisionDiscoveryService discoveryService) =>
        {
            try
            {
                return Results.Ok(await discoveryService.ListCandidatesAsync(repositoryId));
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

    private static void MapDiscoverDecisions(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/discover", async (
            Guid repositoryId,
            IDecisionDiscoveryService discoveryService) =>
        {
            try
            {
                return Results.Ok(await discoveryService.DiscoverAsync(repositoryId));
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

    private static void MapPromoteDecisionCandidate(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/candidates/{candidateId}/promote", async (
            Guid repositoryId,
            string candidateId,
            DecisionCandidateTransitionRequest? request,
            IDecisionDiscoveryService discoveryService) =>
        {
            try
            {
                return Results.Ok(await discoveryService.PromoteCandidateAsync(repositoryId, candidateId, request?.Reason));
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

    private static void MapDismissDecisionCandidate(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/candidates/{candidateId}/dismiss", async (
            Guid repositoryId,
            string candidateId,
            DecisionCandidateTransitionRequest? request,
            IDecisionDiscoveryService discoveryService) =>
        {
            try
            {
                return Results.Ok(await discoveryService.DismissCandidateAsync(repositoryId, candidateId, request?.Reason));
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

    private static void MapExpireDecisionCandidate(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/candidates/{candidateId}/expire", async (
            Guid repositoryId,
            string candidateId,
            DecisionCandidateTransitionRequest? request,
            IDecisionDiscoveryService discoveryService) =>
        {
            try
            {
                return Results.Ok(await discoveryService.ExpireCandidateAsync(repositoryId, candidateId, request?.Reason));
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

    private static void MapMarkDecisionCandidateDuplicate(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/candidates/{candidateId}/duplicate", async (
            Guid repositoryId,
            string candidateId,
            DecisionCandidateTransitionRequest? request,
            IDecisionDiscoveryService discoveryService) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.DuplicateOfCandidateId))
                {
                    return Results.BadRequest(new { error = "Duplicate candidate id is required." });
                }

                return Results.Ok(await discoveryService.MarkCandidateDuplicateAsync(
                    repositoryId,
                    candidateId,
                    request.DuplicateOfCandidateId,
                    request.Reason));
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

    private static void MapListDecisionProposals(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/proposals", async (
            Guid repositoryId,
            IDecisionGenerationService generationService) =>
        {
            try
            {
                return Results.Ok(await generationService.ListProposalsAsync(repositoryId));
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

    private static void MapGetDecisionProposal(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}", async (
            Guid repositoryId,
            string proposalId,
            IDecisionGenerationService generationService) =>
        {
            try
            {
                return Results.Ok(await generationService.GetProposalAsync(repositoryId, proposalId));
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

    private static void MapGetDecisionProposalReviewWorkspace(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/review", async (
            Guid repositoryId,
            string proposalId,
            IDecisionReviewService reviewService) =>
        {
            try
            {
                return Results.Ok(await reviewService.GetReviewWorkspaceAsync(repositoryId, proposalId));
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

    private static void MapGenerateDecisionProposal(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/candidates/{candidateId}/proposals", async (
            Guid repositoryId,
            string candidateId,
            IDecisionGenerationService generationService) =>
        {
            try
            {
                return Results.Ok(await generationService.GenerateProposalAsync(repositoryId, candidateId));
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

    private static void MapExpireDecisionProposal(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/expire", async (
            Guid repositoryId,
            string proposalId,
            DecisionProposalTransitionRequest? request,
            IDecisionGenerationService generationService) =>
        {
            try
            {
                return Results.Ok(await generationService.ExpireProposalAsync(repositoryId, proposalId, request?.Reason));
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

    private static void MapDiscardDecisionProposal(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/discard", async (
            Guid repositoryId,
            string proposalId,
            DecisionProposalTransitionRequest? request,
            IDecisionGenerationService generationService) =>
        {
            try
            {
                return Results.Ok(await generationService.DiscardProposalAsync(repositoryId, proposalId, request?.Reason));
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

    private static void MapMarkDecisionProposalViewed(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/review/viewed", async (
            Guid repositoryId,
            string proposalId,
            DecisionProposalTransitionRequest? request,
            IDecisionReviewService reviewService) =>
        {
            try
            {
                return Results.Ok(await reviewService.MarkProposalViewedAsync(repositoryId, proposalId, request?.Reason));
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

    private static void MapMarkDecisionProposalNeedsRefinement(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/review/needs-refinement", async (
            Guid repositoryId,
            string proposalId,
            DecisionProposalTransitionRequest? request,
            IDecisionReviewService reviewService) =>
        {
            try
            {
                return Results.Ok(await reviewService.MarkProposalNeedsRefinementAsync(repositoryId, proposalId, request?.Reason));
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

    private static void MapMarkDecisionProposalReadyForResolution(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/review/ready-for-resolution", async (
            Guid repositoryId,
            string proposalId,
            DecisionProposalTransitionRequest? request,
            IDecisionReviewService reviewService) =>
        {
            try
            {
                return Results.Ok(await reviewService.MarkProposalReadyForResolutionAsync(repositoryId, proposalId, request?.Reason));
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

    private static void MapListDecisionReviewNotes(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/notes", async (
            Guid repositoryId,
            string proposalId,
            IDecisionReviewService reviewService) =>
        {
            try
            {
                return Results.Ok(await reviewService.ListReviewNotesAsync(repositoryId, proposalId));
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

    private static void MapAddDecisionReviewNote(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/notes", async (
            Guid repositoryId,
            string proposalId,
            DecisionReviewNoteRequest? request,
            IDecisionReviewService reviewService) =>
        {
            try
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Review note request is required." });
                }

                return Results.Ok(await reviewService.AddReviewNoteAsync(repositoryId, proposalId, request));
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

    private static void MapRefineDecisionProposal(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/refinements", async (
            Guid repositoryId,
            string proposalId,
            DecisionRefinementRequest? request,
            IDecisionGenerationService generationService) =>
        {
            try
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Refinement request is required." });
                }

                return Results.Ok(await generationService.RefineProposalAsync(repositoryId, proposalId, request));
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

    private static void MapListDecisionProposalRevisions(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/revisions", async (
            Guid repositoryId,
            string proposalId,
            IDecisionGenerationService generationService) =>
        {
            try
            {
                return Results.Ok(await generationService.ListProposalRevisionsAsync(repositoryId, proposalId));
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

    private static void MapResolveDecisionProposal(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/resolve", async (
            Guid repositoryId,
            string proposalId,
            ResolveDecisionCommand? request,
            IDecisionGenerationService generationService) =>
        {
            try
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Resolution command is required." });
                }

                return Results.Ok(await generationService.ResolveProposalAsync(repositoryId, proposalId, request));
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
