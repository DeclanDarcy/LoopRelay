using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Abstractions;

public interface IReasoningThreadService
{
    Task<IReadOnlyList<ReasoningThread>> ListThreadsAsync(Guid repositoryId);

    Task<ReasoningThread> GetThreadAsync(Guid repositoryId, string threadId);

    Task<ReasoningThread> CreateThreadAsync(Guid repositoryId, CreateReasoningThreadCommand command);

    Task<ReasoningThread> AppendThreadEventAsync(Guid repositoryId, string threadId, string eventId);
}
