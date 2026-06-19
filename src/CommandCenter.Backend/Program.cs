using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Projections;
using CommandCenter.Backend.Repositories;

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

        return app;
    }

    public static void Main(string[] args)
    {
        CreateApp(args).Run();
    }
}
