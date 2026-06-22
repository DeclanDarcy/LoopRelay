using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Decisions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDecisions(this IServiceCollection services)
    {
        services.AddSingleton<IDecisionRepository, FileSystemDecisionRepository>();
        services.AddSingleton<IDecisionArtifactProjectionService, DecisionArtifactProjectionService>();
        services.AddSingleton<IDecisionContextService, DecisionContextService>();
        services.AddSingleton<IDecisionDiscoveryService, DecisionDiscoveryService>();
        return services;
    }
}
