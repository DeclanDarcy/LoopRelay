using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;

namespace CommandCenter.Backend.Endpoints;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetWorkflow();
        app.MapGetWorkflowDiagnostics();
        app.MapGetWorkflowTimeline();
        app.MapGetWorkflowHistory();
        app.MapGetWorkflowTransitions();
        app.MapGetWorkflowGates();
        app.MapGetWorkflowGateHistory();
        app.MapGetWorkflowRecovery();
        app.MapPostWorkflowRecover();
        app.MapGetWorkflowExecution();
        app.MapGetWorkflowHandoff();
        app.MapGetWorkflowDecisions();
        app.MapGetWorkflowOperationalContext();
        app.MapGetWorkflowGit();
        app.MapGetWorkflowContinuationEvaluation();
        app.MapPostWorkflowContinuationRun();
        app.MapGetWorkflowContinuationHistory();
        app.MapGetWorkflowPreparationEvaluation();
        app.MapPostWorkflowPreparationRun();
        app.MapGetWorkflowPreparationHistory();
        app.MapGetWorkflowHealth();
        return app;
    }

    private static void MapGetWorkflow(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow", async (
            Guid repositoryId,
            IWorkflowProjectionService workflowProjectionService) =>
        {
            try
            {
                return Results.Ok(await workflowProjectionService.ProjectAsync(repositoryId));
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

    private static void MapGetWorkflowHistory(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/history", async (
            Guid repositoryId,
            IWorkflowProjectionService workflowProjectionService,
            IWorkflowRecoveryService workflowRecoveryService) =>
        {
            try
            {
                var projection = await workflowProjectionService.ProjectAsync(repositoryId);
                var recovery = await workflowRecoveryService.RecoverCurrentWorkflowAsync(repositoryId);
                return Results.Ok(new WorkflowHistoryProjection(
                    repositoryId,
                    recovery.Timeline,
                    projection.BlockedTransitions
                        .Where(transition => transition.GateResolution is not null)
                        .Select(transition => $"{transition.GateResolution!.GateType}: {transition.GateResolution.RequiredHumanAction}")
                        .ToArray(),
                    projection.Diagnostics.Reasoning,
                    [recovery.Diagnostics]));
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

    private static void MapGetWorkflowDiagnostics(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/diagnostics", async (
            Guid repositoryId,
            IWorkflowProjectionService workflowProjectionService) =>
        {
            try
            {
                return Results.Ok(await workflowProjectionService.GetDiagnosticsAsync(repositoryId));
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

    private static void MapGetWorkflowTimeline(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/timeline", async (
            Guid repositoryId,
            IWorkflowProjectionService workflowProjectionService) =>
        {
            try
            {
                return Results.Ok(await workflowProjectionService.GetTimelineAsync(repositoryId));
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

    private static void MapGetWorkflowTransitions(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/transitions", async (
            Guid repositoryId,
            IWorkflowProjectionService workflowProjectionService) =>
        {
            try
            {
                var projection = await workflowProjectionService.ProjectAsync(repositoryId);
                return Results.Ok(projection.Diagnostics.StateMachine);
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

    private static void MapGetWorkflowGates(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/gates", async (
            Guid repositoryId,
            IWorkflowGateCatalogService workflowGateCatalogService) =>
        {
            try
            {
                return Results.Ok(await workflowGateCatalogService.GetGatesAsync(repositoryId));
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

    private static void MapGetWorkflowGateHistory(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/gates/history", async (
            Guid repositoryId,
            IWorkflowGateCatalogService workflowGateCatalogService) =>
        {
            try
            {
                return Results.Ok(await workflowGateCatalogService.GetGateHistoryAsync(repositoryId));
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

    private static void MapGetWorkflowRecovery(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/recovery", async (
            Guid repositoryId,
            IWorkflowRecoveryService workflowRecoveryService) =>
        {
            try
            {
                return Results.Ok(await workflowRecoveryService.ValidateRecoveredWorkflowAsync(repositoryId));
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

    private static void MapPostWorkflowRecover(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/workflow/recover", async (
            Guid repositoryId,
            IWorkflowRecoveryService workflowRecoveryService) =>
        {
            try
            {
                return Results.Ok(await workflowRecoveryService.RecoverCurrentWorkflowAsync(repositoryId));
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

    private static void MapGetWorkflowExecution(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/execution", async (
            Guid repositoryId,
            IWorkflowExecutionService workflowExecutionService) =>
        {
            try
            {
                return Results.Ok(await workflowExecutionService.ProjectExecutionAsync(repositoryId));
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

    private static void MapGetWorkflowHandoff(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/handoff", async (
            Guid repositoryId,
            IWorkflowHandoffService workflowHandoffService) =>
        {
            try
            {
                return Results.Ok(await workflowHandoffService.ProjectHandoffAsync(repositoryId));
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

    private static void MapGetWorkflowDecisions(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/decisions", async (
            Guid repositoryId,
            IWorkflowDecisionService workflowDecisionService) =>
        {
            try
            {
                return Results.Ok(await workflowDecisionService.ProjectDecisionAsync(repositoryId));
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

    private static void MapGetWorkflowOperationalContext(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/operational-context", async (
            Guid repositoryId,
            IWorkflowExecutionService workflowExecutionService,
            IWorkflowDecisionService workflowDecisionService,
            IWorkflowOperationalContextService workflowOperationalContextService) =>
        {
            try
            {
                WorkflowExecutionProjection execution = await workflowExecutionService.ProjectExecutionAsync(repositoryId);
                WorkflowDecisionProjection decision = await workflowDecisionService.ProjectDecisionAsync(repositoryId);
                return Results.Ok(await workflowOperationalContextService.ProjectOperationalContextAsync(repositoryId, decision, execution));
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

    private static void MapGetWorkflowGit(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/git", async (
            Guid repositoryId,
            IWorkflowExecutionService workflowExecutionService,
            IWorkflowDecisionService workflowDecisionService,
            IWorkflowOperationalContextService workflowOperationalContextService,
            IWorkflowGitService workflowGitService) =>
        {
            try
            {
                WorkflowExecutionProjection execution = await workflowExecutionService.ProjectExecutionAsync(repositoryId);
                WorkflowDecisionProjection decision = await workflowDecisionService.ProjectDecisionAsync(repositoryId);
                WorkflowOperationalContextProjection operationalContext = await workflowOperationalContextService.ProjectOperationalContextAsync(repositoryId, decision, execution);
                return Results.Ok(await workflowGitService.ProjectGitAsync(repositoryId, execution, operationalContext));
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

    private static void MapGetWorkflowContinuationEvaluation(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/continuation/evaluation", async (
            Guid repositoryId,
            IWorkflowContinuationService workflowContinuationService) =>
        {
            try
            {
                return Results.Ok(await workflowContinuationService.EvaluateContinuationAsync(repositoryId));
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

    private static void MapPostWorkflowContinuationRun(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/workflow/continuation/run", async (
            Guid repositoryId,
            IWorkflowContinuationService workflowContinuationService) =>
        {
            try
            {
                return Results.Ok(await workflowContinuationService.RunContinuationAsync(repositoryId));
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

    private static void MapGetWorkflowContinuationHistory(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/continuation/history", async (
            Guid repositoryId,
            IWorkflowContinuationService workflowContinuationService) =>
        {
            try
            {
                return Results.Ok(await workflowContinuationService.GetContinuationHistoryAsync(repositoryId));
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

    private static void MapGetWorkflowPreparationEvaluation(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/preparation/evaluation", async (
            Guid repositoryId,
            IWorkflowPreparationService workflowPreparationService) =>
        {
            try
            {
                return Results.Ok(await workflowPreparationService.EvaluatePreparationAsync(repositoryId));
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

    private static void MapPostWorkflowPreparationRun(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/workflow/preparation/run", async (
            Guid repositoryId,
            IWorkflowPreparationService workflowPreparationService) =>
        {
            try
            {
                return Results.Ok(await workflowPreparationService.RunPreparationAsync(repositoryId));
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

    private static void MapGetWorkflowPreparationHistory(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/preparation/history", async (
            Guid repositoryId,
            IWorkflowPreparationService workflowPreparationService) =>
        {
            try
            {
                return Results.Ok(await workflowPreparationService.GetPreparationHistoryAsync(repositoryId));
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

    private static void MapGetWorkflowHealth(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/workflow/health", async (
            Guid repositoryId,
            IWorkflowHealthService workflowHealthService) =>
        {
            try
            {
                return Results.Ok(await workflowHealthService.AssessHealthAsync(repositoryId));
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
}
