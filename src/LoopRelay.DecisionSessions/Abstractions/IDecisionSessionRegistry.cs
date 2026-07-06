using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Primitives;

namespace LoopRelay.DecisionSessions.Abstractions;

public interface IDecisionSessionRegistry
{
    Task<DecisionSession> CreateSessionAsync(Guid repositoryId, string createdBy);

    Task<DecisionSession> ActivateSessionAsync(Guid repositoryId, DecisionSessionId sessionId);

    Task<DecisionSession> MarkTransferPendingAsync(Guid repositoryId, DecisionSessionId sessionId, string reason);

    Task<DecisionSession> MarkTransferredAsync(Guid repositoryId, DecisionSessionId sourceSessionId, DecisionSessionId targetSessionId, string reason);

    Task<DecisionSession> RetireSessionAsync(Guid repositoryId, DecisionSessionId sessionId, string reason);

    Task<DecisionSession?> GetActiveSessionAsync(Guid repositoryId);
}
