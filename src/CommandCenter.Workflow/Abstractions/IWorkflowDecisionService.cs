using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowDecisionService
{
    Task<WorkflowDecisionProjection> ProjectDecisionAsync(Guid repositoryId);
}
