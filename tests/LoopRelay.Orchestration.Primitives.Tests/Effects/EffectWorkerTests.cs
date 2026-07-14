using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Orchestration.Tests.Effects;

public sealed class EffectWorkerTests
{
    [Fact]
    public async Task UnknownMutationIsReconciledWithoutASecondExecution()
    {
        var store = new MemoryEffectWorkStore(Intent(order: 0));
        var executor = new ScriptedExecutor { Failure = new IOException("receipt connection lost") };
        var reconciler = new ScriptedReconciler(EffectReconciliationVerdict.Succeeded);
        var worker = Worker(store, executor, reconciler);

        EffectWorkerResult first = await worker.RunOnceAsync();
        EffectWorkItem unknown = (await store.ReadAsync(store.OnlyIdentity, CancellationToken.None))!;
        Assert.Equal(EffectLifecycle.Unknown, unknown.State);
        Assert.Equal(1, first.RecoveryRequired);

        executor.Failure = null;
        EffectWorkerResult second = await worker.RunOnceAsync();
        EffectWorkItem settled = (await store.ReadAsync(store.OnlyIdentity, CancellationToken.None))!;

        Assert.Equal(1, executor.Calls);
        Assert.Equal(1, reconciler.Calls);
        Assert.Equal(EffectLifecycle.Succeeded, settled.State);
        Assert.NotNull(settled.Receipt);
        Assert.True(settled.Receipt.PostconditionSatisfied);
        Assert.Equal(1, second.Succeeded);
    }

    [Fact]
    public async Task DependencyOrderPreventsDependentOutwardWorkUntilReceiptExists()
    {
        EffectIntent first = Intent(order: 0);
        EffectIntent second = Intent(order: 1, dependencies: [first.Identity]);
        var store = new MemoryEffectWorkStore(first, second);
        var executor = new ScriptedExecutor();
        var worker = Worker(store, executor, new ScriptedReconciler(EffectReconciliationVerdict.StillUnknown));

        EffectWorkerResult result = await worker.RunOnceAsync();

        Assert.Equal(2, result.Succeeded);
        Assert.Equal([first.Identity, second.Identity], executor.Executed);
        Assert.All(store.Items, item => Assert.NotNull(item.Receipt));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task Dynamically_appended_ordered_child_blocks_preplanned_downstream_effect_until_subsequent_scan(
        int downstreamOrder)
    {
        CanonicalCausalContext causality = new(
            WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(), AttemptIdentity.New());
        EffectIntent parent = Intent(order: 0, causality: causality);
        EffectIntent downstream = Intent(order: downstreamOrder, dependencies: [parent.Identity], causality: causality);
        var store = new MemoryEffectWorkStore(parent, downstream);
        EffectIntent? child = null;
        var executor = new ScriptedExecutor
        {
            OnExecute = intent =>
            {
                if (intent.Identity == parent.Identity)
                {
                    child = Intent(order: 1, dependencies: [parent.Identity], causality: causality);
                    store.Append(child);
                }
            },
        };
        var worker = Worker(store, executor, new ScriptedReconciler(EffectReconciliationVerdict.StillUnknown));

        await worker.RunOnceAsync();

        Assert.Equal([parent.Identity], executor.Executed);
        Assert.Equal(EffectLifecycle.Planned,
            (await store.ReadAsync(downstream.Identity, CancellationToken.None))!.State);
        Assert.Equal(EffectLifecycle.Planned,
            (await store.ReadAsync(child!.Identity, CancellationToken.None))!.State);

        await worker.RunOnceAsync();
        await worker.RunOnceAsync();

        Assert.Equal([parent.Identity, child.Identity, downstream.Identity], executor.Executed);
    }

    [Fact]
    public async Task PendingRequiredAsyncEffectRemainsDiscoverableAcrossRuns()
    {
        EffectIntent push = Intent(order: 0, requiredness: EffectRequiredness.RequiredAsync);
        var store = new MemoryEffectWorkStore(push);
        var executor = new ScriptedExecutor
        {
            Observation = new EffectExecutionObservation(
                EffectLifecycle.Pending,
                "Remote is unavailable.",
                ["remote:origin"],
                "ref:before",
                "ref:before",
                false),
        };
        var worker = Worker(store, executor, new ScriptedReconciler(EffectReconciliationVerdict.StillUnknown));

        EffectWorkerResult first = await worker.RunOnceAsync();
        EffectWorkerResult second = await worker.RunOnceAsync();

        Assert.Equal(2, executor.Calls);
        Assert.Equal(1, first.Pending);
        Assert.Equal(1, second.Pending);
        Assert.Equal(EffectLifecycle.Pending, store.Items.Single().State);
        Assert.Null(store.Items.Single().Receipt);
    }

    [Fact]
    public async Task CompareAndSetLeaseAllowsOnlyOneWorker()
    {
        var store = new MemoryEffectWorkStore(Intent(order: 0));
        EffectWorkItem snapshot = store.Items.Single();

        EffectLease? first = await store.TryLeaseAsync(
            snapshot.Intent.Identity, snapshot.RowVersion, "worker-a", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1), CancellationToken.None);
        EffectLease? duplicate = await store.TryLeaseAsync(
            snapshot.Intent.Identity, snapshot.RowVersion, "worker-b", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(duplicate);
    }

    [Theory]
    [InlineData(EffectLifecycle.Unknown, EffectLifecycle.Planned)]
    [InlineData(EffectLifecycle.Failed, EffectLifecycle.Leased)]
    [InlineData(EffectLifecycle.Stalled, EffectLifecycle.Leased)]
    [InlineData(EffectLifecycle.Cancelled, EffectLifecycle.Leased)]
    [InlineData(EffectLifecycle.Succeeded, EffectLifecycle.Leased)]
    public void UncertainOrTerminalWorkCannotBeBlindlyReexecuted(
        EffectLifecycle current,
        EffectLifecycle next)
    {
        Assert.False(EffectLifecyclePolicy.CanTransition(current, next));
        Assert.Throws<InvalidOperationException>(() => EffectLifecyclePolicy.RequireTransition(current, next));
    }

    [Fact]
    public async Task CallerCancellationDuringOutwardCallPersistsUnknownWithEvidenceToken()
    {
        var store = new MemoryEffectWorkStore(Intent(order: 0));
        var executor = new ScriptedExecutor { Failure = new OperationCanceledException("cancelled in transport") };
        var recovery = new RecordingRecoveryCases();
        var worker = Worker(
            store,
            executor,
            new ScriptedReconciler(EffectReconciliationVerdict.StillUnknown),
            recovery);

        EffectWorkerResult result = await worker.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.RecoveryRequired);
        Assert.Equal(EffectLifecycle.Unknown, store.Items.Single().State);
        Assert.Contains(store.Items.Single().Events, item =>
            item.State == EffectLifecycle.Unknown && item.Evidence.Contains(nameof(OperationCanceledException)));
        Assert.Equal(
            RecoveryBoundaryClassification.AcceptedUnknown,
            Assert.Single(recovery.Classifications).Classification);
    }

    [Fact]
    public async Task CancellationBeforeSchedulingLeavesDurablePlanUnstarted()
    {
        var store = new MemoryEffectWorkStore(Intent(order: 0));
        var executor = new ScriptedExecutor();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Worker(store, executor, new ScriptedReconciler(EffectReconciliationVerdict.StillUnknown))
                .RunOnceAsync(cancellation.Token));

        Assert.Equal(0, executor.Calls);
        Assert.Equal(EffectLifecycle.Planned, store.Items.Single().State);
    }

    private static EffectWorker Worker(
        IEffectWorkStore store,
        IEffectExecutor executor,
        IEffectReconciler reconciler,
        ICanonicalRecoveryCaseRecorder? recovery = null) =>
        new("worker-test", store, new EffectExecutorRegistry([executor]), reconciler,
            TimeSpan.FromMinutes(1), _recoveryCases: recovery);

    private sealed class RecordingRecoveryCases : ICanonicalRecoveryCaseRecorder
    {
        public List<CanonicalRecoveryClassification> Classifications { get; } = [];
        public Task<CanonicalRecoveryClassification> RecordAsync(
            RecoveryScopeKind scope,
            RecoveryCausalSubject subject,
            RecoveryDurableFacts facts,
            CancellationToken cancellationToken = default)
        {
            CanonicalRecoveryClassification classification = CanonicalRecoveryClassifier.Classify(
                new CanonicalRecoveryCase(RecoveryCaseIdentity.New(), scope, subject, DateTimeOffset.UtcNow),
                facts);
            Classifications.Add(classification);
            return Task.FromResult(classification);
        }
    }

    private static EffectIntent Intent(
        int order,
        IReadOnlyList<EffectIntentIdentity>? dependencies = null,
        EffectRequiredness requiredness = EffectRequiredness.BlockingLocal,
        CanonicalCausalContext? causality = null) =>
        new(
            EffectIntentIdentity.New(),
            causality ?? new CanonicalCausalContext(
                WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
                TransitionRunIdentity.New(), AttemptIdentity.New()),
            $"operation-{order}-{Guid.NewGuid():N}",
            new EffectExecutorKey("test"),
            "1",
            new EffectTargetDescriptor("test-target", $"target-{order}", "{}"),
            "{}",
            "sha256:test",
            order,
            dependencies ?? [],
            requiredness,
            new EffectCondition("always", "{}"),
            new EffectCondition("observed", "{}"),
            "observe-before-repeat",
            $"idempotency-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow);

    private sealed class ScriptedExecutor : IEffectExecutor
    {
        public EffectExecutorKey Key => new("test");
        public string Version => "1";
        public int Calls { get; private set; }
        public List<EffectIntentIdentity> Executed { get; } = [];
        public Exception? Failure { get; set; }
        public Action<EffectIntent>? OnExecute { get; set; }
        public EffectExecutionObservation Observation { get; set; } = new(
            EffectLifecycle.Succeeded, "Applied.", ["observed"], "before", "after", true);

        public Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken)
        {
            Calls++;
            Executed.Add(intent.Identity);
            OnExecute?.Invoke(intent);
            if (Failure is not null) throw Failure;
            return Task.FromResult(Observation);
        }
    }

    private sealed class ScriptedReconciler(EffectReconciliationVerdict verdict) : IEffectReconciler
    {
        public int Calls { get; private set; }

        public Task<EffectReconciliationObservation> ReconcileAsync(
            EffectIntent intent,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new EffectReconciliationObservation(
                verdict, "Observed independently.", ["independent-observation"], "before", "after", "correlation"));
        }
    }

    private sealed class MemoryEffectWorkStore(params EffectIntent[] intents) : IEffectWorkStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<EffectIntentIdentity, MutableItem> _items =
            intents.ToDictionary(item => item.Identity, item => new MutableItem(item));
        private long _sequence;

        public EffectIntentIdentity OnlyIdentity => _items.Keys.Single();
        public IReadOnlyList<EffectWorkItem> Items { get { lock (_gate) return _items.Values.Select(value => value.Snapshot()).ToArray(); } }

        public void Append(EffectIntent intent)
        {
            lock (_gate) _items.Add(intent.Identity, new MutableItem(intent));
        }

        public Task<IReadOnlyList<EffectWorkItem>> ScanUnsettledAsync(
            int limit, DateTimeOffset now, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                IReadOnlyList<EffectWorkItem> result = _items.Values
                    .Where(item => item.State != EffectLifecycle.Succeeded)
                    .Where(item => item.LeaseExpiresAt is null || item.LeaseExpiresAt <= now || item.LeaseOwner is null)
                    .OrderBy(item => item.Intent.Order)
                    .Take(limit)
                    .Select(item => item.Snapshot())
                    .ToArray();
                return Task.FromResult(result);
            }
        }

        public Task<EffectWorkItem?> ReadAsync(EffectIntentIdentity identity, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                return Task.FromResult(_items.TryGetValue(identity, out MutableItem? item) ? item.Snapshot() : null);
            }
        }

        public Task<IReadOnlyList<EffectWorkItem>> ReadPlanAsync(
            TransitionRunIdentity transitionRun,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                IReadOnlyList<EffectWorkItem> result = _items.Values
                    .Where(item => item.Intent.Causality.TransitionRun == transitionRun)
                    .OrderBy(item => item.Intent.Order)
                    .Select(item => item.Snapshot())
                    .ToArray();
                return Task.FromResult(result);
            }
        }

        public Task<EffectLease?> TryLeaseAsync(
            EffectIntentIdentity identity,
            long expectedRowVersion,
            string worker,
            DateTimeOffset now,
            TimeSpan duration,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                MutableItem item = _items[identity];
                if (item.RowVersion != expectedRowVersion ||
                    (item.LeaseOwner is not null && item.LeaseExpiresAt > now))
                {
                    return Task.FromResult<EffectLease?>(null);
                }

                EffectLifecycle previous = item.State;
                EffectLifecyclePolicy.RequireTransition(previous, EffectLifecycle.Leased);
                item.State = EffectLifecycle.Leased;
                item.LeaseOwner = worker;
                item.LeaseExpiresAt = now + duration;
                item.RowVersion++;
                item.AttemptCount++;
                item.Events.Add(Event(item, worker, "Lease acquired.", []));
                return Task.FromResult<EffectLease?>(new EffectLease(
                    item.Intent, item.RowVersion, worker, item.LeaseExpiresAt.Value, previous));
            }
        }

        public Task<EffectWorkItem> AppendLifecycleAsync(
            EffectIntentIdentity identity,
            long expectedRowVersion,
            EffectLifecycle state,
            string worker,
            string explanation,
            IReadOnlyList<string> evidence,
            DateTimeOffset recordedAt,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                MutableItem item = RequireVersion(identity, expectedRowVersion);
                EffectLifecyclePolicy.RequireTransition(item.State, state);
                item.State = state;
                item.RowVersion++;
                if (state is not (EffectLifecycle.Leased or EffectLifecycle.Started or EffectLifecycle.Reconciling))
                {
                    item.LeaseOwner = null;
                    item.LeaseExpiresAt = null;
                }
                item.Events.Add(Event(item, worker, explanation, evidence));
                return Task.FromResult(item.Snapshot());
            }
        }

        public Task<EffectWorkItem> RecordReceiptAsync(
            EffectIntentIdentity identity,
            long expectedRowVersion,
            EffectReceipt receipt,
            string worker,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                MutableItem item = RequireVersion(identity, expectedRowVersion);
                Assert.Equal(identity, receipt.Intent);
                Assert.True(receipt.PostconditionSatisfied);
                EffectLifecyclePolicy.RequireTransition(item.State, EffectLifecycle.Succeeded);
                item.Receipt = receipt;
                item.State = EffectLifecycle.Succeeded;
                item.RowVersion++;
                item.LeaseOwner = null;
                item.LeaseExpiresAt = null;
                item.Events.Add(Event(item, worker, "Receipt recorded.", receipt.Evidence));
                return Task.FromResult(item.Snapshot());
            }
        }

        public Task RecordReconciliationAsync(
            EffectIntentIdentity identity,
            long expectedRowVersion,
            EffectReconciliationObservation observation,
            string worker,
            DateTimeOffset recordedAt,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                MutableItem item = RequireVersion(identity, expectedRowVersion);
                if (item.State != EffectLifecycle.Reconciling || item.LeaseOwner != worker)
                    throw new InvalidOperationException("Reconciliation lease mismatch.");
                return Task.CompletedTask;
            }
        }

        private MutableItem RequireVersion(EffectIntentIdentity identity, long expected)
        {
            MutableItem item = _items[identity];
            if (item.RowVersion != expected) throw new InvalidOperationException("Compare-and-set conflict.");
            return item;
        }

        private EffectLifecycleEvent Event(MutableItem item, string worker, string explanation, IReadOnlyList<string> evidence) =>
            new(++_sequence, item.Intent.Identity, item.State, worker, explanation, evidence, DateTimeOffset.UtcNow);

        private sealed class MutableItem(EffectIntent intent)
        {
            public EffectIntent Intent { get; } = intent;
            public EffectLifecycle State { get; set; } = EffectLifecycle.Planned;
            public long RowVersion { get; set; }
            public string? LeaseOwner { get; set; }
            public DateTimeOffset? LeaseExpiresAt { get; set; }
            public int AttemptCount { get; set; }
            public EffectReceipt? Receipt { get; set; }
            public List<EffectLifecycleEvent> Events { get; } = [];
            public EffectWorkItem Snapshot() => new(
                Intent, State, RowVersion, LeaseOwner, LeaseExpiresAt, AttemptCount, Receipt, Events.ToArray());
        }
    }
}
