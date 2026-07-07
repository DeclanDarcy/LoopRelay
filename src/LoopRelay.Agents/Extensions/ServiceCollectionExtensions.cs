using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Services;
using LoopRelay.Permissions.Extensions;
using LoopRelay.Permissions.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoopRelay.Agents.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the role-agnostic agent runtime: process runner, token estimator, turn-boundary
    /// detector, Codex launcher, session registry, and the runtime facade. Uses TryAdd so callers
    /// that already register <see cref="IProcessRunner"/> (for example AddExecution) keep their
    /// registration.
    /// </summary>
    public static IServiceCollection AddAgents(this IServiceCollection services)
    {
        services.AddCodexPermissions();
        return services.AddAgentRuntime();
    }

    public static IServiceCollection AddAgents(this IServiceCollection services, PermissionPolicyOptions policy)
    {
        services.AddCodexPermissions(policy);
        return services.AddAgentRuntime();
    }

    private static IServiceCollection AddAgentRuntime(this IServiceCollection services)
    {
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
        services.TryAddSingleton<IAgentTokenEstimator, DeterministicAgentTokenEstimator>();
        services.TryAddSingleton<IAgentTurnBoundaryDetector, CodexEventTurnBoundaryDetector>();
        services.TryAddSingleton<IAgentExecutableResolver, EnvironmentAgentExecutableResolver>();
        services.TryAddSingleton<IAgentProcessLauncher, CodexAgentProcessLauncher>();
        services.TryAddSingleton<AgentSessionRegistry>();
        services.TryAddSingleton<IAgentRuntime, AgentRuntime>();
        return services;
    }
}
