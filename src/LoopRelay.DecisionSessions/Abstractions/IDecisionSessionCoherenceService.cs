using LoopRelay.DecisionSessions.Models;

namespace LoopRelay.DecisionSessions.Abstractions;

public interface IDecisionSessionCoherenceService
{
    Task<DecisionSessionCoherenceSnapshot> GetCoherenceAsync(Guid repositoryId);
}
