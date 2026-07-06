using LoopRelay.Reasoning.Abstractions;
using LoopRelay.Reasoning.Projections;
using LoopRelay.Reasoning.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LoopRelay.Reasoning.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReasoning(this IServiceCollection services)
    {
        services.AddSingleton<IReasoningArtifactProjectionService, ReasoningArtifactProjectionService>();
        services.AddSingleton<IReasoningRepository, FileSystemReasoningRepository>();
        services.AddSingleton<IReasoningEventService, ReasoningEventService>();
        services.AddSingleton<IReasoningManualCaptureService, ReasoningManualCaptureService>();
        services.AddSingleton<IReasoningThreadService, ReasoningThreadService>();
        services.AddSingleton<IReasoningRelationshipService, ReasoningRelationshipService>();
        services.AddSingleton<IReasoningGraphService, ReasoningGraphService>();
        services.AddSingleton<IReasoningReconstructionService, ReasoningReconstructionService>();
        services.AddSingleton<IReasoningQueryService, ReasoningQueryService>();
        services.AddSingleton<IReasoningMaterializationReviewService, ReasoningMaterializationReviewService>();
        services.AddSingleton<IReasoningCertificationService, ReasoningCertificationService>();
        return services;
    }
}
