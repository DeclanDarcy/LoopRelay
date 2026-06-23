using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowProjectionService
{
    Task<WorkflowInstance> ProjectAsync(Guid repositoryId);

    Task<WorkflowProjectionDiagnostics> GetDiagnosticsAsync(Guid repositoryId);

    Task<IReadOnlyList<WorkflowTimelineEntry>> GetTimelineAsync(Guid repositoryId);
}
