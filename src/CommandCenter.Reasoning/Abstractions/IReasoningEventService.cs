using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Abstractions;

public interface IReasoningEventService
{
    Task<IReadOnlyList<ReasoningEvent>> ListEventsAsync(Guid repositoryId);

    Task<ReasoningEvent> GetEventAsync(Guid repositoryId, string eventId);

    Task<ReasoningEvent> CreateEventAsync(Guid repositoryId, CreateReasoningEventCommand command);
}
