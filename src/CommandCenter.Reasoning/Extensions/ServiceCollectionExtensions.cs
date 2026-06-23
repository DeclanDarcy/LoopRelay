using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Reasoning.Extensions;

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
        return services;
    }
}
