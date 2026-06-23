using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowRecoveryService(
    IRepositoryService repositoryService,
    IWorkflowProjectionService projectionService,
    IWorkflowRepository workflowRepository) : IWorkflowRecoveryService
{
    public async Task<WorkflowTimeline> RebuildTimelineAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
        WorkflowTimeline timeline = CreateTimeline(projection);
        await workflowRepository.SaveTimelineAsync(repository, timeline);
        return timeline;
    }

    public async Task<WorkflowRecoveryResult> RecoverCurrentWorkflowAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
        WorkflowTimeline rebuilt = CreateTimeline(projection);
        WorkflowTimeline? latest = null;
        var diagnostics = new List<string>();
        var discardedArtifacts = new List<string>();
        var recoveredArtifacts = new List<string>();

        try
        {
            latest = await workflowRepository.GetLatestTimelineAsync(repository);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            diagnostics.Add($"Persisted workflow timeline could not be loaded: {exception.Message}");
        }

        if (latest?.Fingerprint == rebuilt.Fingerprint)
        {
            diagnostics.Add("Persisted workflow timeline matches domain-derived projection.");
            recoveredArtifacts.Add(latest.Fingerprint);
            return new WorkflowRecoveryResult(
                latest,
                new WorkflowRecoveryDiagnostics(
                    repository.Id,
                    DateTimeOffset.UtcNow,
                    rebuilt.Fingerprint,
                    latest.Fingerprint,
                    false,
                    true,
                    recoveredArtifacts,
                    discardedArtifacts,
                    diagnostics));
        }

        if (latest is null)
        {
            diagnostics.Add("No persisted workflow timeline exists; rebuilt timeline from domain evidence.");
        }
        else
        {
            diagnostics.Add("Persisted workflow timeline conflicts with domain-derived projection; rebuilt timeline from domain evidence.");
            discardedArtifacts.Add(latest.Fingerprint);
        }

        await workflowRepository.SaveTimelineAsync(repository, rebuilt);
        recoveredArtifacts.Add(rebuilt.Fingerprint);

        return new WorkflowRecoveryResult(
            rebuilt,
            new WorkflowRecoveryDiagnostics(
                repository.Id,
                DateTimeOffset.UtcNow,
                rebuilt.Fingerprint,
                latest?.Fingerprint,
                true,
                false,
                recoveredArtifacts,
                discardedArtifacts,
                diagnostics));
    }

    public async Task<WorkflowRecoveryDiagnostics> ValidateRecoveredWorkflowAsync(Guid repositoryId) =>
        (await RecoverCurrentWorkflowAsync(repositoryId)).Diagnostics;

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(candidate => candidate.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static WorkflowTimeline CreateTimeline(WorkflowInstance projection)
    {
        WorkflowStage previousStage = projection.Timeline.Count == 0
            ? WorkflowStage.Unknown
            : projection.Timeline[^1].Stage;
        string fingerprint = WorkflowFingerprint.ForTimeline(
            projection.RepositoryId,
            projection.CurrentStage,
            previousStage,
            projection.ProgressState,
            projection.BlockingGate,
            projection.Timeline,
            projection.BlockedTransitions.Select(transition => transition.BlockingCondition).ToArray()).Value;

        return new WorkflowTimeline(
            projection.RepositoryId,
            projection.CurrentStage,
            previousStage,
            projection.ProgressState,
            projection.BlockingGate,
            DateTimeOffset.UtcNow,
            projection.Timeline,
            fingerprint);
    }
}
