using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Primitives;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionContinuityArtifactService
{
    Task<DecisionSessionContinuityArtifact> CreateAsync(
        Guid repositoryId,
        DecisionSessionId sourceSessionId,
        DecisionSessionId? targetSessionId = null);

    Task<IReadOnlyList<DecisionSessionContinuityArtifact>> ListAsync(Guid repositoryId);

    Task<DecisionSessionContinuityArtifact?> GetAsync(Guid repositoryId, string artifactId);

    Task<DecisionSessionContinuityArtifact> AttachTargetSessionAsync(
        Guid repositoryId,
        string artifactId,
        DecisionSessionId targetSessionId);

    DecisionSessionContinuityArtifactValidation Validate(DecisionSessionContinuityArtifact artifact);
}
