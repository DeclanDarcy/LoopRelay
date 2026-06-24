using CommandCenter.Core.Repositories;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowReportService(
    IRepositoryService repositoryService,
    IWorkflowProjectionService projectionService,
    IWorkflowRepository workflowRepository,
    IWorkflowHealthService healthService,
    IWorkflowCertificationService certificationService) : IWorkflowReportService
{
    public async Task<RepositoryWorkflowReport> GetRepositoryReportAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowInstance projection = await projectionService.ProjectAsync(repository.Id);
        WorkflowHealthAssessment health = await healthService.AssessHealthAsync(repository.Id);
        WorkflowCertificationResult certification = await certificationService.GetCurrentCertificationAsync(repository.Id);
        IReadOnlyList<WorkflowContinuationEvent> continuationHistory = await workflowRepository.ListContinuationEventsAsync(repository);
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory = await workflowRepository.ListPreparationEventsAsync(repository);

        return new RepositoryWorkflowReport(
            repository.Id,
            DateTimeOffset.UtcNow,
            projection.CurrentStage,
            projection.ProgressState,
            projection.BlockingGate,
            projection.RequiredHumanAction,
            projection.Timeline.Count,
            projection.OpenGates.Count,
            projection.SatisfiedGates.Count,
            continuationHistory.Count,
            preparationHistory.Count,
            health.OverallStatus,
            certification.Certified,
            certification.FailedFindingCount,
            health.Diagnostics
                .Concat(certification.Failures)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    public async Task<WorkflowProgressionReport> GetProgressionReportAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowInstance projection = await projectionService.ProjectAsync(repository.Id);
        IReadOnlyList<WorkflowContinuationEvent> continuationHistory = await workflowRepository.ListContinuationEventsAsync(repository);

        return new WorkflowProgressionReport(
            repository.Id,
            DateTimeOffset.UtcNow,
            projection.CurrentStage,
            projection.ProgressState,
            projection.BlockingGate,
            projection.ValidTransitions.Count,
            projection.BlockedTransitions.Count,
            continuationHistory.Count,
            projection.ValidTransitions
                .Select(transition => $"{transition.Transition.FromStage}->{transition.Transition.ToStage}: {transition.Reason}")
                .ToArray(),
            projection.BlockedTransitions
                .Select(transition => $"{transition.Transition.FromStage}->{transition.Transition.ToStage}: {transition.BlockingCondition}: {transition.Reason}")
                .ToArray(),
            continuationHistory
                .Select(entry => $"{entry.EventId}: {entry.Decision}: {entry.FromStage}->{entry.ToStage?.ToString() ?? "None"}")
                .ToArray(),
            projection.Diagnostics.Reasoning
                .Concat(projection.BlockedTransitions.Select(transition => transition.Reason))
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    public async Task<HumanGovernanceReport> GetHumanGovernanceReportAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowInstance projection = await projectionService.ProjectAsync(repository.Id);
        WorkflowCertificationResult certification = await certificationService.GetCurrentCertificationAsync(repository.Id);

        return new HumanGovernanceReport(
            repository.Id,
            DateTimeOffset.UtcNow,
            projection.BlockingGate,
            projection.RequiredHumanAction,
            projection.OpenGates.Count,
            projection.SatisfiedGates.Count,
            projection.OpenGates
                .Select(gate => $"{gate.Type}: {gate.RequiredAction}")
                .ToArray(),
            projection.SatisfiedGates
                .Select(gate => $"{gate.Type}: {gate.SourceDomain}:{gate.SourceArtifact}")
                .ToArray(),
            certification.Findings
                .Where(finding => string.Equals(finding.Category, "Authority", StringComparison.Ordinal))
                .Select(finding => $"{finding.Id}: passed={finding.Passed}: {finding.Summary}")
                .ToArray(),
            projection.GateDiagnostics.Reasoning
                .Concat(projection.GateDiagnostics.Conflicts)
                .Concat(certification.Findings
                    .Where(finding => string.Equals(finding.Category, "Authority", StringComparison.Ordinal))
                    .SelectMany(finding => finding.Diagnostics))
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    public async Task<WorkflowReadinessReport> GetReadinessReportAsync(Guid repositoryId)
    {
        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
        WorkflowHealthAssessment health = await healthService.AssessHealthAsync(repositoryId);
        WorkflowCertificationResult certification = await certificationService.GetCurrentCertificationAsync(repositoryId);
        IReadOnlyList<string> blockingReasons = projection.BlockedTransitions
            .Select(transition => $"{transition.BlockingCondition}: {transition.Reason}")
            .Concat(projection.OpenGates.Select(gate => $"{gate.Type}: {gate.RequiredAction}"))
            .Concat(health.Dimensions
                .Where(dimension => string.Equals(dimension.Status, "Blocked", StringComparison.Ordinal))
                .Select(dimension => $"{dimension.Name}: {dimension.Reason}"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        bool ready = certification.Certified &&
            !string.Equals(health.OverallStatus, "Degraded", StringComparison.Ordinal) &&
            projection.CurrentStage is not WorkflowStage.Unknown and not WorkflowStage.Failed;

        return new WorkflowReadinessReport(
            repositoryId,
            DateTimeOffset.UtcNow,
            ready,
            certification.Certified,
            health.OverallStatus,
            projection.CurrentStage,
            projection.ProgressState,
            projection.BlockingGate,
            blockingReasons,
            certification.Failures,
            health.Diagnostics);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(candidate => candidate.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository '{repositoryId}' was not found.");
    }
}
