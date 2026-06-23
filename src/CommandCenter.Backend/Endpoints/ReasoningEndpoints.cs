using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.Backend.Endpoints;

public static class ReasoningEndpoints
{
    public static IEndpointRouteBuilder MapReasoningEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/repositories/{repositoryId:guid}/reasoning");

        group.MapGet("/events", async (
            Guid repositoryId,
            IReasoningEventService eventService) =>
            await HandleAsync(() => eventService.ListEventsAsync(repositoryId)));

        group.MapGet("/events/{eventId}", async (
            Guid repositoryId,
            string eventId,
            IReasoningEventService eventService) =>
            await HandleAsync(() => eventService.GetEventAsync(repositoryId, eventId)));

        group.MapPost("/events", async (
            Guid repositoryId,
            CreateReasoningEventCommand command,
            IReasoningEventService eventService) =>
            await HandleAsync(() => eventService.CreateEventAsync(repositoryId, command)));

        group.MapGet("/manual-captures/templates", (
            IReasoningManualCaptureService manualCaptureService) =>
            Results.Ok(manualCaptureService.ListTemplates()));

        group.MapPost("/manual-captures", async (
            Guid repositoryId,
            ManualReasoningCaptureCommand command,
            IReasoningManualCaptureService manualCaptureService) =>
            await HandleAsync(() => manualCaptureService.CaptureAsync(repositoryId, command)));

        group.MapGet("/threads", async (
            Guid repositoryId,
            IReasoningThreadService threadService) =>
            await HandleAsync(() => threadService.ListThreadsAsync(repositoryId)));

        group.MapGet("/threads/{threadId}", async (
            Guid repositoryId,
            string threadId,
            IReasoningThreadService threadService) =>
            await HandleAsync(() => threadService.GetThreadAsync(repositoryId, threadId)));

        group.MapPost("/threads", async (
            Guid repositoryId,
            CreateReasoningThreadCommand command,
            IReasoningThreadService threadService) =>
            await HandleAsync(() => threadService.CreateThreadAsync(repositoryId, command)));

        group.MapPost("/threads/{threadId}/events", async (
            Guid repositoryId,
            string threadId,
            AppendReasoningThreadEventRequest request,
            IReasoningThreadService threadService) =>
            await HandleAsync(() => threadService.AppendThreadEventAsync(repositoryId, threadId, request.EventId)));

        group.MapGet("/relationships", async (
            Guid repositoryId,
            IReasoningRelationshipService relationshipService) =>
            await HandleAsync(() => relationshipService.ListRelationshipsAsync(repositoryId)));

        group.MapPost("/relationships", async (
            Guid repositoryId,
            CreateReasoningRelationshipCommand command,
            IReasoningRelationshipService relationshipService) =>
            await HandleAsync(() => relationshipService.CreateRelationshipAsync(repositoryId, command)));

        group.MapGet("/graph", async (
            Guid repositoryId,
            IReasoningGraphService graphService) =>
            await HandleAsync(() => graphService.GetGraphAsync(repositoryId)));

        group.MapGet("/trace/backward", async (
            Guid repositoryId,
            string kind,
            string id,
            IReasoningGraphService graphService) =>
            await HandleAsync(() => graphService.TraceBackwardAsync(repositoryId, CreateTraceTarget(kind, id))));

        group.MapGet("/trace/forward", async (
            Guid repositoryId,
            string kind,
            string id,
            IReasoningGraphService graphService) =>
            await HandleAsync(() => graphService.TraceForwardAsync(repositoryId, CreateTraceTarget(kind, id))));

        group.MapPost("/queries", async (
            Guid repositoryId,
            ReasoningQuery query,
            IReasoningQueryService queryService) =>
            await HandleAsync(() => queryService.RunQueryAsync(repositoryId, query)));

        group.MapPost("/reconstructions", async (
            Guid repositoryId,
            ReasoningQuery query,
            IReasoningReconstructionService reconstructionService) =>
            await HandleAsync(() => reconstructionService.ReconstructAsync(repositoryId, query)));

        group.MapGet("/materialization-review", async (
            Guid repositoryId,
            IReasoningMaterializationReviewService materializationReviewService) =>
            await HandleAsync(() => materializationReviewService.RunReviewAsync(repositoryId)));

        group.MapPost("/materialization-review", async (
            Guid repositoryId,
            ReasoningMaterializationReviewRequest request,
            IReasoningMaterializationReviewService materializationReviewService) =>
            await HandleAsync(() => materializationReviewService.RunReviewAsync(repositoryId, request)));

        return app;
    }

    private static async Task<IResult> HandleAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (ReasoningConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
        catch (ReasoningValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static ReasoningReference CreateTraceTarget(string kind, string id)
    {
        if (!Enum.TryParse(kind, ignoreCase: true, out ReasoningReferenceKind referenceKind))
        {
            throw new ArgumentException($"Unsupported reasoning reference kind: {kind}");
        }

        return new ReasoningReference(referenceKind, id);
    }
}

public sealed record AppendReasoningThreadEventRequest(string EventId);
