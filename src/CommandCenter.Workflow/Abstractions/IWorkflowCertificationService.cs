using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowCertificationService
{
    Task<WorkflowCertificationResult> GetCurrentCertificationAsync(Guid repositoryId);

    Task<WorkflowCertificationResult> RunCertificationAsync(Guid repositoryId);
}
