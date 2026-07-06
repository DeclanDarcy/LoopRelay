using LoopRelay.Core.Repositories;
using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Abstractions;

public interface IReasoningRepository
{
    Task<IReadOnlyList<ReasoningEvent>> ListEventsAsync(Repository repository);

    Task<ReasoningEvent?> GetEventAsync(Repository repository, string eventId);

    Task<ReasoningEvent> CreateEventAsync(Repository repository, CreateReasoningEventCommand command);

    Task<IReadOnlyList<ReasoningThread>> ListThreadsAsync(Repository repository);

    Task<ReasoningThread?> GetThreadAsync(Repository repository, string threadId);

    Task<ReasoningThread> CreateThreadAsync(Repository repository, CreateReasoningThreadCommand command);

    Task<ReasoningThread> AppendThreadEventAsync(Repository repository, string threadId, string eventId);

    Task<IReadOnlyList<ReasoningRelationship>> ListRelationshipsAsync(Repository repository);

    Task<ReasoningRelationship> CreateRelationshipAsync(Repository repository, CreateReasoningRelationshipCommand command);

    Task<IReadOnlyList<ReasoningReconstructionReport>> ListReconstructionReportsAsync(Repository repository);

    Task<ReasoningReconstructionReport> SaveReconstructionReportAsync(Repository repository, ReasoningReconstructionReport report);

    Task<IReadOnlyList<ReasoningCertificationReport>> ListCertificationReportsAsync(Repository repository);

    Task<ReasoningCertificationReport> SaveCertificationReportAsync(Repository repository, ReasoningCertificationReport report);
}
