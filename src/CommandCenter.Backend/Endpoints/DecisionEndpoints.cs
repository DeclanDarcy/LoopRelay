using CommandCenter.Backend.Services;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

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
        app.MapListDecisionProposalBrowser();
        app.MapListDecisionProposals();
        app.MapGetDecisionProposal();
        app.MapGetDecisionProposalReviewWorkspace();
        app.MapGetDecisionProposalOptionComparison();
        app.MapGetDecisionProposalEvidenceInspection();
        app.MapListDecisionProposalSourceAttributions();
        app.MapGenerateDecisionProposal();
        app.MapMarkDecisionProposalViewed();
        app.MapMarkDecisionProposalNeedsRefinement();
        app.MapMarkDecisionProposalReadyForResolution();
        app.MapListDecisionReviewNotes();
        app.MapAddDecisionReviewNote();
        app.MapAnalyzeDecisionProposalRefinement();
        app.MapRefineDecisionProposal();
        app.MapGetDecisionProposalLineage();
        app.MapListDecisionProposalRevisions();
        app.MapGetDecisionProposalRevisionComparison();
        app.MapResolveDecisionProposal();
        app.MapSupersedeDecision();
        app.MapArchiveDecision();
        app.MapGetDecisionAssimilationRecommendation();
        app.MapProposeDecisionOperationalContextAssimilation();
        app.MapGetDecisionGovernance();
        app.MapGenerateDecisionGovernanceReport();
        app.MapListDecisionGovernanceReports();
        app.MapGetExecutionDecisionProjection();
        app.MapGetDecisionCertification();
        app.MapRunDecisionCertification();
        app.MapListDecisionCertificationReports();
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

    private static void MapListDecisionProposalBrowser(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/proposals/browser", async (
            Guid repositoryId,
            string? states,
            IDecisionReviewService reviewService) =>
        {
            try
            {
                return Results.Ok(await reviewService.ListProposalBrowserItemsAsync(
                    repositoryId,
                    ParseProposalStates(states)));
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

    private static void MapGetDecisionProposalOptionComparison(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/options", async (
            Guid repositoryId,
            string proposalId,
            IDecisionReviewService reviewService) =>
        {
            try
            {
                return Results.Ok(await reviewService.GetOptionComparisonAsync(repositoryId, proposalId));
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

    private static void MapGetDecisionProposalEvidenceInspection(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/evidence", async (
            Guid repositoryId,
            string proposalId,
            IDecisionReviewService reviewService) =>
        {
            try
            {
                return Results.Ok(await reviewService.GetEvidenceInspectionAsync(repositoryId, proposalId));
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

    private static void MapListDecisionProposalSourceAttributions(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/sources", async (
            Guid repositoryId,
            string proposalId,
            IDecisionReviewService reviewService) =>
        {
            try
            {
                return Results.Ok(await reviewService.ListSourceAttributionsAsync(repositoryId, proposalId));
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
            IDecisionRefinementService refinementService) =>
        {
            try
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Refinement request is required." });
                }

                return Results.Ok(await refinementService.RefineProposalAsync(repositoryId, proposalId, request));
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

    private static void MapAnalyzeDecisionProposalRefinement(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/refinements/analyze", async (
            Guid repositoryId,
            string proposalId,
            DecisionRefinementAnalysisRequest? request,
            IRefinementAnalysisService refinementAnalysisService) =>
        {
            try
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Refinement analysis request is required." });
                }

                return Results.Ok(await refinementAnalysisService.AnalyzeRefinementAsync(repositoryId, proposalId, request));
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

    private static void MapGetDecisionProposalLineage(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/lineage", async (
            Guid repositoryId,
            string proposalId,
            IDecisionRefinementService refinementService) =>
        {
            try
            {
                return Results.Ok(await refinementService.GetProposalLineageAsync(repositoryId, proposalId));
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
            IDecisionRefinementService refinementService) =>
        {
            try
            {
                return Results.Ok(await refinementService.ListProposalRevisionsAsync(repositoryId, proposalId));
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

    private static void MapGetDecisionProposalRevisionComparison(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/proposals/{proposalId}/revisions/{revisionId}/comparison", async (
            Guid repositoryId,
            string proposalId,
            string revisionId,
            IDecisionRefinementService refinementService) =>
        {
            try
            {
                return Results.Ok(await refinementService.GetProposalRevisionComparisonAsync(repositoryId, proposalId, revisionId));
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
            IDecisionResolutionService resolutionService,
            IDecisionReasoningCaptureService reasoningCaptureService) =>
        {
            try
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Resolution command is required." });
                }

                Decision decision = await resolutionService.ResolveProposalAsync(repositoryId, proposalId, request);
                await reasoningCaptureService.CaptureProposalResolvedAsync(repositoryId, decision, request);
                return Results.Ok(decision);
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

    private static void MapSupersedeDecision(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/{decisionId}/supersede", async (
            Guid repositoryId,
            string decisionId,
            SupersedeDecisionCommand? request,
            IDecisionResolutionService resolutionService,
            IDecisionReasoningCaptureService reasoningCaptureService) =>
        {
            try
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Supersede command is required." });
                }

                Decision superseded = await resolutionService.SupersedeDecisionAsync(repositoryId, decisionId, request);
                await reasoningCaptureService.CaptureDecisionSupersededAsync(repositoryId, superseded, request);
                return Results.Ok(superseded);
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

    private static void MapArchiveDecision(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/{decisionId}/archive", async (
            Guid repositoryId,
            string decisionId,
            ArchiveDecisionCommand? request,
            IDecisionResolutionService resolutionService,
            IDecisionReasoningCaptureService reasoningCaptureService) =>
        {
            try
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Archive command is required." });
                }

                Decision archived = await resolutionService.ArchiveDecisionAsync(repositoryId, decisionId, request);
                await reasoningCaptureService.CaptureDecisionArchivedAsync(repositoryId, archived, request);
                return Results.Ok(archived);
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

    private static void MapGetDecisionAssimilationRecommendation(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/{decisionId}/assimilation", async (
            Guid repositoryId,
            string decisionId,
            IDecisionOperationalContextAssimilationService assimilationService) =>
        {
            try
            {
                DecisionAssimilationRecommendation? recommendation =
                    await assimilationService.GetRecommendationAsync(repositoryId, decisionId);
                return recommendation is null
                    ? Results.NotFound(new { error = $"Decision assimilation recommendation was not found: {decisionId}" })
                    : Results.Ok(recommendation);
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

    private static void MapProposeDecisionOperationalContextAssimilation(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/{decisionId}/assimilation/propose-operational-context", async (
            Guid repositoryId,
            string decisionId,
            CreateDecisionAssimilationRecommendationCommand? request,
            IDecisionOperationalContextAssimilationService assimilationService) =>
        {
            try
            {
                return Results.Ok(await assimilationService.ProposeOperationalContextAssimilationAsync(
                    repositoryId,
                    decisionId,
                    request));
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

    private static void MapGetDecisionGovernance(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/governance", async (
            Guid repositoryId,
            IDecisionGovernanceService governanceService) =>
        {
            try
            {
                return Results.Ok(await governanceService.GetCurrentReportAsync(repositoryId));
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

    private static void MapGenerateDecisionGovernanceReport(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/governance/reports", async (
            Guid repositoryId,
            IDecisionGovernanceService governanceService,
            IDecisionReasoningCaptureService reasoningCaptureService) =>
        {
            try
            {
                DecisionGovernanceReport report = await governanceService.GenerateReportAsync(repositoryId);
                await reasoningCaptureService.CaptureGovernanceContradictionsAsync(repositoryId, report);
                return Results.Ok(report);
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

    private static void MapListDecisionGovernanceReports(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/governance/reports", async (
            Guid repositoryId,
            IDecisionGovernanceService governanceService) =>
        {
            try
            {
                return Results.Ok(await governanceService.ListReportsAsync(repositoryId));
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

    private static void MapGetExecutionDecisionProjection(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/execution-projection", async (
            Guid repositoryId,
            string? executionRequest,
            string? milestoneContent,
            IDecisionProjectionService projectionService) =>
        {
            try
            {
                return Results.Ok(await projectionService.BuildExecutionProjectionAsync(
                    repositoryId,
                    executionRequest,
                    milestoneContent));
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

    private static void MapGetDecisionCertification(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/certification", async (
            Guid repositoryId,
            IDecisionCertificationService certificationService) =>
        {
            try
            {
                return Results.Ok(await certificationService.GetCurrentCertificationAsync(repositoryId));
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

    private static void MapRunDecisionCertification(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/decisions/certification", async (
            Guid repositoryId,
            IDecisionCertificationService certificationService) =>
        {
            try
            {
                return Results.Ok(await certificationService.RunCertificationAsync(repositoryId));
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

    private static void MapListDecisionCertificationReports(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/decisions/certification/reports", async (
            Guid repositoryId,
            IDecisionCertificationService certificationService) =>
        {
            try
            {
                return Results.Ok(await certificationService.ListReportsAsync(repositoryId));
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

    private static IReadOnlySet<DecisionProposalState>? ParseProposalStates(string? states)
    {
        if (string.IsNullOrWhiteSpace(states))
        {
            return null;
        }

        var parsed = new HashSet<DecisionProposalState>();
        foreach (string state in states.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse(state, ignoreCase: true, out DecisionProposalState parsedState))
            {
                throw new ArgumentException($"Unknown proposal state filter: {state}");
            }

            parsed.Add(parsedState);
        }

        return parsed;
    }
}
