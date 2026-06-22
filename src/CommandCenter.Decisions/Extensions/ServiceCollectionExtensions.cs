using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Decisions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDecisions(this IServiceCollection services)
    {
        services.AddSingleton<IDecisionRepository, FileSystemDecisionRepository>();
        return services;
    }
}
