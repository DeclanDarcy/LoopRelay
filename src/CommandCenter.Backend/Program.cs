using CommandCenter.Backend.Endpoints;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Configuration;
using CommandCenter.Continuity;
using CommandCenter.Core.Planning;
using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution;
using CommandCenter.Middle.Continuity;
using CommandCenter.Middle.Projections;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Continuity.Abstractions;
using CommandCenter.Execution.Extensions;
using CommandCenter.Continuity.Extensions;
using CommandCenter.Decisions.Extensions;
using CommandCenter.Reasoning.Extensions;
using CommandCenter.Workflow.Extensions;
using CommandCenter.Backend.Services;

namespace CommandCenter.Backend;

public static class Program
{
    public static WebApplication CreateApp(
        string[] args,
        Action<IServiceCollection>? configureServices = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<IApplicationConfigurationStore, ApplicationConfigurationStore>();
        builder.Services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();
        builder.Services.AddSingleton<IRepositoryService, RepositoryService>();
        builder.Services.AddSingleton<IArtifactService, ArtifactService>();
        builder.Services.AddSingleton<IArtifactRotationService, ArtifactRotationService>();
        builder.Services.AddContinuity();
        builder.Services.AddDecisions();
        builder.Services.AddReasoning();
        builder.Services.AddSingleton<IDecisionReasoningCaptureService, DecisionReasoningCaptureService>();
        // Generation lives in Middle (it depends on Execution), so it is wired here
        // rather than inside AddContinuity().
        builder.Services.AddSingleton<IOperationalContextGenerationService, OperationalContextGenerationService>();
        builder.Services.AddSingleton<IPlanningService, PlanningService>();
        builder.Services.AddExecution();
        builder.Services.AddWorkflow();
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

        WebApplication app = builder.Build();
        app.UseCors();

        app.MapPingEndpoints();
        app.MapRepositoriesEndpoints();
        app.MapArtifactsEndpoints();
        app.MapPlanningEndpoints();
        app.MapOperationalContextEndpoints();
        app.MapContinuityEndpoints();
        app.MapExecutionEndpoints();
        app.MapGitEndpoints();
        app.MapExecutionSessionsEndpoints();
        app.MapDecisionEndpoints();
        app.MapReasoningEndpoints();
        app.MapWorkflowEndpoints();

        return app;
    }

    public static void Main(string[] args)
    {
        CreateApp(args).Run();
    }
}
