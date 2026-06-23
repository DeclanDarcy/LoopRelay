using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Abstractions;

public interface IReasoningRelationshipService
{
    Task<IReadOnlyList<ReasoningRelationship>> ListRelationshipsAsync(Guid repositoryId);

    Task<ReasoningRelationship> CreateRelationshipAsync(Guid repositoryId, CreateReasoningRelationshipCommand command);
}
