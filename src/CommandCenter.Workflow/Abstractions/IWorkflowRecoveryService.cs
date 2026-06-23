using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowRecoveryService
{
    Task<WorkflowTimeline> RebuildTimelineAsync(Guid repositoryId);

    Task<WorkflowRecoveryResult> RecoverCurrentWorkflowAsync(Guid repositoryId);

    Task<WorkflowRecoveryDiagnostics> ValidateRecoveredWorkflowAsync(Guid repositoryId);
}
