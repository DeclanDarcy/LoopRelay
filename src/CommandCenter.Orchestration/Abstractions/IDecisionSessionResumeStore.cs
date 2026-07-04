using CommandCenter.Orchestration.Models;

namespace CommandCenter.Orchestration.Abstractions;

/// <summary>Persistence seam for the decision session's cross-run resume state (per repo, per epic).</summary>
public interface IDecisionSessionResumeStore
{
    /// <summary>Null when absent or unusable (an unusable file is deleted).</summary>
    Task<DecisionSessionResumeState?> ReadAsync(CancellationToken cancellationToken = default);

    Task WriteAsync(DecisionSessionResumeState state, CancellationToken cancellationToken = default);

    /// <summary>Idempotent delete.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
