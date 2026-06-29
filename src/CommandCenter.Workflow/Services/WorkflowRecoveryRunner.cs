using CommandCenter.Core.Repositories;
using CommandCenter.Persistence.Sqlite.Abstractions;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Services;

/// <summary>
/// On-demand workflow-recovery entrypoint (refactor-lazy-sqlite.md, Phase 5). This carries the body of the
/// deleted <c>WorkflowRecoveryHostedService</c> VERBATIM — a <c>foreach (repo) RecoverCurrentWorkflowAsync</c>
/// that is reconstructable audit evidence and must never let one repository's incomplete evidence stop the
/// others — but it is no longer an <see cref="Microsoft.Extensions.Hosting.IHostedService"/>: nothing runs it
/// before Kestrel binds. Recovery is triggered lazily by first repo access (<c>GET /workflow/history</c> calls
/// <see cref="IWorkflowRecoveryService.RecoverCurrentWorkflowAsync"/> inline), and the per-repo entrypoint routes
/// through <see cref="IPerRepositoryRecoveryGate"/> so concurrent same-repo recoveries SERIALIZE (the service has
/// no internal lock), while each call RE-RUNS and returns its fresh result — a second <c>GET /workflow/history</c>
/// must reflect current state, not a cached first result.
/// </summary>
public sealed class WorkflowRecoveryRunner(
    IRepositoryService repositoryService,
    IWorkflowRecoveryService recoveryService,
    IPerRepositoryRecoveryGate recoveryGate)
{
    private const string GateScope = "workflow-recovery";

    /// <summary>
    /// Recovers a single repository, serializing concurrent same-repo callers through the gate and returning the
    /// FRESH result of this invocation (no caching — a subsequent call re-runs and reflects current state). This
    /// is the live-read endpoint entrypoint for <c>GET /workflow/history</c> and <c>POST /workflow/recover</c>.
    /// </summary>
    public Task<WorkflowRecoveryResult> RecoverCurrentWorkflowAsync(
        Guid repositoryId, CancellationToken cancellationToken = default) =>
        recoveryGate.RunAsync(
            GateScope,
            repositoryId,
            ct => recoveryService.RecoverCurrentWorkflowAsync(repositoryId),
            cancellationToken);

    /// <summary>
    /// Recovers every registered repository (the old hosted <c>StartAsync</c> body, verbatim), tolerating a
    /// per-repository failure so one corrupt repo cannot strand the others, and serializing each repo through the
    /// gate. Used on-demand and by the startup-parity recovery test that previously drove the hosted class.
    /// </summary>
    public async Task RecoverAllAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Repository> repositories = await repositoryService.GetAllAsync();
        foreach (Repository repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await recoveryGate.RunAsync(
                    GateScope,
                    repository.Id,
                    ct => recoveryService.RecoverCurrentWorkflowAsync(repository.Id),
                    cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Recovery is reconstructable audit evidence. A repository with incomplete local evidence must
                // not prevent unrelated repositories from being recovered.
            }
        }
    }
}
