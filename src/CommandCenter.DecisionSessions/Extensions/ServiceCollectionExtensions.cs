using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.DecisionSessions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDecisionSessions(this IServiceCollection services)
    {
        services.AddSingleton<IDecisionSessionRepository, FileSystemDecisionSessionRepository>();
        services.AddSingleton<IDecisionSessionRegistry, DecisionSessionRegistry>();
        services.AddSingleton<IDecisionSessionRecoveryService, DecisionSessionRecoveryService>();
        services.AddSingleton<ITokenEstimator, DeterministicTokenEstimator>();
        services.AddSingleton<IDecisionSessionMetricsService, DecisionSessionMetricsService>();
        return services;
    }
}
