using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;

namespace LoopRelay.Orchestration.Tests.Effects;

/// <summary>
/// Pins the settlement returns of the durable effect store. <c>AppendLifecycleAsync</c> and
/// <c>RecordReceiptAsync</c> project the item they just wrote out of in-transaction knowledge
/// rather than re-reading it after commit, so these tests hold that projection to what an
/// independent read of the committed row observes.
/// </summary>
public sealed class DurableEffectSettlementReturnTests
{
    [Fact]
    public async Task Lease_retaining_lifecycle_append_returns_what_an_independent_read_observes()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        EffectIntent intent = Intent(Causality(), order: 0, key: "started");
        await store.AppendPlanAsync([intent], CancellationToken.None);
        EffectWorkItem planned = (await store.ReadAsync(intent.Identity, CancellationToken.None))!;
        EffectLease lease = (await store.TryLeaseAsync(
            intent.Identity, planned.RowVersion, "worker-a", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1), CancellationToken.None))!;

        EffectWorkItem returned = await store.AppendLifecycleAsync(
            intent.Identity, lease.RowVersion, EffectLifecycle.Started, "worker-a",
            "Outward effect execution started.", ["dispatch:1"], DateTimeOffset.UtcNow, CancellationToken.None);

        EffectWorkItem observed = (await new CanonicalEffectWorkStore(repository)
            .ReadAsync(intent.Identity, CancellationToken.None))!;
        Assert.Equal(EffectLifecycle.Started, returned.State);
        Assert.Equal(lease.RowVersion + 1, returned.RowVersion);
        // A lease-retaining transition must not clear the lease in the projection either, or the
        // next compare-and-set sees an owner the database does not hold.
        Assert.Equal("worker-a", returned.LeaseOwner);
        AssertMatches(observed, returned);
    }

    [Fact]
    public async Task Lease_clearing_lifecycle_append_returns_what_an_independent_read_observes()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        EffectIntent intent = Intent(Causality(), order: 0, key: "unknown");
        await store.AppendPlanAsync([intent], CancellationToken.None);
        EffectWorkItem planned = (await store.ReadAsync(intent.Identity, CancellationToken.None))!;
        EffectLease lease = (await store.TryLeaseAsync(
            intent.Identity, planned.RowVersion, "worker-a", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1), CancellationToken.None))!;
        EffectWorkItem started = await store.AppendLifecycleAsync(
            intent.Identity, lease.RowVersion, EffectLifecycle.Started, "worker-a", "started",
            [], DateTimeOffset.UtcNow, CancellationToken.None);

        EffectWorkItem returned = await store.AppendLifecycleAsync(
            intent.Identity, started.RowVersion, EffectLifecycle.Unknown, "worker-a",
            "Effect execution ended without a trustworthy observation.", ["IOException", "socket closed"],
            DateTimeOffset.UtcNow, CancellationToken.None);

        EffectWorkItem observed = (await new CanonicalEffectWorkStore(repository)
            .ReadAsync(intent.Identity, CancellationToken.None))!;
        Assert.Equal(EffectLifecycle.Unknown, returned.State);
        Assert.Equal(started.RowVersion + 1, returned.RowVersion);
        Assert.Null(returned.LeaseOwner);
        Assert.Null(returned.LeaseExpiresAt);
        AssertMatches(observed, returned);
    }

    [Fact]
    public async Task Receipt_record_returns_what_an_independent_read_observes()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        EffectIntent intent = Intent(Causality(), order: 0, key: "settled");
        await store.AppendPlanAsync([intent], CancellationToken.None);
        EffectWorkItem planned = (await store.ReadAsync(intent.Identity, CancellationToken.None))!;
        EffectLease lease = (await store.TryLeaseAsync(
            intent.Identity, planned.RowVersion, "worker-a", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1), CancellationToken.None))!;
        EffectWorkItem started = await store.AppendLifecycleAsync(
            intent.Identity, lease.RowVersion, EffectLifecycle.Started, "worker-a", "started",
            [], DateTimeOffset.UtcNow, CancellationToken.None);
        var receipt = new EffectReceipt(
            EffectReceiptIdentity.New(), intent.Identity, intent.Executor, intent.ExecutorVersion,
            intent.Target.Identity, "absent", "present", true, "sha256:abc", ["file:written"],
            DateTimeOffset.UtcNow);

        EffectWorkItem returned = await store.RecordReceiptAsync(
            intent.Identity, started.RowVersion, receipt, "worker-a", CancellationToken.None);

        EffectWorkItem observed = (await new CanonicalEffectWorkStore(repository)
            .ReadAsync(intent.Identity, CancellationToken.None))!;
        Assert.Equal(EffectLifecycle.Succeeded, returned.State);
        Assert.Equal(started.RowVersion + 1, returned.RowVersion);
        Assert.Null(returned.LeaseOwner);
        Assert.Null(returned.LeaseExpiresAt);
        Assert.Equal(receipt.Identity, returned.Receipt!.Identity);
        AssertMatches(observed, returned);
    }

    [Fact]
    public async Task Settled_effects_keep_their_lifecycle_order_in_the_returned_item_and_in_the_store()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = Causality();
        EffectIntent first = Intent(causality, order: 0, key: "first");
        EffectIntent second = Intent(causality, order: 1, key: "second", dependencies: [first.Identity]);
        await store.AppendPlanAsync([first, second], CancellationToken.None);
        var executor = new RecordingExecutor();

        EffectWorkerResult result = await Worker(store, executor).RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, result.Succeeded);
        Assert.Equal([first.Identity, second.Identity], executor.Executed);
        EffectLifecycle[] expected =
        [
            EffectLifecycle.Planned, EffectLifecycle.Leased, EffectLifecycle.Started, EffectLifecycle.Succeeded,
        ];
        foreach (EffectIntent intent in new[] { first, second })
        {
            EffectWorkItem settled = (await store.ReadAsync(intent.Identity, CancellationToken.None))!;
            Assert.Equal(expected, settled.Events.Select(item => item.State));
            Assert.Equal(
                settled.Events.Select(item => item.Sequence).Order(),
                settled.Events.Select(item => item.Sequence));
        }
    }

    [Fact]
    public async Task Settling_an_effect_stays_within_the_four_connection_open_budget()
    {
        Repository repository = CreateRepository();
        CanonicalCausalContext causality = Causality();
        EffectIntent[] intents = Enumerable.Range(0, 10)
            .Select(index => Intent(causality, order: index, key: $"item-{index}"))
            .ToArray();
        var store = new CanonicalEffectWorkStore(repository);
        await store.AppendPlanAsync(intents, CancellationToken.None);
        var counting = new ConnectionOpenCountingStore(store);

        EffectWorkerResult result = await Worker(counting, new RecordingExecutor()).RunOnceAsync(CancellationToken.None);

        Assert.Equal(10, result.Succeeded);
        // Directly measured call shape: one scan, then lease + post-lease read + start + receipt
        // per effect, and no plan read because nothing has a dependency.
        Assert.Equal(1, counting.Scans);
        Assert.Equal(10, counting.Reads);
        Assert.Equal(10, counting.Leases);
        Assert.Equal(10, counting.LifecycleAppends);
        Assert.Equal(10, counting.ReceiptRecords);
        Assert.Equal(0, counting.PlanReads);
        Assert.Equal(41, counting.DerivedConnectionOpens);
        Assert.Equal(4, (counting.DerivedConnectionOpens - ConnectionOpenCountingStore.ScanOpens) / result.Succeeded);
    }

    private static void AssertMatches(EffectWorkItem observed, EffectWorkItem returned)
    {
        Assert.Equal(observed.Intent.Identity, returned.Intent.Identity);
        Assert.Equal(observed.State, returned.State);
        Assert.Equal(observed.RowVersion, returned.RowVersion);
        Assert.Equal(observed.LeaseOwner, returned.LeaseOwner);
        Assert.Equal(observed.LeaseExpiresAt, returned.LeaseExpiresAt);
        Assert.Equal(observed.AttemptCount, returned.AttemptCount);
        Assert.Equal(observed.Receipt?.Identity, returned.Receipt?.Identity);
        Assert.Equal(observed.Receipt?.Evidence, returned.Receipt?.Evidence);
        Assert.Equal(observed.Receipt?.RecordedAt, returned.Receipt?.RecordedAt);
        Assert.Equal(
            observed.Events.Select(item => (item.Sequence, item.State, item.Worker, item.Explanation, item.RecordedAt)),
            returned.Events.Select(item => (item.Sequence, item.State, item.Worker, item.Explanation, item.RecordedAt)));
        Assert.Equal(
            observed.Events.Select(item => item.Evidence),
            returned.Events.Select(item => item.Evidence));
    }

    private static EffectWorker Worker(IEffectWorkStore store, RecordingExecutor executor) => new(
        "settlement-return-worker",
        store,
        new EffectExecutorRegistry([executor]),
        new UnusedReconciler(),
        TimeSpan.FromMinutes(1));

    private static CanonicalCausalContext Causality() => new(
        WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
        TransitionRunIdentity.New(), AttemptIdentity.New());

    private static EffectIntent Intent(
        CanonicalCausalContext causality,
        int order,
        string key,
        IReadOnlyList<EffectIntentIdentity>? dependencies = null) => new(
        EffectIntentIdentity.New(),
        causality,
        $"filesystem.write.{key}",
        new EffectExecutorKey("filesystem-write"),
        "1",
        new EffectTargetDescriptor("repository", key, $"{{\"relativePath\":\"{key}\"}}"),
        $"{{\"content\":\"{key}\"}}",
        new string('c', 64),
        order,
        dependencies ?? [],
        EffectRequiredness.BlockingLocal,
        new EffectCondition("absent", "{\"expected\":\"absent\"}"),
        new EffectCondition("present", "{\"expected\":\"present\"}"),
        "observe-file-before-repeat",
        $"filesystem-write:{key}",
        DateTimeOffset.UtcNow);

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-effect-return-").FullName;
        return new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
    }

    /// <summary>
    /// Counts the work-store calls a run makes and derives connection opens from them, using the
    /// same audited per-method constants the wave-5 reconnaissance used so the numbers stay
    /// comparable to its baseline. Every <see cref="CanonicalEffectWorkStore"/> entry point opens
    /// exactly one connection; before the in-transaction returns, the two settlement writes opened
    /// a second one each for their post-commit re-read. Changing the number of opens inside any of
    /// these methods means changing the matching constant here.
    /// </summary>
    private sealed class ConnectionOpenCountingStore(IEffectWorkStore _inner) : IEffectWorkStore
    {
        internal const int ScanOpens = 1;
        private const int ReadOpens = 1;
        private const int PlanReadOpens = 1;
        private const int DependencyGateOpens = 1;
        private const int LeaseOpens = 1;
        private const int LifecycleAppendOpens = 1;
        private const int ReceiptRecordOpens = 1;
        private const int ReconciliationOpens = 1;

        public int Scans { get; private set; }
        public int Reads { get; private set; }
        public int PlanReads { get; private set; }
        public int DependencyGates { get; private set; }
        public int Leases { get; private set; }
        public int LifecycleAppends { get; private set; }
        public int ReceiptRecords { get; private set; }
        public int Reconciliations { get; private set; }

        public int DerivedConnectionOpens =>
            (Scans * ScanOpens) + (Reads * ReadOpens) + (PlanReads * PlanReadOpens) +
            (DependencyGates * DependencyGateOpens) + (Leases * LeaseOpens) +
            (LifecycleAppends * LifecycleAppendOpens) + (ReceiptRecords * ReceiptRecordOpens) +
            (Reconciliations * ReconciliationOpens);

        public Task<IReadOnlyList<EffectWorkItem>> ScanUnsettledAsync(
            int limit, DateTimeOffset now, CancellationToken cancellationToken,
            IReadOnlySet<EffectIntentIdentity>? only = null)
        {
            Scans++;
            return _inner.ScanUnsettledAsync(limit, now, cancellationToken, only);
        }

        public Task<IReadOnlyList<EffectWorkItem>> ReadPlanAsync(
            TransitionRunIdentity transitionRun, CancellationToken cancellationToken)
        {
            PlanReads++;
            return _inner.ReadPlanAsync(transitionRun, cancellationToken);
        }

        public Task<EffectWorkItem?> ReadAsync(EffectIntentIdentity identity, CancellationToken cancellationToken)
        {
            Reads++;
            return _inner.ReadAsync(identity, cancellationToken);
        }

        public Task<bool> DependencySatisfiedAsync(
            EffectIntent candidate, EffectIntentIdentity dependency, CancellationToken cancellationToken)
        {
            DependencyGates++;
            return _inner.DependencySatisfiedAsync(candidate, dependency, cancellationToken);
        }

        public Task<EffectLease?> TryLeaseAsync(
            EffectIntentIdentity identity, long expectedRowVersion, string worker, DateTimeOffset now,
            TimeSpan duration, CancellationToken cancellationToken)
        {
            Leases++;
            return _inner.TryLeaseAsync(identity, expectedRowVersion, worker, now, duration, cancellationToken);
        }

        public Task<EffectWorkItem> AppendLifecycleAsync(
            EffectIntentIdentity identity, long expectedRowVersion, EffectLifecycle state, string worker,
            string explanation, IReadOnlyList<string> evidence, DateTimeOffset recordedAt,
            CancellationToken cancellationToken)
        {
            LifecycleAppends++;
            return _inner.AppendLifecycleAsync(
                identity, expectedRowVersion, state, worker, explanation, evidence, recordedAt, cancellationToken);
        }

        public Task<EffectWorkItem> RecordReceiptAsync(
            EffectIntentIdentity identity, long expectedRowVersion, EffectReceipt receipt, string worker,
            CancellationToken cancellationToken)
        {
            ReceiptRecords++;
            return _inner.RecordReceiptAsync(identity, expectedRowVersion, receipt, worker, cancellationToken);
        }

        public Task RecordReconciliationAsync(
            EffectIntentIdentity identity, long expectedRowVersion, EffectReconciliationObservation observation,
            string worker, DateTimeOffset recordedAt, CancellationToken cancellationToken)
        {
            Reconciliations++;
            return _inner.RecordReconciliationAsync(
                identity, expectedRowVersion, observation, worker, recordedAt, cancellationToken);
        }
    }

    private sealed class RecordingExecutor : IEffectExecutor
    {
        private readonly List<EffectIntentIdentity> _executed = [];

        public EffectExecutorKey Key => new("filesystem-write");
        public string Version => "1";
        public IReadOnlyList<EffectIntentIdentity> Executed => _executed;

        public Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken)
        {
            _executed.Add(intent.Identity);
            return Task.FromResult(new EffectExecutionObservation(
                EffectLifecycle.Succeeded, "Wrote the file.", ["file:written"], "absent", "present", true));
        }
    }

    private sealed class UnusedReconciler : IEffectReconciler
    {
        public Task<EffectReconciliationObservation> ReconcileAsync(
            EffectIntent intent,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Reconciliation is not expected in a settlement-return test.");
    }
}
