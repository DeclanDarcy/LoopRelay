using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowGitService
{
    Task<WorkflowGitProjection> ProjectGitAsync(
        Guid repositoryId,
        WorkflowExecutionProjection execution,
        WorkflowOperationalContextProjection operationalContext);
}
