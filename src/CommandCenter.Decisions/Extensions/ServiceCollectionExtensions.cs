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
        services.AddSingleton<IDecisionLifecycleEligibilityService, DecisionLifecycleEligibilityService>();
        services.AddSingleton<DecisionContextService>();
        services.AddSingleton<IDecisionContextService>(provider => provider.GetRequiredService<DecisionContextService>());
        services.AddSingleton<IDecisionContextProjectionService>(provider => provider.GetRequiredService<DecisionContextService>());
        services.AddSingleton<IDecisionDiscoveryService, DecisionDiscoveryService>();
        services.AddSingleton<IOptionValidationService, OptionValidationService>();
        services.AddSingleton<IOptionGenerationService, OptionGenerationService>();
        services.AddSingleton<ITradeoffAnalysisService, TradeoffAnalysisService>();
        services.AddSingleton<IOptionComparisonService, OptionComparisonService>();
        services.AddSingleton<IRecommendationService, RecommendationService>();
        services.AddSingleton<IDecisionPackageService, DecisionPackageService>();
        services.AddSingleton<IDecisionGenerationService, DecisionGenerationService>();
        services.AddSingleton<IDecisionResolutionService, DecisionResolutionService>();
        services.AddSingleton<IRefinementAnalysisService, RefinementAnalysisService>();
        services.AddSingleton<IDecisionRefinementService, DecisionRefinementService>();
        services.AddSingleton<IDecisionReviewService, DecisionReviewService>();
        services.AddSingleton<IDecisionOperationalContextAssimilationService, DecisionOperationalContextAssimilationService>();
        services.AddSingleton<IDecisionGovernanceService, DecisionGovernanceService>();
        services.AddSingleton<IDecisionProjectionService, DecisionProjectionService>();
        services.AddSingleton<IDecisionInfluenceService, DecisionInfluenceService>();
        services.AddSingleton<IDecisionCertificationService, DecisionCertificationService>();
        services.AddSingleton<IHumanAuthoringBurdenService, HumanAuthoringBurdenService>();
        services.AddSingleton<IDecisionGenerationCertificationService, DecisionGenerationCertificationService>();
        services.AddSingleton<IDecisionQualitySignalService, DecisionQualitySignalService>();
        services.AddSingleton<IDecisionQualityAssessmentService, DecisionQualityAssessmentService>();
        services.AddSingleton<IDecisionQualityReportService, DecisionQualityReportService>();
        return services;
    }
}
