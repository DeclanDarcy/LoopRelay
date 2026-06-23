using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Workflow.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflow(this IServiceCollection services)
    {
        services.AddSingleton<IWorkflowProjectionService, WorkflowProjectionService>();
        return services;
    }
}
