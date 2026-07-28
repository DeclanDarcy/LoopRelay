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
    public async Task Lifecycle_append_returns_what_an_independent_read_observes()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        EffectIntent intent = Intent(Causality(), order: 0, key: "unknown");
        await store.AppendPlanAsync([intent], CancellationToken.None);
        EffectWorkItem planned = (await store.ReadAsync(intent.Identity, CancellationToken.None))!;

        EffectWorkItem returned = await store.AppendLifecycleAsync(
            intent.Identity, EffectLifecycle.Unknown, "worker-a",
            "Effect execution ended without a trustworthy observation.", ["IOException", "socket closed"],
            DateTimeOffset.UtcNow, CancellationToken.None);

        EffectWorkItem observed = (await new CanonicalEffectWorkStore(repository)
            .ReadAsync(intent.Identity, CancellationToken.None))!;
        Assert.Equal(EffectLifecycle.Planned, planned.State);
        Assert.Equal(EffectLifecycle.Unknown, returned.State);
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
        var receipt = new EffectReceipt(
            EffectReceiptIdentity.New(), intent.Identity, intent.Executor, intent.ExecutorVersion,
            intent.Target.Identity, "absent", "present", true, "sha256:abc", ["file:written"],
            DateTimeOffset.UtcNow);

        EffectWorkItem returned = await store.RecordReceiptAsync(
            intent.Identity, receipt, "worker-a", CancellationToken.None);

        EffectWorkItem observed = (await new CanonicalEffectWorkStore(repository)
            .ReadAsync(intent.Identity, CancellationToken.None))!;
        Assert.Equal(EffectLifecycle.Planned, planned.State);
        Assert.Equal(EffectLifecycle.Succeeded, returned.State);
        Assert.Equal(receipt.Identity, returned.Receipt!.Identity);
        AssertMatches(observed, returned);
    }

    [Fact]
    public async Task Settling_an_effect_needs_no_expected_row_version_from_the_caller()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        EffectIntent intent = Intent(Causality(), order: 0, key: "versionless");
        await store.AppendPlanAsync([intent], CancellationToken.None);
        var receipt = new EffectReceipt(
            EffectReceiptIdentity.New(), intent.Identity, intent.Executor, intent.ExecutorVersion,
            intent.Target.Identity, "absent", "present", true, null, ["file:written"], DateTimeOffset.UtcNow);

        EffectWorkItem settled = await store.RecordReceiptAsync(
            intent.Identity, receipt, "worker", CancellationToken.None);

        Assert.Equal(EffectLifecycle.Succeeded, settled.State);
        Assert.DoesNotContain(
            typeof(IEffectWorkStore).GetMethods().SelectMany(method => method.GetParameters()),
            parameter => parameter.Name is "expectedRowVersion");
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
        // One durable write per settled effect: the plan append, then the terminal receipt. No
        // lease event, no start event.
        EffectLifecycle[] expected = [EffectLifecycle.Planned, EffectLifecycle.Succeeded];
        foreach (EffectIntent intent in new[] { first, second })
        {
            EffectWorkItem settled = (await store.ReadAsync(intent.Identity, CancellationToken.None))!;
            Assert.Equal(expected, settled.Events.Select(item => item.State));
            Assert.Equal(
                settled.Events.Select(item => item.Sequence).Order(),
                settled.Events.Select(item => item.Sequence));
        }
    }

#if DEBUG
    [Fact]
    public async Task Settling_an_effect_costs_one_connection_open_beyond_the_shared_scan()
    {
        Repository repository = CreateRepository();
        CanonicalCausalContext causality = Causality();
        EffectIntent[] intents = Enumerable.Range(0, 10)
            .Select(index => Intent(causality, order: index, key: $"item-{index}"))
            .ToArray();
        var store = new CanonicalEffectWorkStore(repository);
        await store.AppendPlanAsync(intents, CancellationToken.None);
        // Watching starts here, so the plan append above is outside the tally.
        using var counting = new ConnectionOpenCountingStore(store);

        EffectWorkerResult result = await Worker(counting, new RecordingExecutor()).RunOnceAsync(CancellationToken.None);

        Assert.Equal(10, result.Succeeded);
        // Ten INDEPENDENT effects in one pass. Directly measured call shape: one shared scan, then
        // one receipt write per effect. No plan read, because nothing here has a dependency; a
        // dependent effect would additionally pay one DependencyGate open per dependency.
        Assert.Equal(1, counting.Scans);
        Assert.Equal(0, counting.Reads);
        Assert.Equal(0, counting.LifecycleAppends);
        Assert.Equal(10, counting.ReceiptRecords);
        Assert.Equal(0, counting.PlanReads);
        // Counted, not modelled: the store reports every connection it opens, attributed to the
        // call that was running. Nothing below is a constant this test declares, so re-adding an
        // open inside any settlement write raises the tally without anyone editing this file.
        Assert.Equal(0, counting.UnattributedOpens);
        // Total INCLUDING the one shared scan open.
        Assert.Equal(11, counting.ConnectionOpens);
        // Per settled independent effect, EXCLUDING the shared scan open.
        Assert.Equal(1, (counting.ConnectionOpens - counting.ScanOpens) / result.Succeeded);
        // The one, decomposed by the step that opened it.
        Assert.Equal(1, counting.ScanOpens);
        Assert.Equal(0, counting.ReadOpens);
        Assert.Equal(0, counting.LifecycleAppendOpens);
        Assert.Equal(10, counting.ReceiptRecordOpens);
        Assert.Equal(0, counting.PlanReadOpens);
        Assert.Equal(0, counting.DependencyGateOpens);
    }

    /// <summary>
    /// The scan's cost is statements, not connections: it hands its own connection to every
    /// per-row read, so the connection-open budget above cannot see a per-row hydration at all.
    /// Counted through <see cref="CanonicalEffectWorkStore.CommandObserverForTesting"/>, which the
    /// store invokes once per statement it prepares, so re-introducing any per-row read raises the
    /// tally without anyone editing this file. Nothing below is a constant this test declares.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(25)]
    public async Task Scanning_unsettled_work_issues_one_statement_regardless_of_row_count(int rowCount)
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = Causality();
        EffectIntent[] intents = Enumerable.Range(0, rowCount)
            .Select(index => Intent(causality, order: index, key: $"scan-{index}"))
            .ToArray();
        await store.AppendPlanAsync(intents, CancellationToken.None);

        // Watching starts here, so the plan append above is outside the tally.
        int statements = 0;
        store.CommandObserverForTesting = _ => statements++;
        try
        {
            IReadOnlyList<EffectScanRow> rows =
                await store.ScanUnsettledAsync(128, DateTimeOffset.UtcNow, CancellationToken.None);

            Assert.Equal(rowCount, rows.Count);
            Assert.All(rows, row => Assert.Equal(EffectLifecycle.Planned, row.State));
        }
        finally
        {
            store.CommandObserverForTesting = null;
        }

        // Constant in the row count: the per-row hydration this replaced cost 2N+1.
        Assert.Equal(1, statements);
    }
#endif

    private static void AssertMatches(EffectWorkItem observed, EffectWorkItem returned)
    {
        Assert.Equal(observed.Intent.Identity, returned.Intent.Identity);
        Assert.Equal(observed.State, returned.State);
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
        new UnusedReconciler());

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

#if DEBUG
    /// <summary>
    /// Counts the work-store calls a run makes, and counts the connections the store really opens
    /// while each of those calls is running.
    /// <para>
    /// The opens are observed, not derived: it installs itself on
    /// <see cref="CanonicalEffectWorkStore.ConnectionObserverForTesting"/>, which the store invokes
    /// once per connection it opens, and attributes each open to the decorated call in flight. A
    /// declared per-method constant could not catch a regression here — re-adding an open inside a
    /// settlement write would keep satisfying it until someone hand-edited the constant, which is
    /// the whole point of the assertion. <see cref="EffectWorker"/> drives the store strictly
    /// sequentially, so "the call in flight" is unambiguous, and any open that arrives outside one
    /// lands in <see cref="UnattributedOpens"/> rather than being silently absorbed.
    /// </para>
    /// </summary>
    private sealed class ConnectionOpenCountingStore : IEffectWorkStore, IDisposable
    {
        private readonly CanonicalEffectWorkStore _inner;
        private readonly int[] _opens = new int[Enum.GetValues<StoreCall>().Length];
        private StoreCall _inFlight = StoreCall.None;

        public ConnectionOpenCountingStore(CanonicalEffectWorkStore inner)
        {
            _inner = inner;
            inner.ConnectionObserverForTesting = _ => _opens[(int)_inFlight]++;
        }

        private enum StoreCall
        {
            None, Scan, Read, PlanRead, DependencyGate, LifecycleAppend, ReceiptRecord,
        }

        public int Scans { get; private set; }
        public int Reads { get; private set; }
        public int PlanReads { get; private set; }
        public int DependencyGates { get; private set; }
        public int LifecycleAppends { get; private set; }
        public int ReceiptRecords { get; private set; }

        public int ConnectionOpens => _opens.Sum();
        public int UnattributedOpens => _opens[(int)StoreCall.None];
        public int ScanOpens => _opens[(int)StoreCall.Scan];
        public int ReadOpens => _opens[(int)StoreCall.Read];
        public int PlanReadOpens => _opens[(int)StoreCall.PlanRead];
        public int DependencyGateOpens => _opens[(int)StoreCall.DependencyGate];
        public int LifecycleAppendOpens => _opens[(int)StoreCall.LifecycleAppend];
        public int ReceiptRecordOpens => _opens[(int)StoreCall.ReceiptRecord];

        public void Dispose() => _inner.ConnectionObserverForTesting = null;

        public Task<IReadOnlyList<EffectScanRow>> ScanUnsettledAsync(
            int limit, DateTimeOffset now, CancellationToken cancellationToken,
            IReadOnlySet<EffectIntentIdentity>? only = null)
        {
            Scans++;
            return AttributeAsync(
                StoreCall.Scan, () => _inner.ScanUnsettledAsync(limit, now, cancellationToken, only));
        }

        public Task<IReadOnlyList<EffectWorkItem>> ReadPlanAsync(
            TransitionRunIdentity transitionRun, CancellationToken cancellationToken)
        {
            PlanReads++;
            return AttributeAsync(
                StoreCall.PlanRead, () => _inner.ReadPlanAsync(transitionRun, cancellationToken));
        }

        public Task<EffectWorkItem?> ReadAsync(EffectIntentIdentity identity, CancellationToken cancellationToken)
        {
            Reads++;
            return AttributeAsync(StoreCall.Read, () => _inner.ReadAsync(identity, cancellationToken));
        }

        public Task<bool> DependencySatisfiedAsync(
            EffectIntent candidate, EffectIntentIdentity dependency, CancellationToken cancellationToken)
        {
            DependencyGates++;
            return AttributeAsync(
                StoreCall.DependencyGate,
                () => _inner.DependencySatisfiedAsync(candidate, dependency, cancellationToken));
        }

        public Task<EffectWorkItem> AppendLifecycleAsync(
            EffectIntentIdentity identity, EffectLifecycle state, string worker,
            string explanation, IReadOnlyList<string> evidence, DateTimeOffset recordedAt,
            CancellationToken cancellationToken)
        {
            LifecycleAppends++;
            return AttributeAsync(
                StoreCall.LifecycleAppend,
                () => _inner.AppendLifecycleAsync(
                    identity, state, worker, explanation, evidence, recordedAt, cancellationToken));
        }

        public Task<EffectWorkItem> RecordReceiptAsync(
            EffectIntentIdentity identity, EffectReceipt receipt, string worker,
            CancellationToken cancellationToken)
        {
            ReceiptRecords++;
            return AttributeAsync(
                StoreCall.ReceiptRecord,
                () => _inner.RecordReceiptAsync(identity, receipt, worker, cancellationToken));
        }

        /// <summary>
        /// Runs one store call with <paramref name="call"/> marked as in flight, restoring whatever
        /// was in flight before rather than assuming nothing was, so a nested call cannot make the
        /// opens after it look unattributed.
        /// </summary>
        private async Task<T> AttributeAsync<T>(StoreCall call, Func<Task<T>> body)
        {
            StoreCall enclosing = _inFlight;
            _inFlight = call;
            try
            {
                return await body();
            }
            finally
            {
                _inFlight = enclosing;
            }
        }
    }
#endif

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
