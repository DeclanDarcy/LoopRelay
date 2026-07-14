using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using DurableEffectExecutor = LoopRelay.Orchestration.Effects.IEffectExecutor;

namespace LoopRelay.Orchestration.Tests.Runtime;

public sealed class TransitionEffectCoordinatorTests
{
    [Fact]
    public async Task CoordinatesDurablePlanAndSettlesOnlyAfterVerifiedReceipts()
    {
        Repository repository = Repository();
        EffectIntent first = Intent(order: 0);
        EffectIntent second = Intent(order: 1, causality: first.Causality, dependencies: [first.Identity]);
        var store = new CanonicalEffectWorkStore(repository);
        await store.AppendPlanAsync([first, second], CancellationToken.None);
        var settlement = new RecordingSettlement { Result = true };
        var executor = new RecordingExecutor();
        var coordinator = Coordinator(store, executor, new RecordingReconciler(), settlement);

        TransitionEffectCoordinationResult result = await coordinator.CoordinateAsync(
            first.Causality.TransitionRun, CancellationToken.None);

        Assert.Equal(RuntimeOutcomeKind.Completed, result.Outcome);
        Assert.False(result.RequiredEffectsPending);
        Assert.Equal([first.Identity, second.Identity], executor.Executed);
        Assert.Equal(1, settlement.Calls);
        Assert.All(await store.ReadPlanAsync(first.Causality.TransitionRun, CancellationToken.None),
            item => Assert.NotNull(item.Receipt));
    }

    [Fact]
    public async Task UnknownWorkReconcilesBeforeAnyRepeatDispatch()
    {
        Repository repository = Repository();
        EffectIntent intent = Intent(order: 0);
        var store = new CanonicalEffectWorkStore(repository);
        await store.AppendPlanAsync([intent], CancellationToken.None);
        var executor = new RecordingExecutor { Failure = new IOException("lost response") };
        var reconciler = new RecordingReconciler { Verdict = EffectReconciliationVerdict.Succeeded };
        var coordinator = Coordinator(store, executor, reconciler, new RecordingSettlement { Result = true });

        TransitionEffectCoordinationResult first = await coordinator.CoordinateAsync(intent.Causality.TransitionRun, CancellationToken.None);
        executor.Failure = null;
        TransitionEffectCoordinationResult restarted = await coordinator.CoordinateAsync(intent.Causality.TransitionRun, CancellationToken.None);

        Assert.Equal(RuntimeOutcomeKind.RecoveryRequired, first.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, restarted.Outcome);
        Assert.Equal(1, executor.Calls);
        Assert.Equal(1, reconciler.Calls);
    }

    [Fact]
    public async Task Independently_verified_not_applied_work_retries_in_same_coordination()
    {
        Repository repository = Repository();
        EffectIntent intent = Intent(order: 0);
        var store = new CanonicalEffectWorkStore(repository);
        await store.AppendPlanAsync([intent], CancellationToken.None);
        var executor = new RecordingExecutor { Failure = new IOException("lost response") };
        var reconciler = new RecordingReconciler { Verdict = EffectReconciliationVerdict.NotApplied };
        var settlement = new RecordingSettlement { Result = true };
        var coordinator = Coordinator(store, executor, reconciler, settlement);

        TransitionEffectCoordinationResult first = await coordinator.CoordinateAsync(
            intent.Causality.TransitionRun, CancellationToken.None);
        executor.Failure = null;
        TransitionEffectCoordinationResult restarted = await coordinator.CoordinateAsync(
            intent.Causality.TransitionRun, CancellationToken.None);

        Assert.Equal(RuntimeOutcomeKind.RecoveryRequired, first.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, restarted.Outcome);
        Assert.Equal(2, executor.Calls);
        Assert.Equal(1, reconciler.Calls);
    }

    [Fact]
    public async Task FailedEffectDoesNotExecuteItsDependent()
    {
        Repository repository = Repository();
        EffectIntent first = Intent(order: 0);
        EffectIntent second = Intent(order: 1, causality: first.Causality, dependencies: [first.Identity]);
        var store = new CanonicalEffectWorkStore(repository);
        await store.AppendPlanAsync([first, second], CancellationToken.None);
        var executor = new RecordingExecutor { State = EffectLifecycle.Failed };

        TransitionEffectCoordinationResult result = await Coordinator(
            store, executor, new RecordingReconciler(), new RecordingSettlement())
            .CoordinateAsync(first.Causality.TransitionRun, CancellationToken.None);

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.True(result.Failed);
        Assert.Single(executor.Executed);
    }

    [Fact]
    public async Task RestartAfterReceiptBeforeSettlementDoesNotRepeatOutwardWork()
    {
        Repository repository = Repository();
        EffectIntent intent = Intent(order: 0);
        var store = new CanonicalEffectWorkStore(repository);
        await store.AppendPlanAsync([intent], CancellationToken.None);
        var executor = new RecordingExecutor();
        var settlement = new RecordingSettlement { Result = false };
        var coordinator = Coordinator(store, executor, new RecordingReconciler(), settlement);

        TransitionEffectCoordinationResult first = await coordinator.CoordinateAsync(
            intent.Causality.TransitionRun, CancellationToken.None);
        settlement.Result = true;
        TransitionEffectCoordinationResult restarted = await coordinator.CoordinateAsync(
            intent.Causality.TransitionRun, CancellationToken.None);

        Assert.Equal(RuntimeOutcomeKind.EffectsPending, first.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, restarted.Outcome);
        Assert.Equal(1, executor.Calls);
        Assert.Equal(2, settlement.Calls);
    }

    private static TransitionEffectCoordinator Coordinator(
        IEffectWorkStore store,
        DurableEffectExecutor executor,
        IEffectReconciler reconciler,
        IEffectPlanSettlementStore settlement) =>
        new(store, new EffectWorker("coordinator-test", store, new EffectExecutorRegistry([executor]),
            reconciler, TimeSpan.FromMinutes(1)), settlement);

    private static EffectIntent Intent(
        int order,
        CanonicalCausalContext? causality = null,
        IReadOnlyList<EffectIntentIdentity>? dependencies = null) => new(
        EffectIntentIdentity.New(),
        causality ?? new CanonicalCausalContext(
            WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(), AttemptIdentity.New()),
        $"operation-{order}", new EffectExecutorKey("test"), "1",
        new EffectTargetDescriptor("test", $"effect-{order}", "{}"), "{}", new string('a', 64),
        order, dependencies ?? [], EffectRequiredness.BlockingLocal,
        new EffectCondition("always", "{}"), new EffectCondition("observed", "{}"),
        "reconcile", $"key-{order}-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

    private static Repository Repository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-effect-coordinator-").FullName;
        return new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
    }

    private sealed class RecordingExecutor : DurableEffectExecutor
    {
        public EffectExecutorKey Key => new("test");
        public string Version => "1";
        public int Calls { get; private set; }
        public List<EffectIntentIdentity> Executed { get; } = [];
        public EffectLifecycle State { get; set; } = EffectLifecycle.Succeeded;
        public Exception? Failure { get; set; }
        public Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken)
        {
            Calls++;
            Executed.Add(intent.Identity);
            if (Failure is not null) throw Failure;
            return Task.FromResult(new EffectExecutionObservation(
                State, State.ToString(), [intent.Identity.Value], "before", "after", State == EffectLifecycle.Succeeded));
        }
    }

    private sealed class RecordingReconciler : IEffectReconciler
    {
        public int Calls { get; private set; }
        public EffectReconciliationVerdict Verdict { get; set; } = EffectReconciliationVerdict.StillUnknown;
        public Task<EffectReconciliationObservation> ReconcileAsync(EffectIntent intent, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new EffectReconciliationObservation(
                Verdict, Verdict.ToString(), ["independent"], "before", "after"));
        }
    }

    private sealed class RecordingSettlement : IEffectPlanSettlementStore
    {
        public int Calls { get; private set; }
        public bool Result { get; set; }
        public List<RuntimeOutcomeKind> Outcomes { get; } = [];
        public Task<bool> TrySettleAsync(TransitionRunIdentity transitionRun, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }

        public Task RecordOutcomeAsync(
            TransitionRunIdentity transitionRun,
            RuntimeOutcomeKind outcome,
            string explanation,
            CancellationToken cancellationToken)
        {
            Outcomes.Add(outcome);
            return Task.CompletedTask;
        }
    }
}
