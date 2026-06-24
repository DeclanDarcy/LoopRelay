using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Workflow.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflow(this IServiceCollection services)
    {
        services.AddSingleton<IWorkflowRepository, FileSystemWorkflowRepository>();
        services.AddSingleton<IWorkflowExecutionService, WorkflowExecutionService>();
        services.AddSingleton<IWorkflowStateMachineService, WorkflowStateMachineService>();
        services.AddSingleton<IWorkflowProjectionService, WorkflowProjectionService>();
        services.AddSingleton<IWorkflowGateCatalogService, WorkflowGateCatalogService>();
        services.AddSingleton<IWorkflowRecoveryService, WorkflowRecoveryService>();
        services.AddHostedService<WorkflowRecoveryHostedService>();
        return services;
    }
}
