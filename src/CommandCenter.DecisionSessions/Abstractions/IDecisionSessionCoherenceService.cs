using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionCoherenceService
{
    Task<DecisionSessionCoherenceSnapshot> GetCoherenceAsync(Guid repositoryId);
}
