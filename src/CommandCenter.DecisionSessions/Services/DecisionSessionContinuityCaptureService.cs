using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Primitives;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionContinuityCaptureService(
    IDecisionSessionContinuityArtifactService artifactService) : IDecisionSessionContinuityCaptureService
{
    public Task<DecisionSessionContinuityArtifact> CaptureAsync(Guid repositoryId, DecisionSessionId sourceSessionId)
    {
        return artifactService.CreateAsync(repositoryId, sourceSessionId);
    }
}
