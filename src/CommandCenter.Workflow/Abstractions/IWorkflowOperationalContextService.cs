using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowOperationalContextService
{
    Task<WorkflowOperationalContextProjection> ProjectOperationalContextAsync(
        Guid repositoryId,
        WorkflowDecisionProjection decision,
        WorkflowExecutionProjection execution);
}
