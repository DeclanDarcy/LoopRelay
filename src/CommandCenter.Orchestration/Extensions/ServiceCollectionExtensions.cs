using CommandCenter.Agents.Extensions;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CommandCenter.Orchestration.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the repository orchestrator composition root: the role-agnostic <c>CommandCenter.Agents</c>
    /// runtime the registry hard-depends on, the singleton registry itself, the <c>IMemoryCache</c>
    /// that backs the reserved <c>{repositoryId}:Plan</c> run slot, the Git-backed
    /// <see cref="IPlanArtifactPublisher"/> Execute Plan uses to commit/push planning + milestone
    /// artifacts (its <c>IGitService</c> dependency comes from <c>AddExecution</c>), and the
    /// registry-free <see cref="IDecisionSessionRouter"/> (+ its <see cref="DecisionSessionRouterOptions"/>)
    /// the continuation loop consults to Continue-or-Transfer on decision-session token pressure (m7). All
    /// registrations use <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton"/>, so an earlier
    /// <c>AddAgents</c>/<c>AddExecution</c> keeps its registrations.
    /// </summary>
    public static IServiceCollection AddOrchestration(this IServiceCollection services)
    {
        services.AddAgents();
        services.AddMemoryCache();
        services.TryAddSingleton<IPlanArtifactPublisher, GitPlanArtifactPublisher>();
        services.TryAddSingleton(new DecisionSessionRouterOptions());
        services.TryAddSingleton<IDecisionSessionRouter, DecisionSessionRouter>();
        // m10 hardening/certification flags. A default instance reproduces today's behavior byte-for-byte; a
        // deployment may override it (e.g. bind from configuration in Program.cs) before this default lands.
        services.TryAddSingleton(new OrchestrationFeatureFlags());
        services.TryAddSingleton<RepositoryOrchestratorRegistry>();
        // m10: a best-effort, bounded app-shutdown hook so a graceful stop disposes the orchestrator + agent
        // registries (held-open codex app-server processes) rather than relying on DI container disposal alone.
        services.AddHostedService<OrchestratorShutdownHostedService>();
        return services;
    }
}
