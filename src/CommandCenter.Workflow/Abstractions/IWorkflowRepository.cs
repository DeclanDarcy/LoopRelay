using CommandCenter.Core.Repositories;
using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowRepository
{
    Task<WorkflowTimeline> SaveTimelineAsync(Repository repository, WorkflowTimeline timeline);

    Task<WorkflowTimeline?> LoadTimelineAsync(Repository repository, string timelineId);

    Task<IReadOnlyList<WorkflowTimeline>> ListTimelinesAsync(Repository repository);

    Task<WorkflowTimeline?> GetLatestTimelineAsync(Repository repository);

    Task SaveReportAsync(Repository repository, string reportId, string jsonContent, string markdownContent);
}
