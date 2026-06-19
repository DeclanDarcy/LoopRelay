using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Projections;
using CommandCenter.Backend.Repositories;
using System.Text.Json.Serialization;

namespace CommandCenter.Backend;

public static class Program
{
    public static WebApplication CreateApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<IApplicationConfigurationStore, ApplicationConfigurationStore>();
        builder.Services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();
        builder.Services.AddSingleton<IRepositoryService, RepositoryService>();
        builder.Services.AddSingleton<IArtifactService, ArtifactService>();
        builder.Services.AddSingleton<IRepositoryProjectionService, RepositoryProjectionService>();
        builder.Services.AddSingleton<IPlanningService, PlanningService>();
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        var app = builder.Build();

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
