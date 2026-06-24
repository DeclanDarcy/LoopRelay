using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowHealthService
{
    Task<WorkflowInfluenceTrace> TraceInfluenceAsync(Guid repositoryId);

    Task<WorkflowHealthAssessment> AssessHealthAsync(Guid repositoryId);
}
