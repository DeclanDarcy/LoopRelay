using CommandCenter.Orchestration.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CommandCenter.Orchestration.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the repository orchestrator composition root: the singleton registry plus the
    /// <c>IMemoryCache</c> that backs the reserved <c>{repositoryId}:Plan</c> run slot. Mirrors the
    /// <c>AddAgents</c>/<c>AddExecution</c> <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton"/> convention.
    /// </summary>
    public static IServiceCollection AddOrchestration(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.TryAddSingleton<RepositoryOrchestratorRegistry>();
        return services;
    }
}
