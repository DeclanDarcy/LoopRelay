using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Decisions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDecisions(this IServiceCollection services)
    {
        return services;
    }
}
