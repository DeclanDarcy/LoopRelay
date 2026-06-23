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
        services.AddSingleton<IOptionGenerationService, OptionGenerationService>();
        services.AddSingleton<IDecisionGenerationService, DecisionGenerationService>();
        services.AddSingleton<IDecisionResolutionService, DecisionResolutionService>();
        services.AddSingleton<IDecisionRefinementService, DecisionRefinementService>();
        services.AddSingleton<IDecisionReviewService, DecisionReviewService>();
        services.AddSingleton<IDecisionOperationalContextAssimilationService, DecisionOperationalContextAssimilationService>();
        services.AddSingleton<IDecisionGovernanceService, DecisionGovernanceService>();
        services.AddSingleton<IDecisionProjectionService, DecisionProjectionService>();
        services.AddSingleton<IDecisionCertificationService, DecisionCertificationService>();
        return services;
    }
}
