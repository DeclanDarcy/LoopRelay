using CommandCenter.Persistence.Sqlite.Extensions;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CommandCenter.Workflow.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflow(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // The Derivation Cache primitives (refactor-lazy-sqlite.md). Phase 4 consumes IDerivedSnapshotReader
        // here so WorkflowRecoveryService can gate the expensive ProjectAsync derivation behind the source
        // fingerprint. AddSqlitePersistence is TryAdd-based and idempotent, so registering it here is safe
        // even when the host (or AddDecisionSessions) also registers it.
        services.AddSqlitePersistence();
        services.AddSingleton<IWorkflowRepository, FileSystemWorkflowRepository>();
        services.AddSingleton<IWorkflowExecutionService, WorkflowExecutionService>();
        services.AddSingleton<IWorkflowHandoffService, WorkflowHandoffService>();
        services.AddSingleton<IWorkflowDecisionService, WorkflowDecisionService>();
        services.AddSingleton<IWorkflowOperationalContextService, WorkflowOperationalContextService>();
        services.AddSingleton<IWorkflowGitService, WorkflowGitService>();
        services.AddSingleton<IWorkflowDecisionSessionService, WorkflowDecisionSessionService>();
        services.AddSingleton<IWorkflowStateMachineService, WorkflowStateMachineService>();
        services.AddSingleton<IWorkflowProjectionService, WorkflowProjectionService>();
        services.AddSingleton<IWorkflowGateCatalogService, WorkflowGateCatalogService>();
        services.AddSingleton<IWorkflowContinuationService, WorkflowContinuationService>();
        services.AddSingleton<IWorkflowPreparationService, WorkflowPreparationService>();
        services.AddSingleton<IWorkflowHealthService, WorkflowHealthService>();
        services.AddSingleton<IWorkflowCertificationService, WorkflowCertificationService>();
        services.AddSingleton<IWorkflowReportService, WorkflowReportService>();
        services.AddSingleton<IWorkflowRecoveryService, WorkflowRecoveryService>();

        // Phase 5 (refactor-lazy-sqlite.md): the WorkflowRecoveryHostedService and WorkflowContinuationHostedService
        // are DELETED — nothing runs workflow recovery, continuation, or preparation before Kestrel binds, and the
        // 60s continuation PeriodicTimer loop is gone entirely. Their bodies survive verbatim as the on-demand
        // WorkflowRecoveryRunner / WorkflowContinuationRunner, reached lazily on first access (GET /workflow/history
        // already calls RecoverCurrentWorkflowAsync inline) and coalesced once-per-process by the per-repo AsyncLazy
        // recovery gate. Continuation idempotency is preserved by the existing fingerprint-dedup.
        services.AddSingleton<WorkflowRecoveryRunner>();
        services.AddSingleton<WorkflowContinuationRunner>();
        return services;
    }
}
