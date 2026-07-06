using LoopRelay.Core.Repositories;
using LoopRelay.DecisionSessions.Models;

namespace LoopRelay.DecisionSessions.Abstractions;

public interface IDecisionSessionEvidenceReader
{
    Task<DecisionSessionEvidence> ReadAsync(Repository repository, DecisionSession? activeSession, DateTimeOffset measuredAt);
}
