using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowGateCatalogService
{
    WorkflowGateCatalogProjection ProjectGates(WorkflowInstance projection);

    Task<WorkflowGateCatalogProjection> GetGatesAsync(Guid repositoryId);

    Task<WorkflowGateHistoryProjection> GetGateHistoryAsync(Guid repositoryId);
}
