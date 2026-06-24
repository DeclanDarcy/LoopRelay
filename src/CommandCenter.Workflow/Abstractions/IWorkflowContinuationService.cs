using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowContinuationService
{
    Task<WorkflowContinuationEvaluation> EvaluateContinuationAsync(Guid repositoryId);

    Task<WorkflowContinuationEvent> RunContinuationAsync(Guid repositoryId, string trigger = "endpoint");

    Task<IReadOnlyList<WorkflowContinuationEvent>> GetContinuationHistoryAsync(Guid repositoryId);
}
