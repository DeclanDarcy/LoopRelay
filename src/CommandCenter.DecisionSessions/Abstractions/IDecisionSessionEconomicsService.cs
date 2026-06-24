using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionEconomicsService
{
    Task<DecisionSessionEconomicsSnapshot> GetEconomicsAsync(Guid repositoryId);
}
