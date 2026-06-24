using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionTransferService
{
    Task<DecisionSessionTransferResult> ExecuteAsync(Guid repositoryId);

    Task<IReadOnlyList<DecisionSessionTransfer>> ListAsync(Guid repositoryId);

    Task<DecisionSessionTransferDiagnostics> GetDiagnosticsAsync(Guid repositoryId);
}
