using LoopRelay.DecisionSessions.Abstractions;
using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Persistence;
using LoopRelay.DecisionSessions.Services;
using LoopRelay.Persistence.Sqlite;
using LoopRelay.Persistence.Sqlite.Abstractions;
using LoopRelay.Persistence.Sqlite.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoopRelay.DecisionSessions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDecisionSessions(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // The Derivation Cache persistence primitives (refactor-lazy-sqlite.md). Phase 2 consumes
        // ISourceFingerprintProvider from here so DecisionSessionRecoveryService can key warm-restart
        // staleness on source CONTENT instead of the fragile mtime probe. AddSqlitePersistence is
        // TryAdd-based and idempotent, so registering it here is safe if the host also registers it.
        services.AddSqlitePersistence();

        // Phase 4 (refactor-lazy-sqlite.md): the recovery-result audit moves from
        // .agents/decision-sessions/recovery/*.json to the per-repo SQLite recovery_result table. The store
        // round-trips DecisionSessionRecoveryResult through the SAME DecisionSessionJson.Options the file path
        // used, so GetHistoryAsync is shape-identical. TryAdd keeps a host-supplied override authoritative.
        services.TryAddSingleton<IRecoveryResultStore>(provider =>
            new SqliteRecoveryResultStore(
                provider.GetRequiredService<ISqliteConnectionFactory>(),
                DecisionSessionJson.Options));
        services.AddSingleton<IDecisionSessionRepository, FileSystemDecisionSessionRepository>();
        services.AddSingleton<IDecisionSessionRegistry, DecisionSessionRegistry>();
        services.AddSingleton<IDecisionSessionRecoveryService, DecisionSessionRecoveryService>();
        services.AddSingleton<IDecisionSessionEvidenceReader, DecisionSessionEvidenceReader>();
        services.AddSingleton<ITokenEstimator, DeterministicTokenEstimator>();
        services.AddSingleton<IDecisionSessionMetricsService, DecisionSessionMetricsService>();
        services.AddSingleton(new DecisionSessionEconomicsOptions());
        services.AddSingleton<IDecisionSessionEconomicsService, DecisionSessionEconomicsService>();
        services.AddSingleton(new DecisionSessionCoherenceOptions());
        services.AddSingleton<IDecisionSessionCoherenceService, DecisionSessionCoherenceService>();
        services.AddSingleton(new DecisionSessionLifecyclePolicyOptions());
        services.AddSingleton<IDecisionSessionLifecyclePolicy, DecisionSessionLifecyclePolicy>();
        services.AddSingleton<IDecisionSessionTransferEligibilityService, DecisionSessionTransferEligibilityService>();
        services.AddSingleton<IDecisionSessionContinuityArtifactService, DecisionSessionContinuityArtifactService>();
        services.AddSingleton<IDecisionSessionContinuityCaptureService, DecisionSessionContinuityCaptureService>();
        services.AddSingleton<IDecisionSessionContinuityIntegrationService, DecisionSessionContinuityIntegrationService>();
        services.AddSingleton<IDecisionSessionTransferService, DecisionSessionTransferService>();
        services.AddSingleton<IDecisionSessionObservabilityService, DecisionSessionObservabilityService>();
        services.AddSingleton<IDecisionSessionCertificationService, DecisionSessionCertificationService>();

        // Phase 5 (refactor-lazy-sqlite.md): the DecisionSessionRecoveryHostedService is DELETED — nothing runs
        // decision-session recovery before Kestrel binds. Its body survives verbatim as the on-demand
        // DecisionSessionRecoveryRunner, reached lazily on first access and coalesced once-per-process by the
        // per-repo AsyncLazy recovery gate.
        services.AddSingleton<DecisionSessionRecoveryRunner>();
        return services;
    }
}
