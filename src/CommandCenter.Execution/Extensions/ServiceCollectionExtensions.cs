using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Modules;
using CommandCenter.Execution.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Execution.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddExecution(this IServiceCollection services)
    {
        services.AddSingleton<IExecutionContextService, ExecutionContextService>();
        services.AddSingleton<IExecutionPromptBuilder, ExecutionPromptBuilder>();
        services.AddSingleton<IExecutionSessionStore, FileSystemExecutionSessionStore>();
        services.AddSingleton<IExecutionSessionService, ExecutionSessionService>();
        services.AddHostedService<ExecutionSessionRecoveryHostedService>();
        services.AddSingleton<ExecutionEventRetentionPolicy>();
        services.AddSingleton<IExecutionMonitoringService, ExecutionMonitoringService>();
        services.AddSingleton<IHandoffService, HandoffService>();
        services.AddSingleton<ICodexExecutableResolver, CodexExecutableResolver>();
        services.AddSingleton<IExecutionProvider, CodexExecutionProvider>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IGitService, GitService>();
        return services;
    }
}
