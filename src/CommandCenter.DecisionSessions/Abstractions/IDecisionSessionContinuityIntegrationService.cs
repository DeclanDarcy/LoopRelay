using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionContinuityIntegrationService
{
    Task<IReadOnlyList<string>> IntegrateAsync(Guid repositoryId, DecisionSessionContinuityArtifact artifact);
}
