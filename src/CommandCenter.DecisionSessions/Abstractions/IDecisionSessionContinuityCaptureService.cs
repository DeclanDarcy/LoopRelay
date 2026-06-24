using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Primitives;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionContinuityCaptureService
{
    Task<DecisionSessionContinuityArtifact> CaptureAsync(Guid repositoryId, DecisionSessionId sourceSessionId);
}
