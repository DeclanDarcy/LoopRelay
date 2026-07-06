using LoopRelay.Execution.Abstractions;
using LoopRelay.Persistence.Sqlite.Abstractions;
using Microsoft.Extensions.Hosting;

namespace LoopRelay.Execution.Services;

/// <summary>
/// The ONE eager-but-cheap recovery the Derivation Cache refactor keeps (refactor-lazy-sqlite.md, "The one
/// exception — execution orphan recovery"). Orphaned <c>Executing</c> sessions must be reconciled (reattach or
/// fail) deterministically: provider reattach needs live OS process handles and a stale <c>Executing</c> would
/// wrongly block a fresh <c>StartAsync</c>. It is a correctness state-mutation, not a derived cache, so it is NOT
/// made lazy.
///
/// What changed in Phase 5: it no longer runs on the pre-Kestrel <see cref="IHostedService.StartAsync"/> path
/// (which blocked cold start). It now runs from <see cref="IHostedLifecycleService.StartedAsync"/>, which fires
/// AFTER Kestrel binds — so Kestrel binds with zero per-repo derivation work and this state-fix happens just
/// after — and it is guarded by the global <c>recovery_ledger.execution_recovered_at</c> stamp so it runs at most
/// once per process. The underlying <see cref="IExecutionSessionService.RecoverAsync"/> still loads one global
/// session file behind its existing <c>SemaphoreSlim</c>.
/// </summary>
public sealed class ExecutionSessionRecoveryHostedService(
    IExecutionSessionService executionSessionService,
    IRecoveryLedgerStore recoveryLedger,
    TimeProvider timeProvider) : IHostedLifecycleService
{
    // Pre-bind hooks: deliberately no-ops. Execution recovery must NOT block Kestrel binding.
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Fires once Kestrel has bound. Claims the global execution-recovery slot (atomic, once per process) and,
    /// iff this caller won the claim, runs orphan reconciliation. A failure here must not fault host startup —
    /// the served endpoints already degrade gracefully — so it is best-effort.
    /// </summary>
    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await recoveryLedger.TryClaimExecutionRecoveryAsync(timeProvider.GetUtcNow(), cancellationToken)
                    .ConfigureAwait(false))
            {
                await executionSessionService.RecoverAsync().ConfigureAwait(false);
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Orphan reconciliation is best-effort post-bind; a failure must never block or fault host startup.
        }
    }

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
