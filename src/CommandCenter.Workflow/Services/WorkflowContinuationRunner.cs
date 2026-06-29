using CommandCenter.Core.Repositories;
using CommandCenter.Persistence.Sqlite.Abstractions;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using Microsoft.Extensions.Options;

namespace CommandCenter.Workflow.Services;

/// <summary>
/// On-demand workflow continuation + preparation entrypoint (refactor-lazy-sqlite.md, Phase 5). This carries the
/// body of the deleted <c>WorkflowContinuationHostedService</c> — including the
/// <see cref="HasAlreadyPreparedDuplicateEvidence"/> dedup that keeps repeated runs idempotent — but with the
/// <b>60s <see cref="PeriodicTimer"/> loop removed entirely</b>: continuation/preparation are now compute-on-
/// demand, and the existing fingerprint-dedup prevents duplicate events. It is no longer an
/// <see cref="Microsoft.Extensions.Hosting.IHostedService"/>; nothing runs before Kestrel binds. The per-repo
/// entrypoint routes through <see cref="IPerRepositoryRecoveryGate"/> so concurrent same-repo continuations
/// SERIALIZE (the service has no internal lock), while each call RE-RUNS and returns its fresh result — a second
/// <c>POST /workflow/continuation/run</c> must reflect current state, not a cached first result.
/// </summary>
public sealed class WorkflowContinuationRunner(
    IRepositoryService repositoryService,
    IWorkflowContinuationService continuationService,
    IWorkflowPreparationService preparationService,
    IPerRepositoryRecoveryGate recoveryGate,
    IOptions<WorkflowContinuationOptions> options)
{
    private const string GateScope = "workflow-continuation";

    /// <summary>
    /// Runs a single continuation for a repository through the gate, serializing concurrent same-repo callers and
    /// returning the FRESH <see cref="WorkflowContinuationEvent"/> of this invocation (no caching). This is the
    /// live-read endpoint entrypoint for <c>POST /workflow/continuation/run</c>.
    /// </summary>
    public Task<WorkflowContinuationEvent> RunContinuationAsync(
        Guid repositoryId, string trigger = "endpoint", CancellationToken cancellationToken = default) =>
        recoveryGate.RunAsync(
            GateScope,
            repositoryId,
            ct => continuationService.RunContinuationAsync(repositoryId, trigger),
            cancellationToken);

    /// <summary>
    /// Runs continuation + preparation across every registered repository (the old hosted <c>RunOnceAsync</c>
    /// body, verbatim minus the timer loop), gated by <see cref="WorkflowContinuationOptions.ContinuationEnabled"/>,
    /// serialized per repo through the gate, and tolerant of per-repository failure. Used on-demand and by the
    /// tests that previously drove the hosted class.
    /// </summary>
    public async Task RunAllAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.ContinuationEnabled)
        {
            return;
        }

        IReadOnlyList<Repository> repositories = await repositoryService.GetAllAsync();
        foreach (Repository repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await recoveryGate.RunAsync(
                    GateScope,
                    repository.Id,
                    ct => RunRepositoryAsync(repository.Id),
                    cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Continuation and preparation are derived evidence. A repository with incomplete or conflicting
                // evidence must not stop unrelated repositories from being evaluated.
            }
        }
    }

    private async Task RunRepositoryAsync(Guid repositoryId)
    {
        await continuationService.RunContinuationAsync(repositoryId, "hosted");
        WorkflowPreparationEvaluation preparationEvaluation =
            await preparationService.EvaluatePreparationAsync(repositoryId);
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory =
            await preparationService.GetPreparationHistoryAsync(repositoryId);
        if (!HasAlreadyPreparedDuplicateEvidence(preparationEvaluation, preparationHistory))
        {
            await preparationService.RunPreparationAsync(repositoryId, "hosted");
        }
    }

    private static bool HasAlreadyPreparedDuplicateEvidence(
        WorkflowPreparationEvaluation preparationEvaluation,
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        if (!preparationEvaluation.HasDuplicateDomainEvidence ||
            preparationEvaluation.DuplicateEvidence.Count == 0)
        {
            return false;
        }

        return preparationHistory.Any(preparationEvent =>
            preparationEvent.Stage == preparationEvaluation.Stage &&
            preparationEvent.Command == preparationEvaluation.Command &&
            preparationEvent.CreatedArtifactIds.Count > 0 &&
            preparationEvent.CreatedArtifactIds.Any(createdArtifactId =>
                preparationEvaluation.DuplicateEvidence.Contains(createdArtifactId, StringComparer.Ordinal)));
    }
}
