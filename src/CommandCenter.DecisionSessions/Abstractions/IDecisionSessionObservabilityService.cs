using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionObservabilityService
{
    Task<DecisionSessionLifecycleProjection> GetProjectionAsync(Guid repositoryId);

    Task<DecisionSessionLifecycleHistory> GetHistoryAsync(Guid repositoryId);
}
