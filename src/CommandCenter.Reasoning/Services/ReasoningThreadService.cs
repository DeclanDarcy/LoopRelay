using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Services;

public sealed class ReasoningThreadService(
    IRepositoryService repositoryService,
    IReasoningRepository reasoningRepository)
    : IReasoningThreadService
{
    public async Task<IReadOnlyList<ReasoningThread>> ListThreadsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await reasoningRepository.ListThreadsAsync(repository);
    }

    public async Task<ReasoningThread> GetThreadAsync(Guid repositoryId, string threadId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await reasoningRepository.GetThreadAsync(repository, threadId)
            ?? throw new KeyNotFoundException($"Reasoning thread was not found: {threadId}");
    }

    public async Task<ReasoningThread> CreateThreadAsync(Guid repositoryId, CreateReasoningThreadCommand command)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        try
        {
            return await reasoningRepository.CreateThreadAsync(repository, command);
        }
        catch (ReasoningValidationException exception) when (IsMissingReasoningReference(exception))
        {
            throw new ReasoningConflictException(exception.Message);
        }
    }

    public async Task<ReasoningThread> AppendThreadEventAsync(Guid repositoryId, string threadId, string eventId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        try
        {
            return await reasoningRepository.AppendThreadEventAsync(repository, threadId, eventId);
        }
        catch (ReasoningValidationException exception) when (IsMissingReasoningReference(exception))
        {
            throw new ReasoningConflictException(exception.Message);
        }
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static bool IsMissingReasoningReference(ReasoningValidationException exception)
    {
        return exception.Message.StartsWith("Reasoning event ", StringComparison.Ordinal) ||
            exception.Message.StartsWith("Reasoning thread ", StringComparison.Ordinal);
    }
}
