using CommandCenter.Core.Repositories;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowContinuationHostedService(
    IRepositoryService repositoryService,
    IWorkflowContinuationService continuationService,
    IWorkflowPreparationService preparationService,
    IOptions<WorkflowContinuationOptions> options) : IHostedService
{
    private CancellationTokenSource? cancellationTokenSource;
    private Task? loopTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        WorkflowContinuationOptions currentOptions = options.Value;
        if (!currentOptions.ContinuationEnabled)
        {
            return;
        }

        cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await RunOnceAsync(cancellationTokenSource.Token);
        loopTask = RunLoopAsync(currentOptions, cancellationTokenSource.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (cancellationTokenSource is null)
        {
            return;
        }

        await cancellationTokenSource.CancelAsync();
        if (loopTask is not null)
        {
            try
            {
                await loopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellationTokenSource.Dispose();
        cancellationTokenSource = null;
        loopTask = null;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Repository> repositories = await repositoryService.GetAllAsync();
        foreach (Repository repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await continuationService.RunContinuationAsync(repository.Id, "hosted");
                WorkflowPreparationEvaluation preparationEvaluation =
                    await preparationService.EvaluatePreparationAsync(repository.Id);
                IReadOnlyList<WorkflowPreparationEvent> preparationHistory =
                    await preparationService.GetPreparationHistoryAsync(repository.Id);
                if (!HasAlreadyPreparedDuplicateEvidence(preparationEvaluation, preparationHistory))
                {
                    await preparationService.RunPreparationAsync(repository.Id, "hosted");
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Continuation and preparation are derived evidence. A repository
                // with incomplete or conflicting evidence must not stop unrelated
                // repositories from being evaluated by the hosted runner.
            }
        }
    }

    private async Task RunLoopAsync(
        WorkflowContinuationOptions currentOptions,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(ResolveIntervalSeconds(currentOptions)));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RunOnceAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static int ResolveIntervalSeconds(WorkflowContinuationOptions currentOptions) =>
        currentOptions.ContinuationIntervalSeconds > 0
            ? currentOptions.ContinuationIntervalSeconds
            : 60;

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
