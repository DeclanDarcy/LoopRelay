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

        return app;
    }

    public static void Main(string[] args)
    {
        CreateApp(args).Run();
    }
}
