using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowStateMachineService
{
    IReadOnlyList<WorkflowTransition> GetCanonicalGraph();

    WorkflowStateMachineDiagnostics Evaluate(
        Guid repositoryId,
        WorkflowStage currentStage,
        WorkflowProgressState progressState,
        WorkflowGateType blockingGate,
        string requiredHumanAction);

    WorkflowTransitionResult ValidateTransition(
        WorkflowStage fromStage,
        WorkflowStage toStage,
        WorkflowProgressState progressState,
        WorkflowGateType blockingGate,
        string requiredHumanAction);
}
