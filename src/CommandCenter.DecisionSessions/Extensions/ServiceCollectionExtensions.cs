using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CommandCenter.DecisionSessions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDecisionSessions(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IDecisionSessionRepository, FileSystemDecisionSessionRepository>();
        services.AddSingleton<IDecisionSessionRegistry, DecisionSessionRegistry>();
        services.AddSingleton<IDecisionSessionRecoveryService, DecisionSessionRecoveryService>();
        services.AddSingleton<IDecisionSessionEvidenceReader, DecisionSessionEvidenceReader>();
        services.AddSingleton<ITokenEstimator, DeterministicTokenEstimator>();
        services.AddSingleton<IDecisionSessionMetricsService, DecisionSessionMetricsService>();
        services.AddSingleton(new DecisionSessionEconomicsOptions());
        services.AddSingleton<IDecisionSessionEconomicsService, DecisionSessionEconomicsService>();
        services.AddSingleton(new DecisionSessionCoherenceOptions());
        services.AddSingleton<IDecisionSessionCoherenceService, DecisionSessionCoherenceService>();
        services.AddSingleton(new DecisionSessionLifecyclePolicyOptions());
        services.AddSingleton<IDecisionSessionLifecyclePolicy, DecisionSessionLifecyclePolicy>();
        services.AddSingleton<IDecisionSessionTransferEligibilityService, DecisionSessionTransferEligibilityService>();
        return services;
    }
}
