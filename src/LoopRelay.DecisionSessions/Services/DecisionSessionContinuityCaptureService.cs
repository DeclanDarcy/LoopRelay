using LoopRelay.DecisionSessions.Abstractions;
using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Primitives;

namespace LoopRelay.DecisionSessions.Services;

public sealed class DecisionSessionContinuityCaptureService(
    IDecisionSessionContinuityArtifactService artifactService) : IDecisionSessionContinuityCaptureService
{
    public Task<DecisionSessionContinuityArtifact> CaptureAsync(Guid repositoryId, DecisionSessionId sourceSessionId)
    {
        return artifactService.CreateAsync(repositoryId, sourceSessionId);
    }
}
