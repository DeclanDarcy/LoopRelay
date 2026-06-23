using CommandCenter.Execution;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend.Services;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;

namespace CommandCenter.Backend.Endpoints;

public static class ExecutionSessionsEndpoints
{
    public static IEndpointRouteBuilder MapExecutionSessionsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetSession();
        app.MapGetSessionStatus();
        app.MapGetSessionEvents();
        app.MapStreamSessionEvents();
        app.MapAcceptSession();
        app.MapRejectSession();
        return app;
    }

    private static void MapGetSession(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/execution-sessions/{sessionId:guid}", async (
            Guid sessionId,
            IExecutionSessionService executionSessionService) =>
        {
            ExecutionSession? session = await executionSessionService.GetSessionAsync(sessionId);
            return session is null ? Results.NotFound(new { error = "Execution session was not found." }) : Results.Ok(session);
        });

    private static void MapGetSessionStatus(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/execution-sessions/{sessionId:guid}/status", async (
            Guid sessionId,
            IExecutionMonitoringService monitoringService) =>
        {
            ExecutionStatus? status = await monitoringService.GetStatusAsync(sessionId);
            return status is null ? Results.NotFound(new { error = "Execution session was not found." }) : Results.Ok(status);
        });

    private static void MapGetSessionEvents(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/execution-sessions/{sessionId:guid}/events", async (
            Guid sessionId,
            IExecutionMonitoringService monitoringService) =>
        {
            ExecutionStatus? status = await monitoringService.GetStatusAsync(sessionId);
            return status is null ? Results.NotFound(new { error = "Execution session was not found." }) : Results.Ok(await monitoringService.GetEventsAsync(sessionId));
        });

    private static void MapStreamSessionEvents(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/execution-sessions/{sessionId:guid}/events/stream", async Task<IResult> (
            Guid sessionId,
            HttpContext httpContext,
            IExecutionMonitoringService monitoringService) =>
        {
            ExecutionStatus? status = await monitoringService.GetStatusAsync(sessionId);
            if (status is null)
            {
                return Results.NotFound(new { error = "Execution session was not found." });
            }

            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.ContentType = "text/event-stream";

            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            jsonOptions.Converters.Add(new JsonStringEnumConverter());

            try
            {
                await foreach (ExecutionEvent executionEvent in monitoringService.StreamEventsAsync(sessionId, httpContext.RequestAborted))
                {
                    await httpContext.Response.WriteAsync($"id: {executionEvent.Sequence}\n", httpContext.RequestAborted);
                    await httpContext.Response.WriteAsync("event: execution-event\n", httpContext.RequestAborted);
                    await httpContext.Response.WriteAsync($"data: {JsonSerializer.Serialize(executionEvent, jsonOptions)}\n\n", httpContext.RequestAborted);
                    await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
                }
            }
            catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
            {
            }

            return Results.Empty;
        });

    private static void MapAcceptSession(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/execution-sessions/{sessionId:guid}/accept", async (
            Guid sessionId,
            ExecutionAcceptanceRequest request,
            IExecutionSessionService executionSessionService,
            IDecisionReasoningCaptureService reasoningCaptureService) =>
        {
            try
            {
                ExecutionSessionSummary summary = await executionSessionService.AcceptAsync(sessionId, request);
                ExecutionSession? session = await executionSessionService.GetSessionAsync(sessionId);
                if (session is not null)
                {
                    await reasoningCaptureService.CaptureExecutionHandoffDecisionAsync(session, accepted: true);
                }

                return Results.Ok(summary);
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });

    private static void MapRejectSession(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/execution-sessions/{sessionId:guid}/reject", async (
            Guid sessionId,
            ExecutionAcceptanceRequest request,
            IExecutionSessionService executionSessionService,
            IDecisionReasoningCaptureService reasoningCaptureService) =>
        {
            try
            {
                ExecutionSessionSummary summary = await executionSessionService.RejectAsync(sessionId, request);
                ExecutionSession? session = await executionSessionService.GetSessionAsync(sessionId);
                if (session is not null)
                {
                    await reasoningCaptureService.CaptureExecutionHandoffDecisionAsync(session, accepted: false);
                }

                return Results.Ok(summary);
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });
}
