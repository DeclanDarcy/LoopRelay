using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Runtime;

public sealed class TransitionRecoveryClassifierTests
{
    [Fact]
    public void PreSubmissionCancellationIsSafeToRetry()
    {
        TransitionRecoveryDecision decision = TransitionRecoveryClassifier.Classify(
            Snapshot(TransitionDurableState.Cancelled, boundaries: [Boundary(TransitionBoundaryKind.PreSubmission)]));

        Assert.Equal(TransitionRecoveryDisposition.SafeRetry, decision.Disposition);
        Assert.True(decision.MaySubmitProviderTurn);
    }

    [Fact]
    public void AcceptedRequestRequiresReconciliationAndForbidsResubmission()
    {
        TransitionRecoveryDecision decision = TransitionRecoveryClassifier.Classify(
            Snapshot(TransitionDurableState.Blocked, boundaries:
            [
                Boundary(TransitionBoundaryKind.RequestSubmitted),
                Boundary(TransitionBoundaryKind.RequestAccepted),
            ]));

        Assert.Equal(TransitionRecoveryDisposition.ReconcileProvider, decision.Disposition);
        Assert.False(decision.MaySubmitProviderTurn);
        Assert.False(decision.MayApplyEffects);
    }

    [Fact]
    public void CommittedRawOutputSkipsProviderAndMayContinueDeterministically()
    {
        TransitionRecoveryDecision decision = TransitionRecoveryClassifier.Classify(
            Snapshot(
                TransitionDurableState.PromptCompleted,
                raw: new PromptExecutionResult(
                    PromptExecutionStatus.Completed, "output", TimeSpan.Zero,
                    new Dictionary<string, string>()),
                boundaries: [Boundary(TransitionBoundaryKind.ProviderCompleted)]));

        Assert.Equal(TransitionRecoveryDisposition.MaterializeCommittedOutput, decision.Disposition);
        Assert.False(decision.MaySubmitProviderTurn);
        Assert.True(decision.MayApplyEffects);
    }

    [Fact]
    public void PartialEffectFailsClosed()
    {
        TransitionRecoveryDecision decision = TransitionRecoveryClassifier.Classify(
            Snapshot(TransitionDurableState.EffectsPartiallyApplied,
                boundaries: [Boundary(TransitionBoundaryKind.DuringEffects)]));

        Assert.Equal(TransitionRecoveryDisposition.FailClosedUnknownSideEffect, decision.Disposition);
        Assert.False(decision.MaySubmitProviderTurn);
        Assert.False(decision.MayApplyEffects);
    }

    [Fact]
    public void CompletedRunPerformsNoFurtherWork()
    {
        TransitionRecoveryDecision decision = TransitionRecoveryClassifier.Classify(
            Snapshot(TransitionDurableState.Completed,
                boundaries: [Boundary(TransitionBoundaryKind.CompletionPersisted)]));

        Assert.Equal(TransitionRecoveryDisposition.ReuseCompleted, decision.Disposition);
        Assert.False(decision.MaySubmitProviderTurn);
        Assert.False(decision.MayApplyEffects);
    }

    private static TransitionRunRecoverySnapshot Snapshot(
        TransitionDurableState state,
        PromptExecutionResult? raw = null,
        IReadOnlyList<TransitionBoundaryObservation>? boundaries = null) =>
        new(
            "run-1",
            new WorkflowTransitionIdentity("Transition"),
            state,
            state == TransitionDurableState.Completed ? RuntimeOutcomeKind.Completed : RuntimeOutcomeKind.Waiting,
            "input-hash",
            raw,
            [],
            boundaries ?? [],
            state.ToString(),
            []);

    private static TransitionBoundaryObservation Boundary(TransitionBoundaryKind kind) =>
        new("run-1", new WorkflowTransitionIdentity("Transition"), kind, 1,
            DateTimeOffset.UtcNow, "input-hash", null, []);
}
