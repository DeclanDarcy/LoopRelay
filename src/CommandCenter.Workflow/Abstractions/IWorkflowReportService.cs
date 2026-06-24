using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowReportService
{
    Task<RepositoryWorkflowReport> GetRepositoryReportAsync(Guid repositoryId);

    Task<WorkflowProgressionReport> GetProgressionReportAsync(Guid repositoryId);

    Task<HumanGovernanceReport> GetHumanGovernanceReportAsync(Guid repositoryId);

    Task<WorkflowReadinessReport> GetReadinessReportAsync(Guid repositoryId);
}
