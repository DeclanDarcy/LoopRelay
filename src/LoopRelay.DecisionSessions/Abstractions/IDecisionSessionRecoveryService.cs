using LoopRelay.DecisionSessions.Models;

namespace LoopRelay.DecisionSessions.Abstractions;

public interface IDecisionSessionRecoveryService
{
    Task<DecisionSessionDiagnostics> GetDiagnosticsAsync(Guid repositoryId);

    Task<DecisionSessionRecoveryResult> RecoverAsync(Guid repositoryId);

    Task<DecisionSessionRecoveryResult> GetRecoveryAsync(Guid repositoryId);

    Task<DecisionSessionRecoveryHistory> GetHistoryAsync(Guid repositoryId);

    Task<DecisionSessionRecoveryDiagnostics> GetRecoveryDiagnosticsAsync(Guid repositoryId);
}
