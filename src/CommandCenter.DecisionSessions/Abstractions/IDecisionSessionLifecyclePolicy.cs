using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionLifecyclePolicy
{
    Task<DecisionSessionLifecycleSnapshot> EvaluateAsync(Guid repositoryId);
}
