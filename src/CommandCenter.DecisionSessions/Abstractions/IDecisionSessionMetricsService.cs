using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionMetricsService
{
    Task<DecisionSessionMetricsSnapshot> GetMetricsAsync(Guid repositoryId);
}
