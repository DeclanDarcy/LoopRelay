using CommandCenter.Agents.Extensions;
using CommandCenter.Orchestration.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CommandCenter.Orchestration.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the repository orchestrator composition root: the role-agnostic <c>CommandCenter.Agents</c>
    /// runtime the registry hard-depends on, the singleton registry itself, and the <c>IMemoryCache</c>
    /// that backs the reserved <c>{repositoryId}:Plan</c> run slot. All registrations use the
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton"/> convention, so an earlier
    /// <c>AddAgents</c>/<c>AddExecution</c> keeps its registrations.
    /// </summary>
    public static IServiceCollection AddOrchestration(this IServiceCollection services)
    {
        services.AddAgents();
        services.AddMemoryCache();
        services.TryAddSingleton<RepositoryOrchestratorRegistry>();
        return services;
    }
}
