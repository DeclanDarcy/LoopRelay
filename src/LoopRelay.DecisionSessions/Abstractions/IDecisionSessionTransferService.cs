using LoopRelay.DecisionSessions.Models;

namespace LoopRelay.DecisionSessions.Abstractions;

public interface IDecisionSessionTransferService
{
    Task<DecisionSessionTransferResult> ExecuteAsync(Guid repositoryId);

    Task<IReadOnlyList<DecisionSessionTransfer>> ListAsync(Guid repositoryId);

    Task<DecisionSessionTransferDiagnostics> GetDiagnosticsAsync(Guid repositoryId);
}
