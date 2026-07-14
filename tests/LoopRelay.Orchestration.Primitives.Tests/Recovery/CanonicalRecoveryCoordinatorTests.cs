using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Runtime;
using Xunit;

namespace LoopRelay.Orchestration.Tests.Recovery;

public sealed class CanonicalRecoveryCoordinatorTests
{
    public static TheoryData<RecoveryDurableFacts, RecoveryBoundaryClassification> ClassificationCases => new()
    {
        { Facts() with { Corrupt = true }, RecoveryBoundaryClassification.Corrupt },
        { Facts() with { EvidenceComplete = false }, RecoveryBoundaryClassification.EvidenceIncomplete },
        { Facts() with { CompletionClosureStarted = true }, RecoveryBoundaryClassification.CompletionPartiallyClosed },
        { Facts() with { RequiredEffects = 3, SucceededEffects = 1 }, RecoveryBoundaryClassification.PartiallyEffected },
        { Facts() with { ExplicitCancellation = true, CancellationBoundary = RecoveryCancellationBoundary.BeforeDispatch }, RecoveryBoundaryClassification.Cancelled },
        { Facts() with { ExplicitFailure = true }, RecoveryBoundaryClassification.Failed },
        { Facts() with { RawOutputDurable = true }, RecoveryBoundaryClassification.SucceededUncommitted },
        { Facts() with { ProviderOutcomeUnknown = true }, RecoveryBoundaryClassification.ProviderUnknown },
        { Facts() with { OutwardAccepted = true }, RecoveryBoundaryClassification.AcceptedUnknown },
        { Facts() with { Authorized = true, ValidInFlightCorrelation = true }, RecoveryBoundaryClassification.InFlight },
        { Facts(), RecoveryBoundaryClassification.NotStarted },
    };

    [Theory]
    [MemberData(nameof(ClassificationCases))]
    public void Classifier_distinguishes_every_canonical_durable_boundary(
        RecoveryDurableFacts facts,
        RecoveryBoundaryClassification expected)
    {
        CanonicalRecoveryCase recoveryCase = Case(facts.Scope, facts.Subject);

        CanonicalRecoveryClassification result = CanonicalRecoveryClassifier.Classify(recoveryCase, facts);

        Assert.Equal(expected, result.Classification);
        Assert.Equal(facts.Evidence, result.SourceEvidence);
    }

    [Theory]
    [InlineData(RecoveryCancellationBoundary.BeforeDispatch, CanonicalRecoveryAction.RetryNewAttempt)]
    [InlineData(RecoveryCancellationBoundary.AfterOutwardAcceptance, CanonicalRecoveryAction.ReconcileProvider)]
    [InlineData(RecoveryCancellationBoundary.AfterValidatedOutput, CanonicalRecoveryAction.ReuseRawOutput)]
    [InlineData(RecoveryCancellationBoundary.DuringEffects, CanonicalRecoveryAction.ReconcileEffects)]
    [InlineData(RecoveryCancellationBoundary.DuringCompletionClosure, CanonicalRecoveryAction.ReconcileEffects)]
    public void Planner_applies_the_accepted_cancellation_salvage_ruling(
        RecoveryCancellationBoundary boundary,
        CanonicalRecoveryAction expected)
    {
        RecoveryDurableFacts facts = Facts() with
        {
            ExplicitCancellation = true,
            CancellationBoundary = boundary,
        };
        CanonicalRecoveryCase recoveryCase = Case(facts.Scope, facts.Subject);
        CanonicalRecoveryClassification classification = CanonicalRecoveryClassifier.Classify(recoveryCase, facts);

        CanonicalRecoveryPlan plan = CanonicalRecoveryPlanner.Plan(
            recoveryCase,
            classification,
            Authority(Enum.GetValues<CanonicalRecoveryAction>().ToHashSet()));

        Assert.Equal(expected, plan.Action);
        if (expected == CanonicalRecoveryAction.RetryNewAttempt)
            Assert.NotNull(plan.NewAttempt);
        else
            Assert.Null(plan.NewAttempt);
    }

    [Fact]
    public void Exact_profile_denial_selects_certified_reconstruction_and_never_resume_or_fork()
    {
        RecoveryDurableFacts facts = Facts(RecoveryScopeKind.WarmSession) with { ExplicitFailure = true };
        CanonicalRecoveryCase recoveryCase = Case(facts.Scope, facts.Subject);
        CanonicalRecoveryClassification classification = CanonicalRecoveryClassifier.Classify(recoveryCase, facts);
        RecoveryPlanningAuthority authority = Authority(new HashSet<CanonicalRecoveryAction>
            { CanonicalRecoveryAction.ResumeSession, CanonicalRecoveryAction.NativeFork,
              CanonicalRecoveryAction.ReconstructContext, CanonicalRecoveryAction.RequestHumanDecision }) with
        {
            ExactProfileSupported = false,
            CertifiedReconstructionAvailable = true,
        };

        CanonicalRecoveryPlan plan = CanonicalRecoveryPlanner.Plan(recoveryCase, classification, authority);

        Assert.Equal(CanonicalRecoveryAction.ReconstructContext, plan.Action);
    }

    [Fact]
    public async Task Coordinator_persists_immutable_classification_and_plan_before_runtime_action()
    {
        var store = new MemoryStore();
        var coordinator = new CanonicalRecoveryCoordinator(store);
        RecoveryDurableFacts facts = Facts() with { RawOutputDurable = true };
        CanonicalRecoveryCase recoveryCase = Case(facts.Scope, facts.Subject);
        await coordinator.OpenCaseAsync(recoveryCase, facts);
        CanonicalRecoveryPlan plan = await coordinator.PlanAsync(
            new RecoveryPlanRequest(recoveryCase.Identity, Authority(
                new HashSet<CanonicalRecoveryAction> { CanonicalRecoveryAction.ReuseRawOutput })));
        var executor = new RecordingExecutor(store, CanonicalRecoveryAction.ReuseRawOutput);
        var runtime = new CanonicalRecoveryRuntime(store, [executor]);

        CanonicalRecoveryActionEvent first = await runtime.ExecuteAsync(new RecoveryExecuteRequest(plan.Identity));
        CanonicalRecoveryActionEvent restarted = await runtime.ExecuteAsync(new RecoveryExecuteRequest(plan.Identity));

        Assert.Equal(RecoveryActionLifecycle.Succeeded, first.Lifecycle);
        Assert.Equal(first, restarted);
        Assert.Equal(1, executor.Calls);
        Assert.NotNull(await store.ReadPlanAsync(plan.Identity, CancellationToken.None));
    }

    [Theory]
    [InlineData(CanonicalRecoveryAction.ReconcileProvider)]
    [InlineData(CanonicalRecoveryAction.ReconcileEffects)]
    [InlineData(CanonicalRecoveryAction.ResumeSession)]
    [InlineData(CanonicalRecoveryAction.ReconstructContext)]
    [InlineData(CanonicalRecoveryAction.NativeFork)]
    [InlineData(CanonicalRecoveryAction.ReuseRawOutput)]
    [InlineData(CanonicalRecoveryAction.RetryNewAttempt)]
    [InlineData(CanonicalRecoveryAction.Compensate)]
    public async Task Restart_during_each_automatic_action_resumes_the_same_action_idempotently(
        CanonicalRecoveryAction action)
    {
        var store = new MemoryStore();
        RecoveryDurableFacts facts = Facts(RecoveryScopeKind.WarmSession) with { ExplicitFailure = true };
        CanonicalRecoveryCase recoveryCase = Case(facts.Scope, facts.Subject);
        CanonicalRecoveryClassification classification = CanonicalRecoveryClassifier.Classify(recoveryCase, facts);
        await store.AppendCaseAndClassificationAsync(recoveryCase, classification, CancellationToken.None);
        var plan = new CanonicalRecoveryPlan(
            RecoveryPlanIdentity.New(), recoveryCase.Identity, classification.Identity, action,
            "policy-1", "profile-1", ["fact:1"], ["precondition"], ["postcondition"],
            $"restart-{action}", action == CanonicalRecoveryAction.RetryNewAttempt ? AttemptIdentity.New() : null,
            DateTimeOffset.UtcNow);
        await store.AppendPlanAsync(plan, CancellationToken.None);
        var executor = new RecordingExecutor(store, action);
        RecoveryActionIdentity interruptedAction = RecoveryActionIdentity.New();
        await store.AppendActionEventAsync(new CanonicalRecoveryActionEvent(
            interruptedAction, plan.Identity, RecoveryActionLifecycle.Started,
            "process interrupted after start", ["fact:1"], DateTimeOffset.UtcNow), CancellationToken.None);

        CanonicalRecoveryActionEvent restarted = await new CanonicalRecoveryRuntime(store, [executor])
            .ExecuteAsync(new RecoveryExecuteRequest(plan.Identity));
        CanonicalRecoveryActionEvent duplicateRestart = await new CanonicalRecoveryRuntime(store, [executor])
            .ExecuteAsync(new RecoveryExecuteRequest(plan.Identity));

        Assert.Equal(interruptedAction, restarted.Identity);
        Assert.Equal(restarted, duplicateRestart);
        Assert.Equal(RecoveryActionLifecycle.Succeeded, restarted.Lifecycle);
        Assert.Equal(0, executor.Calls);
        Assert.Equal(1, executor.Reconciliations);
    }

    [Fact]
    public async Task Runtime_cannot_bypass_classification_and_persisted_planning()
    {
        var runtime = new CanonicalRecoveryRuntime(new MemoryStore(), []);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            runtime.ExecuteAsync(new RecoveryExecuteRequest(RecoveryPlanIdentity.New())));
    }

    [Fact]
    public async Task Cancellation_during_action_appends_nonfailure_terminal_evidence()
    {
        var store = new MemoryStore();
        var coordinator = new CanonicalRecoveryCoordinator(store);
        RecoveryDurableFacts facts = Facts() with { ExplicitFailure = true };
        CanonicalRecoveryCase recoveryCase = Case(facts.Scope, facts.Subject);
        await coordinator.OpenCaseAsync(recoveryCase, facts);
        CanonicalRecoveryPlan plan = await coordinator.PlanAsync(new RecoveryPlanRequest(
            recoveryCase.Identity,
            Authority(new HashSet<CanonicalRecoveryAction> { CanonicalRecoveryAction.RetryNewAttempt })));
        var runtime = new CanonicalRecoveryRuntime(
            store,
            [new CancellingExecutor(CanonicalRecoveryAction.RetryNewAttempt)]);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            runtime.ExecuteAsync(new RecoveryExecuteRequest(plan.Identity)));

        Assert.Contains(
            await store.ReadActionEventsAsync(plan.Identity, CancellationToken.None),
            item => item.Lifecycle == RecoveryActionLifecycle.Waiting);
    }

    private static CanonicalRecoveryCase Case(RecoveryScopeKind scope, RecoveryCausalSubject subject) =>
        new(RecoveryCaseIdentity.New(), scope, subject, DateTimeOffset.UtcNow);

    private static RecoveryDurableFacts Facts(RecoveryScopeKind scope = RecoveryScopeKind.Transition)
    {
        var causality = new CanonicalCausalContext(
            WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(), AttemptIdentity.New());
        return new RecoveryDurableFacts(
            scope, new RecoveryCausalSubject(causality), true, false, false, false,
            false, false, false, false, false, false, false, false,
            RecoveryCancellationBoundary.None, 0, 0, false, false, ["fact:1"]);
    }

    private static RecoveryPlanningAuthority Authority(IReadOnlySet<CanonicalRecoveryAction> allowed) =>
        new("policy-1", "codex-exact-1", true, false, true, allowed, ["policy:1", "profile:1"]);

    private sealed class RecordingExecutor(MemoryStore store, CanonicalRecoveryAction action)
        : ICanonicalRecoveryActionExecutor
    {
        public int Calls { get; private set; }
        public int Reconciliations { get; private set; }
        public CanonicalRecoveryAction Action => action;

        public async Task<CanonicalRecoveryActionResult> ExecuteAsync(
            CanonicalRecoveryPlan plan,
            CancellationToken cancellationToken)
        {
            Assert.NotNull(await store.ReadPlanAsync(plan.Identity, cancellationToken));
            Calls++;
            return new CanonicalRecoveryActionResult(
                RecoveryActionLifecycle.Succeeded, "recovered", ["receipt:1"]);
        }

        public Task<CanonicalRecoveryActionResult> ReconcileAsync(
            CanonicalRecoveryPlan plan,
            CanonicalRecoveryActionEvent previous,
            CancellationToken cancellationToken)
        {
            Reconciliations++;
            return Task.FromResult(new CanonicalRecoveryActionResult(
                RecoveryActionLifecycle.Succeeded, "independently reconciled", ["reconciliation:1"]));
        }
    }

    private sealed class CancellingExecutor(CanonicalRecoveryAction action) : ICanonicalRecoveryActionExecutor
    {
        public CanonicalRecoveryAction Action => action;
        public Task<CanonicalRecoveryActionResult> ExecuteAsync(
            CanonicalRecoveryPlan plan,
            CancellationToken cancellationToken) => throw new OperationCanceledException();
    }

    private sealed class MemoryStore : ICanonicalRecoveryStore
    {
        private readonly Dictionary<RecoveryCaseIdentity, CanonicalRecoveryCase> cases = [];
        private readonly List<CanonicalRecoveryClassification> classifications = [];
        private readonly List<CanonicalRecoveryPlan> plans = [];
        private readonly List<CanonicalRecoveryActionEvent> events = [];

        public Task<CanonicalRecoveryCase?> ReadCaseAsync(RecoveryCaseIdentity identity, CancellationToken cancellationToken) =>
            Task.FromResult(cases.GetValueOrDefault(identity));
        public Task<CanonicalRecoveryClassification?> ReadLatestClassificationAsync(RecoveryCaseIdentity identity, CancellationToken cancellationToken) =>
            Task.FromResult(classifications.LastOrDefault(item => item.Case == identity));
        public Task<CanonicalRecoveryPlan?> ReadPlanAsync(RecoveryPlanIdentity identity, CancellationToken cancellationToken) =>
            Task.FromResult(plans.SingleOrDefault(item => item.Identity == identity));
        public Task<IReadOnlyList<CanonicalRecoveryPlan>> ReadPlansAsync(RecoveryCaseIdentity recoveryCase, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CanonicalRecoveryPlan>>(plans.Where(item => item.Case == recoveryCase).ToArray());
        public Task<CanonicalRecoveryPlan?> ReadPlanByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(plans.SingleOrDefault(item => item.IdempotencyKey == idempotencyKey));
        public Task<IReadOnlyList<CanonicalRecoveryActionEvent>> ReadActionEventsAsync(RecoveryPlanIdentity plan, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CanonicalRecoveryActionEvent>>(events.Where(item => item.Plan == plan).ToArray());
        public Task AppendCaseAndClassificationAsync(CanonicalRecoveryCase recoveryCase, CanonicalRecoveryClassification classification, CancellationToken cancellationToken)
        {
            cases.Add(recoveryCase.Identity, recoveryCase);
            classifications.Add(classification);
            return Task.CompletedTask;
        }
        public Task AppendClassificationAsync(CanonicalRecoveryClassification classification, CancellationToken cancellationToken)
        {
            classifications.Add(classification);
            return Task.CompletedTask;
        }
        public Task AppendPlanAsync(CanonicalRecoveryPlan plan, CancellationToken cancellationToken)
        {
            plans.Add(plan);
            return Task.CompletedTask;
        }
        public Task AppendActionEventAsync(CanonicalRecoveryActionEvent actionEvent, CancellationToken cancellationToken)
        {
            events.Add(actionEvent);
            return Task.CompletedTask;
        }
    }
}
