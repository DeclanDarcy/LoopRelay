using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Continuity;
using CommandCenter.Backend.Execution;
using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Projections;
using CommandCenter.Backend.Repositories;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandCenter.Backend;

public static class Program
{
    public static WebApplication CreateApp(
        string[] args,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<IApplicationConfigurationStore, ApplicationConfigurationStore>();
        builder.Services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();
        builder.Services.AddSingleton<IRepositoryService, RepositoryService>();
        builder.Services.AddSingleton<IArtifactService, ArtifactService>();
        builder.Services.AddSingleton<IArtifactRotationService, ArtifactRotationService>();
        builder.Services.AddSingleton<IOperationalContextParser, MarkdownOperationalContextParser>();
        builder.Services.AddSingleton<IUnderstandingDiffService, UnderstandingDiffService>();
        builder.Services.AddSingleton<IUnderstandingCompressionService, UnderstandingCompressionService>();
        builder.Services.AddSingleton<IDecisionAnalysisService, DecisionAnalysisService>();
        builder.Services.AddSingleton<IOperationalContextProposalStore, FileSystemOperationalContextProposalStore>();
        builder.Services.AddSingleton<IOperationalContextGenerationService, OperationalContextGenerationService>();
        builder.Services.AddSingleton<IOperationalContextReviewService, OperationalContextReviewService>();
        builder.Services.AddSingleton<IOperationalContextLifecycleService, OperationalContextLifecycleService>();
        builder.Services.AddSingleton<IContinuityDiagnosticsService, ContinuityDiagnosticsService>();
        builder.Services.AddSingleton<IContinuityReportService, ContinuityReportService>();
        builder.Services.AddSingleton<IPlanningService, PlanningService>();
        builder.Services.AddSingleton<IExecutionContextService, ExecutionContextService>();
        builder.Services.AddSingleton<IExecutionPromptBuilder, ExecutionPromptBuilder>();
        builder.Services.AddSingleton<IExecutionSessionStore, FileSystemExecutionSessionStore>();
        builder.Services.AddSingleton<IExecutionSessionService, ExecutionSessionService>();
        builder.Services.AddHostedService<ExecutionSessionRecoveryHostedService>();
        builder.Services.AddSingleton<ExecutionEventRetentionPolicy>();
        builder.Services.AddSingleton<IExecutionMonitoringService, ExecutionMonitoringService>();
        builder.Services.AddSingleton<IHandoffService, HandoffService>();
        builder.Services.AddSingleton<ICodexExecutableResolver, CodexExecutableResolver>();
        builder.Services.AddSingleton<IExecutionProvider, CodexExecutionProvider>();
        builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
        builder.Services.AddSingleton<IGitService, GitService>();
        builder.Services.AddSingleton<IRepositoryProjectionService, RepositoryProjectionService>();
        builder.Services.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy
                    .WithOrigins(
                        "http://localhost:1420",
                        "http://127.0.0.1:1420",
                        "http://localhost:5173",
                        "http://127.0.0.1:5173",
                        "tauri://localhost")
                    .AllowAnyHeader()
                    .AllowAnyMethod()));
        configureServices?.Invoke(builder.Services);
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        var app = builder.Build();
        app.UseCors();

        app.MapGet("/api/ping", () => "Pong");
        app.MapGet("/api/repositories", async (IRepositoryProjectionService projectionService) =>
            await projectionService.GetDashboardAsync());
        app.MapPost("/api/repositories", async (RegisterRepositoryRequest request, IRepositoryService repositoryService) =>
        {
            try
            {
                var repository = await repositoryService.RegisterAsync(request.Path);
                return Results.Created($"/api/repositories/{repository.Id}", repository);
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (DirectoryNotFoundException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (UnauthorizedAccessException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (IOException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });
        app.MapDelete("/api/repositories/{repositoryId:guid}", async (Guid repositoryId, IRepositoryService repositoryService) =>
        {
            await repositoryService.RemoveAsync(repositoryId);
            return Results.NoContent();
        });
        app.MapGet("/api/repositories/{repositoryId:guid}/workspace", async (
            Guid repositoryId,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                return Results.Ok(await projectionService.GetWorkspaceAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });
        app.MapGet("/api/repositories/{repositoryId:guid}/artifacts", async (
            Guid repositoryId,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                var workspace = await projectionService.GetWorkspaceAsync(repositoryId);
                return Results.Ok(workspace.ArtifactInventory);
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });
        app.MapGet("/api/repositories/{repositoryId:guid}/artifacts/content", async (
            Guid repositoryId,
            string relativePath,
            IRepositoryService repositoryService,
            IArtifactService artifactService) =>
        {
            try
            {
                var repository = await GetRepositoryAsync(repositoryService, repositoryId);
                return Results.Text(await artifactService.LoadAsync(repository, relativePath), "text/markdown");
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (FileNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });
        app.MapPut("/api/repositories/{repositoryId:guid}/artifacts/content", async (
            Guid repositoryId,
            SaveArtifactContentRequest request,
            IRepositoryService repositoryService,
            IArtifactService artifactService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                var repository = await GetRepositoryAsync(repositoryService, repositoryId);
                await artifactService.SaveAsync(repository, request.RelativePath, request.Content);
                await projectionService.RefreshWorkspaceAsync(repositoryId);
                return Results.NoContent();
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
        app.MapPost("/api/repositories/{repositoryId:guid}/artifacts/rotate-current-handoff", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            IArtifactRotationService rotationService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                var repository = await GetRepositoryAsync(repositoryService, repositoryId);
                await rotationService.RotateCurrentHandoffAsync(repository);
                return Results.Ok(await projectionService.RefreshWorkspaceAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (FileNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (IOException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });
        app.MapPost("/api/repositories/{repositoryId:guid}/artifacts/rotate-current-decisions", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            IArtifactRotationService rotationService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                var repository = await GetRepositoryAsync(repositoryService, repositoryId);
                await rotationService.RotateCurrentDecisionsAsync(repository);
                return Results.Ok(await projectionService.RefreshWorkspaceAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (FileNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (IOException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });
        app.MapGet("/api/repositories/{repositoryId:guid}/planning", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            IPlanningService planningService) =>
        {
            try
            {
                var repository = await GetRepositoryAsync(repositoryService, repositoryId);
                var milestones = await planningService.GetMilestonesAsync(repository);
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
        app.MapGet("/api/repositories/{repositoryId:guid}/execution/context", async (
            Guid repositoryId,
            string milestonePath,
            IExecutionContextService executionContextService) =>
        {
            try
            {
                return Results.Ok(await executionContextService.BuildContextAsync(repositoryId, milestonePath));
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
        app.MapPost("/api/repositories/{repositoryId:guid}/operational-context/generate", async (
            Guid repositoryId,
            IOperationalContextGenerationService generationService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                var proposal = await generationService.GenerateAsync(repositoryId);
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
        app.MapGet("/api/repositories/{repositoryId:guid}/operational-context/proposals", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            IOperationalContextProposalStore proposalStore) =>
        {
            try
            {
                var repository = await GetRepositoryAsync(repositoryService, repositoryId);
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
        app.MapGet("/api/repositories/{repositoryId:guid}/operational-context/proposals/{proposalId}", async (
            Guid repositoryId,
            string proposalId,
            IRepositoryService repositoryService,
            IOperationalContextProposalStore proposalStore) =>
        {
            try
            {
                var repository = await GetRepositoryAsync(repositoryService, repositoryId);
                var proposal = await proposalStore.GetAsync(repository, proposalId, includeContent: true);
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
        app.MapPut("/api/repositories/{repositoryId:guid}/operational-context/proposals/{proposalId}/content", async (
            Guid repositoryId,
            string proposalId,
            OperationalContextProposalContentRequest request,
            IOperationalContextReviewService reviewService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                var proposal = await reviewService.EditAsync(repositoryId, proposalId, request.Content);
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
        app.MapPost("/api/repositories/{repositoryId:guid}/operational-context/proposals/{proposalId}/accept", async (
            Guid repositoryId,
            string proposalId,
            OperationalContextProposalReviewRequest request,
            IOperationalContextReviewService reviewService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                var proposal = await reviewService.AcceptAsync(repositoryId, proposalId, request.ReviewNote);
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
        app.MapPost("/api/repositories/{repositoryId:guid}/operational-context/proposals/{proposalId}/reject", async (
            Guid repositoryId,
            string proposalId,
            OperationalContextProposalReviewRequest request,
            IOperationalContextReviewService reviewService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                var proposal = await reviewService.RejectAsync(repositoryId, proposalId, request.ReviewNote);
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
        app.MapPost("/api/repositories/{repositoryId:guid}/operational-context/proposals/{proposalId}/promote", async (
            Guid repositoryId,
            string proposalId,
            IOperationalContextLifecycleService lifecycleService,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                var proposal = await lifecycleService.PromoteAsync(repositoryId, proposalId);
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
        app.MapGet("/api/repositories/{repositoryId:guid}/continuity/diagnostics", async (
            Guid repositoryId,
            IContinuityDiagnosticsService diagnosticsService) =>
        {
            try
            {
                return Results.Ok(await diagnosticsService.GetDiagnosticsAsync(repositoryId));
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
        app.MapPost("/api/repositories/{repositoryId:guid}/continuity/reports", async (
            Guid repositoryId,
            IContinuityReportService reportService) =>
        {
            try
            {
                return Results.Ok(await reportService.GenerateReportAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (IOException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
            catch (UnauthorizedAccessException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });
        app.MapGet("/api/repositories/{repositoryId:guid}/continuity/reports", async (
            Guid repositoryId,
            IContinuityReportService reportService) =>
        {
            try
            {
                return Results.Ok(await reportService.ListReportsAsync(repositoryId));
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
        app.MapPost("/api/repositories/{repositoryId:guid}/execution/start", async (
            Guid repositoryId,
            ExecutionStartRequest request,
            IExecutionSessionService executionSessionService) =>
        {
            try
            {
                return Results.Ok(await executionSessionService.StartAsync(repositoryId, request));
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
        app.MapGet("/api/repositories/{repositoryId:guid}/execution/active", async (
            Guid repositoryId,
            IExecutionSessionService executionSessionService) =>
        {
            var session = await executionSessionService.GetActiveSessionAsync(repositoryId);
            return session is null ? Results.NotFound(new { error = "No active execution session." }) : Results.Ok(session);
        });
        app.MapGet("/api/repositories/{repositoryId:guid}/git/status", async (
            Guid repositoryId,
            IRepositoryService repositoryService,
            IGitService gitService) =>
        {
            try
            {
                var repository = await GetRepositoryAsync(repositoryService, repositoryId);
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
        app.MapPost("/api/execution-sessions/{sessionId:guid}/git/push", async (
            Guid sessionId,
            PushRequest request,
            IExecutionSessionService executionSessionService) =>
        {
            try
            {
                return Results.Ok(await executionSessionService.PushAsync(sessionId, request));
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
        app.MapGet("/api/execution-sessions/{sessionId:guid}", async (
            Guid sessionId,
            IExecutionSessionService executionSessionService) =>
        {
            var session = await executionSessionService.GetSessionAsync(sessionId);
            return session is null ? Results.NotFound(new { error = "Execution session was not found." }) : Results.Ok(session);
        });
        app.MapGet("/api/execution-sessions/{sessionId:guid}/status", async (
            Guid sessionId,
            IExecutionMonitoringService monitoringService) =>
        {
            var status = await monitoringService.GetStatusAsync(sessionId);
            return status is null ? Results.NotFound(new { error = "Execution session was not found." }) : Results.Ok(status);
        });
        app.MapGet("/api/execution-sessions/{sessionId:guid}/events", async (
            Guid sessionId,
            IExecutionMonitoringService monitoringService) =>
        {
            var status = await monitoringService.GetStatusAsync(sessionId);
            return status is null ? Results.NotFound(new { error = "Execution session was not found." }) : Results.Ok(await monitoringService.GetEventsAsync(sessionId));
        });
        app.MapGet("/api/execution-sessions/{sessionId:guid}/events/stream", async Task<IResult> (
            Guid sessionId,
            HttpContext httpContext,
            IExecutionMonitoringService monitoringService) =>
        {
            var status = await monitoringService.GetStatusAsync(sessionId);
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
                await foreach (var executionEvent in monitoringService.StreamEventsAsync(sessionId, httpContext.RequestAborted))
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
        app.MapPost("/api/execution-sessions/{sessionId:guid}/accept", async (
            Guid sessionId,
            ExecutionAcceptanceRequest request,
            IExecutionSessionService executionSessionService) =>
        {
            try
            {
                return Results.Ok(await executionSessionService.AcceptAsync(sessionId, request));
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
        app.MapPost("/api/execution-sessions/{sessionId:guid}/reject", async (
            Guid sessionId,
            ExecutionAcceptanceRequest request,
            IExecutionSessionService executionSessionService) =>
        {
            try
            {
                return Results.Ok(await executionSessionService.RejectAsync(sessionId, request));
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
        app.MapPost("/api/repositories/{repositoryId:guid}/refresh", async (
            Guid repositoryId,
            IRepositoryProjectionService projectionService) =>
        {
            try
            {
                return Results.Ok(await projectionService.RefreshWorkspaceAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });

        return app;
    }

    private static async Task<Repository> GetRepositoryAsync(IRepositoryService repositoryService, Guid repositoryId)
    {
        var repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    public static void Main(string[] args)
    {
        CreateApp(args).Run();
    }
}
