using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Cli.Tests.Services.Decisions;

/// <summary>
/// Pass-through recovery store that records the decision-turn compare-and-swap sequence in the order the
/// store actually saw it. Turn-progress durability is an ordering property, so the order the writes reach
/// the store — not just the row they leave behind — is the thing under test.
/// </summary>
internal sealed class RecordingRecoveryStore(IRecoveryStore _inner) : IRecoveryStore
{
    public List<(DecisionSessionTurnRecord Expected, DecisionSessionTurnRecord Updated)> DecisionTurnWrites { get; } =
        new();

    /// <summary>
    /// Makes each decision-turn compare-and-swap observably slow. Microsoft.Data.Sqlite completes a real
    /// write inline on the calling thread, which hides the difference between an advance that is durable on
    /// return and one that is merely in flight — both look identical to the next statement. This restores
    /// the distinction so tests can assert on durable state rather than on elapsed time.
    /// </summary>
    public TimeSpan WriteLatency { get; set; } = TimeSpan.Zero;

    public async Task<RecoveryStoreWriteResult> CompareAndSwapDecisionTurnAsync(
        DecisionSessionTurnRecord expected,
        DecisionSessionTurnRecord updated,
        CancellationToken cancellationToken = default)
    {
        if (WriteLatency > TimeSpan.Zero)
        {
            await Task.Delay(WriteLatency, cancellationToken).ConfigureAwait(false);
        }

        lock (DecisionTurnWrites)
        {
            DecisionTurnWrites.Add((expected, updated));
        }

        return await _inner.CompareAndSwapDecisionTurnAsync(expected, updated, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<DecisionContinuityStatusSnapshot> ReadStatusAsync(CancellationToken cancellationToken = default) =>
        _inner.ReadStatusAsync(cancellationToken);

    public Task<ActiveStateReadResult> ReadActiveAsync(string scopeId, CancellationToken cancellationToken = default) =>
        _inner.ReadActiveAsync(scopeId, cancellationToken);

    public Task<RecoveryStoreWriteResult> CreateScopeAndActivateAsync(
        DecisionSessionScopeRecord scope,
        DecisionSessionLineageNode lineage,
        DecisionSessionActiveState active,
        SessionContinuityProfile profile,
        CancellationToken cancellationToken = default) =>
        _inner.CreateScopeAndActivateAsync(scope, lineage, active, profile, cancellationToken);

    public Task<RecoveryStoreWriteResult> BeginAttemptAsync(
        RecoveryAttempt attempt,
        long expectedActiveRowVersion,
        SessionContinuityProfile profile,
        CancellationToken cancellationToken = default) =>
        _inner.BeginAttemptAsync(attempt, expectedActiveRowVersion, profile, cancellationToken);

    public Task<RecoveryStoreWriteResult> UpdateActiveAccountingAsync(
        DecisionSessionActiveState expected,
        DecisionSessionActiveState updated,
        CancellationToken cancellationToken = default) =>
        _inner.UpdateActiveAccountingAsync(expected, updated, cancellationToken);

    public Task<RecoveryStoreWriteResult> RecordPlannedSuccessorAsync(
        DecisionSessionActiveState expectedActive,
        DecisionSessionLineageNode successor,
        CancellationToken cancellationToken = default) =>
        _inner.RecordPlannedSuccessorAsync(expectedActive, successor, cancellationToken);

    public Task<RecoveryStoreWriteResult> CompareAndSwapAttemptAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        CancellationToken cancellationToken = default) =>
        _inner.CompareAndSwapAttemptAsync(expected, updated, cancellationToken);

    public Task<RecoveryStoreWriteResult> RecordPlanAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        RecoveryPlan plan,
        CancellationToken cancellationToken = default) =>
        _inner.RecordPlanAsync(expected, updated, plan, cancellationToken);

    public Task<RecoveryStoreWriteResult> RecordReplacementAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        DecisionSessionLineageNode replacement,
        CancellationToken cancellationToken = default) =>
        _inner.RecordReplacementAsync(expected, updated, replacement, cancellationToken);

    public Task<RecoveryStoreWriteResult> CompleteRecoveryAndActivateAsync(
        RecoveryAttempt expected,
        RecoveryAttempt completed,
        DecisionSessionActiveState expectedActive,
        DecisionSessionLineageNode replacement,
        CancellationToken cancellationToken = default) =>
        _inner.CompleteRecoveryAndActivateAsync(expected, completed, expectedActive, replacement, cancellationToken);

    public Task<RecoveryAttempt?> ReadAttemptAsync(string attemptId, CancellationToken cancellationToken = default) =>
        _inner.ReadAttemptAsync(attemptId, cancellationToken);

    public Task<RecoveryPlan?> ReadPlanAsync(string planDigest, CancellationToken cancellationToken = default) =>
        _inner.ReadPlanAsync(planDigest, cancellationToken);

    public Task<DecisionSessionLineageNode?> ReadLineageAsync(string lineageId, CancellationToken cancellationToken = default) =>
        _inner.ReadLineageAsync(lineageId, cancellationToken);

    public Task<RecoveryAttempt?> ReadNonterminalAttemptAsync(string scopeId, CancellationToken cancellationToken = default) =>
        _inner.ReadNonterminalAttemptAsync(scopeId, cancellationToken);

    public Task<RecoveryAttempt?> ReadLatestAttemptAsync(string scopeId, CancellationToken cancellationToken = default) =>
        _inner.ReadLatestAttemptAsync(scopeId, cancellationToken);

    public Task<RecoveryStoreWriteResult> RetireScopeAsync(
        string scopeId,
        long expectedActiveRowVersion,
        CancellationToken cancellationToken = default) =>
        _inner.RetireScopeAsync(scopeId, expectedActiveRowVersion, cancellationToken);

    public Task<DecisionSessionTurnRecord?> ReadDecisionTurnAsync(
        string transitionRunId,
        string inputSnapshotHash,
        CancellationToken cancellationToken = default) =>
        _inner.ReadDecisionTurnAsync(transitionRunId, inputSnapshotHash, cancellationToken);

    public Task<RecoveryStoreWriteResult> BeginDecisionTurnAsync(
        DecisionSessionTurnRecord turn,
        CancellationToken cancellationToken = default) =>
        _inner.BeginDecisionTurnAsync(turn, cancellationToken);

    public Task<DecisionTurnCommitResult> CommitDecisionOutputAsync(
        DecisionSessionTurnRecord expectedTurn,
        DecisionSessionActiveState expectedActive,
        DecisionSessionAccounting accounting,
        string output,
        string policyDigest,
        CancellationToken cancellationToken = default) =>
        _inner.CommitDecisionOutputAsync(
            expectedTurn, expectedActive, accounting, output, policyDigest, cancellationToken);

    public Task<RecoveryStoreWriteResult> MarkDecisionArtifactMaterializedAsync(
        DecisionSessionTurnRecord expected,
        CancellationToken cancellationToken = default) =>
        _inner.MarkDecisionArtifactMaterializedAsync(expected, cancellationToken);
}
