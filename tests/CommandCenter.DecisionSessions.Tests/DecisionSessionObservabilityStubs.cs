using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Tests;

// Phase 3 (refactor-lazy-sqlite.md) retarget support: DecisionSessionObservabilityService.GetProjectionAsync
// no longer reads pre-warmed snapshot FILES — it ACTIVELY computes via the analysis providers. These stubs let
// the observability tests feed the SAME fixture snapshots through the provider seam they previously fed through
// the file seam, so each original invariant (projection composes snapshots / history reconstructs events /
// influence emits signals / health reports dimensions) is proven unchanged against the new read path.
// A null snapshot (or a thrown exception) reproduces the old "snapshot unavailable" null-on-read behaviour.

internal sealed class StubMetricsService(DecisionSessionMetricsSnapshot? snapshot, Exception? failure = null)
    : IDecisionSessionMetricsService
{
    public Task<DecisionSessionMetricsSnapshot> GetMetricsAsync(Guid repositoryId) =>
        failure is not null
            ? Task.FromException<DecisionSessionMetricsSnapshot>(failure)
            : snapshot is not null
                ? Task.FromResult(snapshot)
                : throw new KeyNotFoundException("metrics unavailable");
}

internal sealed class StubEconomicsService(DecisionSessionEconomicsSnapshot? snapshot, Exception? failure = null)
    : IDecisionSessionEconomicsService
{
    public Task<DecisionSessionEconomicsSnapshot> GetEconomicsAsync(Guid repositoryId) =>
        failure is not null
            ? Task.FromException<DecisionSessionEconomicsSnapshot>(failure)
            : snapshot is not null
                ? Task.FromResult(snapshot)
                : throw new KeyNotFoundException("economics unavailable");
}

internal sealed class StubCoherenceService(DecisionSessionCoherenceSnapshot? snapshot, Exception? failure = null)
    : IDecisionSessionCoherenceService
{
    public Task<DecisionSessionCoherenceSnapshot> GetCoherenceAsync(Guid repositoryId) =>
        failure is not null
            ? Task.FromException<DecisionSessionCoherenceSnapshot>(failure)
            : snapshot is not null
                ? Task.FromResult(snapshot)
                : throw new KeyNotFoundException("coherence unavailable");
}

internal sealed class StubLifecyclePolicy(DecisionSessionLifecycleSnapshot? snapshot, Exception? failure = null)
    : IDecisionSessionLifecyclePolicy
{
    public Task<DecisionSessionLifecycleSnapshot> EvaluateAsync(Guid repositoryId) =>
        failure is not null
            ? Task.FromException<DecisionSessionLifecycleSnapshot>(failure)
            : snapshot is not null
                ? Task.FromResult(snapshot)
                : throw new KeyNotFoundException("policy unavailable");
}

internal sealed class StubTransferEligibilityService(DecisionSessionTransferEligibilitySnapshot? snapshot, Exception? failure = null)
    : IDecisionSessionTransferEligibilityService
{
    public Task<DecisionSessionTransferEligibilitySnapshot> CheckAsync(Guid repositoryId) =>
        failure is not null
            ? Task.FromException<DecisionSessionTransferEligibilitySnapshot>(failure)
            : snapshot is not null
                ? Task.FromResult(snapshot)
                : throw new KeyNotFoundException("eligibility unavailable");
}
