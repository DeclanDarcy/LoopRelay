using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowHandoffService
{
    Task<WorkflowHandoffProjection> ProjectHandoffAsync(Guid repositoryId);
}
