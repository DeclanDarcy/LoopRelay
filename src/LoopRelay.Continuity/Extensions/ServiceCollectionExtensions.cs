using LoopRelay.Continuity.Abstractions;
using LoopRelay.Continuity.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LoopRelay.Continuity.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContinuity(this IServiceCollection services)
    {
        services.AddSingleton<IOperationalContextParser, MarkdownOperationalContextParser>();
        services.AddSingleton<IUnderstandingDiffService, UnderstandingDiffService>();
        services.AddSingleton<IUnderstandingCompressionService, UnderstandingCompressionService>();
        services.AddSingleton<IDecisionAnalysisService, DecisionAnalysisService>();
        services.AddSingleton<IOperationalContextProposalStore, FileSystemOperationalContextProposalStore>();
        services.AddSingleton<IOperationalContextReviewService, OperationalContextReviewService>();
        services.AddSingleton<IOperationalContextLifecycleService, OperationalContextLifecycleService>();
        services.AddSingleton<IContinuityDiagnosticsService, ContinuityDiagnosticsService>();
        services.AddSingleton<IContinuityReportService, ContinuityReportService>();
        return services;
    }
}
