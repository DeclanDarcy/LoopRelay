using CommandCenter.Core.Repositories;
using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Abstractions;

public interface IWorkflowRepository
{
    Task<WorkflowTimeline> SaveTimelineAsync(Repository repository, WorkflowTimeline timeline);

    Task<WorkflowTimeline?> LoadTimelineAsync(Repository repository, string timelineId);

    Task<IReadOnlyList<WorkflowTimeline>> ListTimelinesAsync(Repository repository);

    Task<WorkflowTimeline?> GetLatestTimelineAsync(Repository repository);

    Task<WorkflowContinuationEvent> SaveContinuationEventAsync(Repository repository, WorkflowContinuationEvent continuationEvent);

    Task<WorkflowContinuationEvent?> LoadContinuationEventAsync(Repository repository, string eventId);

    Task<IReadOnlyList<WorkflowContinuationEvent>> ListContinuationEventsAsync(Repository repository);

    Task<WorkflowPreparationEvent> SavePreparationEventAsync(Repository repository, WorkflowPreparationEvent preparationEvent);

    Task<WorkflowPreparationEvent?> LoadPreparationEventAsync(Repository repository, string eventId);

    Task<IReadOnlyList<WorkflowPreparationEvent>> ListPreparationEventsAsync(Repository repository);

    Task<IReadOnlyList<string>> ListHistoryLoadErrorsAsync(Repository repository);

    Task SaveReportAsync(Repository repository, string reportId, string jsonContent, string markdownContent);
}
