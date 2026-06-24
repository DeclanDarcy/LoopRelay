using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowExecutionService
{
    Task<WorkflowExecutionProjection> ProjectExecutionAsync(Guid repositoryId);
}
