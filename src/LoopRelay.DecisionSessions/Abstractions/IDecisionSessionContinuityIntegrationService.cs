using LoopRelay.DecisionSessions.Models;

namespace LoopRelay.DecisionSessions.Abstractions;

public interface IDecisionSessionContinuityIntegrationService
{
    Task<IReadOnlyList<string>> IntegrateAsync(Guid repositoryId, DecisionSessionContinuityArtifact artifact);
}
