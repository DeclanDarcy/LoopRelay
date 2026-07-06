using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Abstractions;

public interface IReasoningRelationshipService
{
    Task<IReadOnlyList<ReasoningRelationship>> ListRelationshipsAsync(Guid repositoryId);

    Task<ReasoningRelationship> CreateRelationshipAsync(Guid repositoryId, CreateReasoningRelationshipCommand command);
}
