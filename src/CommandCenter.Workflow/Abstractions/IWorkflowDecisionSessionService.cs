using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowDecisionSessionService
{
    Task<WorkflowDecisionSessionProjection> ProjectAsync(Guid repositoryId);

    Task<WorkflowGovernanceSummary> GetSummaryAsync(Guid repositoryId);

    Task<WorkflowGovernanceHealthProjection> GetHealthAsync(Guid repositoryId);

    Task<WorkflowGovernanceInfluenceProjection> GetInfluenceAsync(Guid repositoryId);

    Task<DecisionSessionWorkflowDiagnostics> GetDiagnosticsAsync(Guid repositoryId);
}
