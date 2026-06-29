using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Services;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Modules;
using CommandCenter.Execution.Services;
using CommandCenter.Persistence.Sqlite.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Execution.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddExecution(this IServiceCollection services)
    {
        // The Derivation Cache persistence primitives (refactor-lazy-sqlite.md, Phase 5). Execution recovery is
        // the one eager recovery the refactor keeps, now moved to the post-bind StartedAsync hook and guarded by
        // the global recovery_ledger via IRecoveryLedgerStore. AddSqlitePersistence is TryAdd-based and
        // idempotent, so registering it here is safe even when the host (or another module) also registers it.
        services.AddSqlitePersistence();
        services.AddSingleton<IExecutionContextService, ExecutionContextService>();
        services.AddSingleton<IExecutionPromptBuilder, ExecutionPromptBuilder>();
        services.AddSingleton<IExecutionSessionStore, FileSystemExecutionSessionStore>();
        services.AddSingleton<IExecutionSessionService, ExecutionSessionService>();
        services.AddSingleton<IExecutionGitEligibilityService, ExecutionGitEligibilityService>();
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
