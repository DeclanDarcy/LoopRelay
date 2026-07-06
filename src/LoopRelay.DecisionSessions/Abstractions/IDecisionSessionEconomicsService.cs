using LoopRelay.DecisionSessions.Models;

namespace LoopRelay.DecisionSessions.Abstractions;

public interface IDecisionSessionEconomicsService
{
    Task<DecisionSessionEconomicsSnapshot> GetEconomicsAsync(Guid repositoryId);
}
