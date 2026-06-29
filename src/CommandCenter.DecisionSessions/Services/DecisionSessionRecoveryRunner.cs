using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.Persistence.Sqlite.Abstractions;

namespace CommandCenter.DecisionSessions.Services;

/// <summary>
/// On-demand decision-session recovery entrypoint (refactor-lazy-sqlite.md, Phase 5). This carries the body of
/// the deleted <c>DecisionSessionRecoveryHostedService</c> VERBATIM — a <c>foreach (repo) RecoverAsync</c> that
/// tolerates a per-repository failure (a repo with corrupt lifecycle files must not stop the others) — but it is
/// no longer an <see cref="Microsoft.Extensions.Hosting.IHostedService"/>: nothing runs before Kestrel binds.
/// Decision-session recovery is re-derivation of stateless registry findings (the registry reads files each
/// call); recovery is triggered lazily by first access and the per-repo entrypoint routes through
/// <see cref="IPerRepositoryRecoveryGate"/> so concurrent same-repo recoveries SERIALIZE (the service has no
/// internal lock), while each call RE-RUNS and returns its fresh result — the live <c>POST /recovery</c> read
/// must reflect current state, never a cached first result.
/// </summary>
public sealed class DecisionSessionRecoveryRunner(
    IRepositoryService repositoryService,
    IDecisionSessionRecoveryService recoveryService,
    IPerRepositoryRecoveryGate recoveryGate)
{
    private const string GateScope = "decision-session-recovery";

    /// <summary>
    /// Recovers a single repository, serializing concurrent same-repo callers through the gate and returning the
    /// FRESH result of this invocation (no caching — a subsequent recovery re-runs and reflects current state).
    /// This is the live-read endpoint entrypoint for <c>POST /recovery</c>.
    /// </summary>
    public Task<DecisionSessionRecoveryResult> RecoverAsync(
        Guid repositoryId, CancellationToken cancellationToken = default) =>
        recoveryGate.RunAsync(
            GateScope,
            repositoryId,
            ct => recoveryService.RecoverAsync(repositoryId),
            cancellationToken);

    /// <summary>
    /// Recovers every registered repository (the old hosted <c>StartAsync</c> body, verbatim), tolerating a
    /// per-repository failure so one corrupt repo cannot strand the others, and serializing each repo through the
    /// gate. Used on-demand and by the test that previously drove the hosted class.
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
                    ct => recoveryService.RecoverAsync(repository.Id),
                    cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Recovery evidence is reconstructable. A repository with corrupt lifecycle files must not
                // prevent unrelated repositories from loading.
            }
        }
    }
}
