using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Runtime;

public sealed class TransitionRecoveryClassifierTests
{
    public static TheoryData<TransitionBoundaryKind> EveryBoundary => new(
        Enum.GetValues<TransitionBoundaryKind>());

    [Theory]
    [MemberData(nameof(EveryBoundary))]
    public void Every_transition_and_prompt_boundary_projects_to_the_canonical_taxonomy(
        TransitionBoundaryKind boundary)
    {
        bool raw = boundary is TransitionBoundaryKind.ProviderCompleted or
            TransitionBoundaryKind.RawOutputPersisted or TransitionBoundaryKind.OutputInterpreted or
            TransitionBoundaryKind.OutputValidated or TransitionBoundaryKind.BeforeEffects;
        bool partialEffect = boundary == TransitionBoundaryKind.DuringEffects;
        TransitionDurableState state = partialEffect
            ? TransitionDurableState.EffectsPartiallyApplied
            : raw
                ? TransitionDurableState.PromptCompleted
                : boundary is >= TransitionBoundaryKind.RequestWriteStarted and <= TransitionBoundaryKind.ProviderTerminal
                    ? TransitionDurableState.ProviderOutcomeUnknown
                    : TransitionDurableState.Started;
        TransitionRunRecoverySnapshot snapshot = Snapshot(
            state,
            raw ? new PromptExecutionResult(PromptExecutionStatus.Completed, "output", TimeSpan.Zero, new Dictionary<string, string>()) : null,
            [Boundary(boundary)]);
        RecoveryDurableFacts facts = TransitionRecoveryFactProjector.Project(snapshot);
        CanonicalRecoveryClassification classification = CanonicalRecoveryClassifier.Classify(
            new CanonicalRecoveryCase(RecoveryCaseIdentity.New(), facts.Scope, facts.Subject, DateTimeOffset.UtcNow),
            facts);

        Assert.Contains($"boundary:{boundary}", classification.SourceEvidence);
        Assert.Equal(
            partialEffect
                ? RecoveryBoundaryClassification.PartiallyEffected
                : raw
                    ? RecoveryBoundaryClassification.SucceededUncommitted
                    : state == TransitionDurableState.ProviderOutcomeUnknown
                        ? RecoveryBoundaryClassification.ProviderUnknown
                        : RecoveryBoundaryClassification.InFlight,
            classification.Classification);
    }

    [Fact]
    public void PreSubmissionCancellationRemainsDistinctAndThePlannerOwnsAnyLaterRetry()
    {
        TransitionRecoveryDecision decision = TransitionRecoveryClassifier.Classify(
            Snapshot(TransitionDurableState.Cancelled, boundaries: [Boundary(TransitionBoundaryKind.PreSubmission)]));

        Assert.Equal(TransitionRecoveryDisposition.Cancelled, decision.Disposition);
        Assert.False(decision.MaySubmitProviderTurn);
    }

    [Fact]
    public void AcceptedRequestRequiresReconciliationAndForbidsResubmission()
    {
        TransitionRecoveryDecision decision = TransitionRecoveryClassifier.Classify(
            Snapshot(TransitionDurableState.ProviderOutcomeUnknown, boundaries:
            [
                Boundary(TransitionBoundaryKind.RequestSubmitted),
                Boundary(TransitionBoundaryKind.RequestAccepted),
            ]));

        Assert.Equal(TransitionRecoveryDisposition.ReconcileProvider, decision.Disposition);
        Assert.False(decision.MaySubmitProviderTurn);
        Assert.False(decision.MayApplyEffects);
    }

    [Fact]
    public void RequestWriteStartedRequiresReconciliationBecausePartialSubmissionIsUnknown()
    {
        TransitionRecoveryDecision decision = TransitionRecoveryClassifier.Classify(
            Snapshot(TransitionDurableState.ProviderOutcomeUnknown,
                boundaries: [Boundary(TransitionBoundaryKind.RequestWriteStarted)]));

        Assert.Equal(TransitionRecoveryDisposition.ReconcileProvider, decision.Disposition);
        Assert.False(decision.MaySubmitProviderTurn);
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
            Causality,
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
        new(Causality, new WorkflowTransitionIdentity("Transition"), kind, 1,
            DateTimeOffset.UtcNow, "input-hash", null, []);

    private static CanonicalCausalContext Causality { get; } = new(
        new WorkspaceIdentity("ws_test"),
        new RunIdentity("run_test"),
        new WorkflowInstanceIdentity("wfi_test"),
        new TransitionRunIdentity("tr_test"),
        new AttemptIdentity("att_test"));
}
