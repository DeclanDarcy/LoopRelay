using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowContinuationService
{
    Task<WorkflowContinuationEvaluation> EvaluateContinuationAsync(Guid repositoryId);
}
