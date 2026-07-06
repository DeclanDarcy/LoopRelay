using LoopRelay.DecisionSessions.Models;

namespace LoopRelay.DecisionSessions.Abstractions;

public interface IDecisionSessionMetricsService
{
    Task<DecisionSessionMetricsSnapshot> GetMetricsAsync(Guid repositoryId);
}
