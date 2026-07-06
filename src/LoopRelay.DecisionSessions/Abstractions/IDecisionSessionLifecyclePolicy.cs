using LoopRelay.DecisionSessions.Models;

namespace LoopRelay.DecisionSessions.Abstractions;

public interface IDecisionSessionLifecyclePolicy
{
    Task<DecisionSessionLifecycleSnapshot> EvaluateAsync(Guid repositoryId);
}
