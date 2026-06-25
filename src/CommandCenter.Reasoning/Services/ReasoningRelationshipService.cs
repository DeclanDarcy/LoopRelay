using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Services;

public sealed class ReasoningRelationshipService(
    IRepositoryService repositoryService,
    IReasoningRepository reasoningRepository)
    : IReasoningRelationshipService
{
    public async Task<IReadOnlyList<ReasoningRelationship>> ListRelationshipsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await reasoningRepository.ListRelationshipsAsync(repository);
    }

    public async Task<ReasoningRelationship> CreateRelationshipAsync(Guid repositoryId, CreateReasoningRelationshipCommand command)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        try
        {
            return await reasoningRepository.CreateRelationshipAsync(repository, command);
        }
        catch (ReasoningValidationException exception) when (IsMissingReasoningReference(exception))
        {
            throw new ReasoningConflictException(exception.Message, exception.BoundaryViolation);
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
            exception.Message.StartsWith("Reasoning thread ", StringComparison.Ordinal) ||
            exception.BoundaryViolation is not null;
    }
}
