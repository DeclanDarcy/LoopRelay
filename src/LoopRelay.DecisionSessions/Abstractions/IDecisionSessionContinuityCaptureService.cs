using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Primitives;

namespace LoopRelay.DecisionSessions.Abstractions;

public interface IDecisionSessionContinuityCaptureService
{
    Task<DecisionSessionContinuityArtifact> CaptureAsync(Guid repositoryId, DecisionSessionId sourceSessionId);
}
