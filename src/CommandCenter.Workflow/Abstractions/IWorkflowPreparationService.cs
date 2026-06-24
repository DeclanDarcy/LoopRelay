using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowPreparationService
{
    Task<WorkflowPreparationEvaluation> EvaluatePreparationAsync(Guid repositoryId);

    Task<WorkflowPreparationEvent> RunPreparationAsync(Guid repositoryId, string trigger = "endpoint");

    Task<IReadOnlyList<WorkflowPreparationEvent>> GetPreparationHistoryAsync(Guid repositoryId);
}
