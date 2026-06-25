using CommandCenter.Execution;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;

namespace CommandCenter.Backend.Endpoints;

public static class GitEndpoints
{
    public static IEndpointRouteBuilder MapGitEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetGitStatus();
        app.MapGitEligibility();
        app.MapPrepareCommit();
        app.MapCommit();
        app.MapPush();
        return app;
    }

    private static void MapGetGitStatus(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/git/status", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            IGitService gitService) =>
        {
            try
            {
                Repository repository = await repositoryService.GetRepositoryAsync(repositoryId);
                return Results.Ok(await gitService.GetStatusAsync(repository));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapGitEligibility(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/execution-sessions/{sessionId:guid}/git/eligibility", async (
            Guid sessionId,
            ExecutionGitActionEligibilityRequest request,
            IExecutionGitEligibilityService eligibilityService) =>
        {
            try
            {
                return Results.Ok(await eligibilityService.GetEligibilityAsync(sessionId, request));
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

    private static void MapPrepareCommit(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/execution-sessions/{sessionId:guid}/git/prepare-commit", async (
            Guid sessionId,
            IExecutionSessionService executionSessionService) =>
        {
            try
            {
                return Results.Ok(await executionSessionService.PrepareCommitAsync(sessionId));
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

    private static void MapCommit(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/execution-sessions/{sessionId:guid}/git/commit", async (
            Guid sessionId,
            CommitRequest request,
            IExecutionSessionService executionSessionService) =>
        {
            try
            {
                return Results.Ok(await executionSessionService.CommitAsync(sessionId, request));
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

    private static void MapPush(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/execution-sessions/{sessionId:guid}/git/push", async (
            Guid sessionId,
            PushRequest request,
            IExecutionSessionService executionSessionService) =>
        {
            try
            {
                ExecutionSessionSummary summary = await executionSessionService.PushAsync(sessionId, request);
                return Results.Ok(PushAttemptResult.Success(summary));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                ExecutionSession? session = await executionSessionService.GetSessionAsync(sessionId);
                return Results.Conflict(PushAttemptResult.Failure(
                    exception.Message,
                    session?.ToSummary()));
            }
        });
}
