using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionContinuityIntegrationService : IDecisionSessionContinuityIntegrationService
{
    public Task<IReadOnlyList<string>> IntegrateAsync(Guid repositoryId, DecisionSessionContinuityArtifact artifact)
    {
        if (artifact.RepositoryId != repositoryId)
        {
            throw new DecisionSessionValidationException("Continuity artifact belongs to a different repository.");
        }

        IReadOnlyList<string> diagnostics =
        [
            "Continuity artifact was validated for transfer integration.",
            "Decision Sessions contributed continuity evidence without taking ownership of operational context."
        ];
        return Task.FromResult(diagnostics);
    }
}
