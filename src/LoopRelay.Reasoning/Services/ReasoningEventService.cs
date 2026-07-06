using LoopRelay.Core.Repositories;
using LoopRelay.Reasoning.Abstractions;
using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Services;

public sealed class ReasoningEventService(
    IRepositoryService repositoryService,
    IReasoningRepository reasoningRepository)
    : IReasoningEventService
{
    public async Task<IReadOnlyList<ReasoningEvent>> ListEventsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await reasoningRepository.ListEventsAsync(repository);
    }

    public async Task<ReasoningEvent> GetEventAsync(Guid repositoryId, string eventId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await reasoningRepository.GetEventAsync(repository, eventId)
            ?? throw new KeyNotFoundException($"Reasoning event was not found: {eventId}");
    }

    public async Task<ReasoningEvent> CreateEventAsync(Guid repositoryId, CreateReasoningEventCommand command)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await reasoningRepository.CreateEventAsync(repository, command);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }
}
