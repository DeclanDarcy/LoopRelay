using CommandCenter.Agents.Extensions;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CommandCenter.Orchestration.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the repository orchestrator composition root: the role-agnostic <c>CommandCenter.Agents</c>
    /// runtime the registry hard-depends on, the singleton registry itself, the <c>IMemoryCache</c>
    /// that backs the reserved <c>{repositoryId}:Plan</c> run slot, and the Git-backed
    /// <see cref="IPlanArtifactPublisher"/> Execute Plan uses to commit/push planning + milestone
    /// artifacts (its <c>IGitService</c> dependency comes from <c>AddExecution</c>). All registrations use
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton"/>, so an earlier
    /// <c>AddAgents</c>/<c>AddExecution</c> keeps its registrations.
    /// </summary>
    public static IServiceCollection AddOrchestration(this IServiceCollection services)
    {
        services.AddAgents();
        services.AddMemoryCache();
        services.TryAddSingleton<IPlanArtifactPublisher, GitPlanArtifactPublisher>();
        services.TryAddSingleton<RepositoryOrchestratorRegistry>();
        return services;
    }
}
