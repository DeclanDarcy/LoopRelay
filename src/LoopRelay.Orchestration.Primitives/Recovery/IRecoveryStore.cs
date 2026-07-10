using LoopRelay.Agents.Models.Sessions;

namespace LoopRelay.Orchestration.Recovery;

public interface IRecoveryStore
{
    Task<DecisionContinuityStatusSnapshot> ReadStatusAsync(CancellationToken cancellationToken = default);
    Task<ActiveStateReadResult> ReadActiveAsync(string scopeId, CancellationToken cancellationToken = default);
    Task<RecoveryStoreWriteResult> CreateScopeAndActivateAsync(
        DecisionSessionScopeRecord scope,
        DecisionSessionLineageNode lineage,
        DecisionSessionActiveState active,
        SessionContinuityProfile profile,
        CancellationToken cancellationToken = default);
    Task<RecoveryStoreWriteResult> BeginAttemptAsync(
        RecoveryAttempt attempt,
        long expectedActiveRowVersion,
        SessionContinuityProfile profile,
        CancellationToken cancellationToken = default);
    Task<RecoveryStoreWriteResult> UpdateActiveAccountingAsync(
        DecisionSessionActiveState expected,
        DecisionSessionActiveState updated,
        CancellationToken cancellationToken = default);
    Task<RecoveryStoreWriteResult> RecordPlannedSuccessorAsync(
        DecisionSessionActiveState expectedActive,
        DecisionSessionLineageNode successor,
        CancellationToken cancellationToken = default);
    Task<RecoveryStoreWriteResult> CompareAndSwapAttemptAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        CancellationToken cancellationToken = default);
    Task<RecoveryStoreWriteResult> RecordPlanAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        RecoveryPlan plan,
        CancellationToken cancellationToken = default);
    Task<RecoveryStoreWriteResult> RecordReplacementAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        DecisionSessionLineageNode replacement,
        CancellationToken cancellationToken = default);
    Task<RecoveryStoreWriteResult> CompleteRecoveryAndActivateAsync(
        RecoveryAttempt expected,
        RecoveryAttempt completed,
        DecisionSessionActiveState expectedActive,
        DecisionSessionLineageNode replacement,
        CancellationToken cancellationToken = default);
    Task<RecoveryAttempt?> ReadAttemptAsync(string attemptId, CancellationToken cancellationToken = default);
    Task<RecoveryPlan?> ReadPlanAsync(string planDigest, CancellationToken cancellationToken = default);
    Task<DecisionSessionLineageNode?> ReadLineageAsync(string lineageId, CancellationToken cancellationToken = default);
    Task<RecoveryAttempt?> ReadNonterminalAttemptAsync(string scopeId, CancellationToken cancellationToken = default);
    Task<RecoveryAttempt?> ReadLatestAttemptAsync(string scopeId, CancellationToken cancellationToken = default);
    Task<RecoveryStoreWriteResult> RetireScopeAsync(string scopeId, long expectedActiveRowVersion, CancellationToken cancellationToken = default);
    Task<DecisionSessionTurnRecord?> ReadDecisionTurnAsync(
        string transitionRunId,
        string inputSnapshotHash,
        CancellationToken cancellationToken = default);
    Task<RecoveryStoreWriteResult> BeginDecisionTurnAsync(
        DecisionSessionTurnRecord turn,
        CancellationToken cancellationToken = default);
    Task<RecoveryStoreWriteResult> CompareAndSwapDecisionTurnAsync(
        DecisionSessionTurnRecord expected,
        DecisionSessionTurnRecord updated,
        CancellationToken cancellationToken = default);
    Task<DecisionTurnCommitResult> CommitDecisionOutputAsync(
        DecisionSessionTurnRecord expectedTurn,
        DecisionSessionActiveState expectedActive,
        DecisionSessionAccounting accounting,
        string output,
        string policyDigest,
        CancellationToken cancellationToken = default);
    Task<RecoveryStoreWriteResult> MarkDecisionArtifactMaterializedAsync(
        DecisionSessionTurnRecord expected,
        CancellationToken cancellationToken = default);
}
