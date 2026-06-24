using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Primitives;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionRepository
{
    Task<DecisionSession> CreateAsync(Repository repository, DecisionSession session);

    Task<DecisionSession> UpdateAsync(Repository repository, DecisionSession session);

    Task<DecisionSession?> GetAsync(Repository repository, DecisionSessionId sessionId);

    Task<DecisionSession?> GetActiveAsync(Repository repository);

    Task<IReadOnlyList<DecisionSession>> ListAsync(Repository repository);

    Task<DecisionSessionMetricsSnapshot?> ReadMetricsSnapshotAsync(Repository repository);

    Task WriteMetricsSnapshotAsync(Repository repository, DecisionSessionMetricsSnapshot snapshot);

    Task<DecisionSessionEconomicsSnapshot?> ReadEconomicsSnapshotAsync(Repository repository);

    Task WriteEconomicsSnapshotAsync(Repository repository, DecisionSessionEconomicsSnapshot snapshot);

    Task<DecisionSessionCoherenceSnapshot?> ReadCoherenceSnapshotAsync(Repository repository);

    Task WriteCoherenceSnapshotAsync(Repository repository, DecisionSessionCoherenceSnapshot snapshot);
}
