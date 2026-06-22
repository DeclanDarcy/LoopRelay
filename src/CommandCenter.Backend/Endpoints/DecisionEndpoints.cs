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
}
