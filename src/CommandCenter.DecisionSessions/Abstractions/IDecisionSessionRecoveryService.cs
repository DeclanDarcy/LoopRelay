using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionRecoveryService
{
    Task<DecisionSessionDiagnostics> GetDiagnosticsAsync(Guid repositoryId);
}
