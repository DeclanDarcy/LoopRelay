using System.Globalization;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace LoopRelay.Orchestration.Tests.Effects;

/// <summary>
/// Pins the durable effect pipeline's plan hydration and dependency gate against the real SQLite
/// store: a plan is hydrated with a fixed number of set-based statements rather than one read per
/// item, and the dependency gate answers from columns without ever loading
/// <c>definition_json</c> — the intent document that embeds file contents.
/// </summary>
public sealed class DurableEffectPlanHydrationTests
{
    private const int ScanLimit = 128;

    /// <summary>
    /// The recon's ordering fixture. A dependency check (B on A) deliberately runs *before* the
    /// feature executor that appends a child effect, because a naive two-item fixture does not
    /// reproduce the hazard: whatever answers the gate must observe the child that appeared after
    /// the gate was first consulted in this pass.
    /// </summary>
    [Fact]
    public async Task Publication_waits_for_a_child_effect_appended_after_an_earlier_dependency_check()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = Causality();
        DateTimeOffset planned = DateTimeOffset.UtcNow.AddMinutes(-5);
        EffectIntent a = Intent(causality, order: 0, key: "a", plannedAt: planned);
        EffectIntent b = Intent(causality, order: 1, key: "b", plannedAt: planned, dependencies: [a.Identity]);
        EffectIntent feature = Intent(
            causality, order: 2, key: "feature", plannedAt: planned, executor: "feature-write");
        EffectIntent publication = Intent(
            causality, order: 3, key: "publication", plannedAt: planned, dependencies: [feature.Identity]);
        await store.AppendPlanAsync([a, b, feature, publication], CancellationToken.None);

        var recorder = new ExecutionRecorder();
        EffectIntent child = Intent(
            causality, order: 3, key: "child", plannedAt: DateTimeOffset.UtcNow, dependencies: [feature.Identity]);
        var featureExecutor = new AppendingExecutor(
            new EffectExecutorKey("feature-write"), recorder, store, child);
        await RunToQuiescenceAsync(Worker(store, [new RecordingExecutor(recorder), featureExecutor]));

        Assert.Equal(
            [a.Identity, b.Identity, feature.Identity, child.Identity, publication.Identity],
            recorder.Executed);
    }

    /// <summary>
    /// Observational proof that the dependency gate never loads <c>definition_json</c>: the settled
    /// dependency's intent document is replaced with a value that cannot deserialize into an
    /// <see cref="EffectIntent"/>, so any read of that column throws. The gate still clears.
    /// </summary>
    [Fact]
    public async Task Dependency_gate_clears_when_the_dependency_intent_document_cannot_be_read()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = Causality();
        EffectIntent dependency = Intent(causality, order: 0, key: "dependency");
        EffectIntent dependent = Intent(
            causality, order: 1, key: "dependent", dependencies: [dependency.Identity]);
        await store.AppendPlanAsync([dependency, dependent], CancellationToken.None);

        var recorder = new ExecutionRecorder();
        await Worker(store, [new RecordingExecutor(recorder)]).RunOnceAsync(
            CancellationToken.None, only: new HashSet<EffectIntentIdentity> { dependency.Identity });
        await CorruptIntentDocumentAsync(repository, dependency.Identity);

        // The corruption is real: anything that does read the intent document sees it.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ReadAsync(dependency.Identity, CancellationToken.None));

        await Worker(store, [new RecordingExecutor(recorder)]).RunOnceAsync(
            CancellationToken.None, only: new HashSet<EffectIntentIdentity> { dependent.Identity });

        Assert.Equal([dependency.Identity, dependent.Identity], recorder.Executed);
        Assert.Equal(
            EffectLifecycle.Succeeded,
            (await store.ReadAsync(dependent.Identity, CancellationToken.None))!.State);
    }

    /// <summary>
    /// The barrier matches a shared dependency wherever it sits in a sibling's dependency list, so
    /// a set-based membership test cannot get away with inspecting only the first element.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Dependency_barrier_matches_a_shared_dependency_at_any_list_position(int position)
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = Causality();
        DateTimeOffset planned = DateTimeOffset.UtcNow.AddMinutes(-5);
        EffectIntent shared = Intent(causality, order: 0, key: "shared", plannedAt: planned);
        EffectIntent other = Intent(causality, order: 1, key: "other", plannedAt: planned);
        EffectIntent candidate = Intent(
            causality, order: 5, key: "candidate", plannedAt: planned, dependencies: [shared.Identity]);
        EffectIntent sibling = Intent(
            causality,
            order: 5,
            key: "sibling",
            plannedAt: planned.AddMinutes(1),
            dependencies: position == 0
                ? [shared.Identity, other.Identity]
                : [other.Identity, shared.Identity]);
        await store.AppendPlanAsync([shared, other, candidate, sibling], CancellationToken.None);

        var recorder = new ExecutionRecorder();
        await RunToQuiescenceAsync(Worker(store, [new RecordingExecutor(recorder)]));

        Assert.Equal(
            [shared.Identity, other.Identity, sibling.Identity, candidate.Identity],
            recorder.Executed);
    }

    /// <summary>
    /// Plan hydration costs a fixed number of statements regardless of plan size, counted from the
    /// statements <c>ReadPlanAsync</c> really compiles rather than from a model of them: one per
    /// table — intents, receipts, lifecycle events — at every plan size.
    /// <para>
    /// The fixture is settled first, so every item carries a receipt and a lifecycle history. That
    /// is what makes the sizes discriminating: item-at-a-time hydration paid <c>1 + 2N + R</c> here
    /// — an identity query, then a core row and an event history per item plus a receipt per
    /// settled one — so twenty items cost 61 statements against the set-based three, and a
    /// regression to per-row reads cannot land inside the asserted count at any of these sizes.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task Plan_hydration_costs_the_same_number_of_statements_at_every_plan_size(int items)
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = Causality();
        await store.AppendPlanAsync(
            [.. Enumerable.Range(0, items).Select(index => Intent(causality, index, $"item-{index}"))],
            CancellationToken.None);
        await Worker(store, [new RecordingExecutor(new ExecutionRecorder())]).RunOnceAsync(CancellationToken.None);

        var counter = new PreparedStatementCounter();
        store.ConnectionObserverForTesting = counter.Watch;
        IReadOnlyList<EffectWorkItem> plan;
        try
        {
            plan = await store.ReadPlanAsync(causality.TransitionRun, CancellationToken.None);
        }
        finally
        {
            store.ConnectionObserverForTesting = null;
        }

        Assert.Equal(items, plan.Count);
        Assert.All(plan, item => Assert.NotNull(item.Receipt));
        // Counted, not modelled: one statement per table, and no term in the plan's size.
        Assert.Equal(3, counter.Statements);
    }

    [Fact]
    public async Task Set_based_hydration_observes_what_an_item_at_a_time_hydration_observes()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = Causality();
        EffectIntent first = Intent(causality, order: 0, key: "first");
        EffectIntent second = Intent(causality, order: 1, key: "second", dependencies: [first.Identity]);
        // Never run, so it carries no receipt and only its `Planned` event.
        EffectIntent untouched = Intent(causality, order: 2, key: "untouched", semanticOperation: "shared.operation");
        EffectIntent alsoShared = Intent(causality, order: 3, key: "also-shared", semanticOperation: "shared.operation");
        await store.AppendPlanAsync([first, second, untouched, alsoShared], CancellationToken.None);
        await Worker(store, [new RecordingExecutor(new ExecutionRecorder())]).RunOnceAsync(
            CancellationToken.None,
            only: new HashSet<EffectIntentIdentity> { first.Identity, second.Identity });

        IReadOnlyList<EffectWorkItem> plan = await store.ReadPlanAsync(
            causality.TransitionRun, CancellationToken.None);
        IReadOnlyList<EffectWorkItem> run = await store.ReadRunAsync(causality.Run, CancellationToken.None);
        IReadOnlyList<EffectWorkItem> semantic = await store.ReadBySemanticOperationAsync(
            "shared.operation", CancellationToken.None);

        Assert.Equal(
            [first.Identity, second.Identity, untouched.Identity, alsoShared.Identity],
            plan.Select(item => item.Intent.Identity));
        Assert.Equal(
            [untouched.Identity, alsoShared.Identity],
            semantic.Select(item => item.Intent.Identity));
        Assert.Equal(
            plan.Select(item => item.Intent.Identity.Value).Order(StringComparer.Ordinal),
            run.Select(item => item.Intent.Identity.Value).Order(StringComparer.Ordinal));
        foreach (EffectWorkItem item in plan.Concat(run).Concat(semantic))
        {
            AssertSameObservation(
                (await store.ReadAsync(item.Intent.Identity, CancellationToken.None))!, item);
        }
        Assert.Null(plan.Single(item => item.Intent.Identity == untouched.Identity).Receipt);
        Assert.NotNull(plan.Single(item => item.Intent.Identity == second.Identity).Receipt);
    }

    private static void AssertSameObservation(EffectWorkItem expected, EffectWorkItem actual)
    {
        Assert.Equal(expected.Intent.Identity, actual.Intent.Identity);
        Assert.Equal(expected.Intent.IdempotencyKey, actual.Intent.IdempotencyKey);
        Assert.Equal(expected.Intent.Dependencies, actual.Intent.Dependencies);
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.RowVersion, actual.RowVersion);
        Assert.Equal(expected.LeaseOwner, actual.LeaseOwner);
        Assert.Equal(expected.LeaseExpiresAt, actual.LeaseExpiresAt);
        Assert.Equal(expected.AttemptCount, actual.AttemptCount);
        Assert.Equal(expected.Receipt?.Identity, actual.Receipt?.Identity);
        Assert.Equal(expected.Receipt?.Intent, actual.Receipt?.Intent);
        Assert.Equal(expected.Receipt?.PostconditionSatisfied, actual.Receipt?.PostconditionSatisfied);
        Assert.Equal(expected.Receipt?.Evidence, actual.Receipt?.Evidence);
        Assert.Equal(expected.Receipt?.RecordedAt, actual.Receipt?.RecordedAt);
        Assert.Equal(
            expected.Events.Select(value => (value.Sequence, value.Intent, value.State, value.Worker,
                value.Explanation, value.RecordedAt)),
            actual.Events.Select(value => (value.Sequence, value.Intent, value.State, value.Worker,
                value.Explanation, value.RecordedAt)));
        Assert.Equal(
            expected.Events.Select(value => value.Evidence),
            actual.Events.Select(value => value.Evidence));
    }

    /// <summary>
    /// Drives repeated worker passes the way <c>TransitionEffectCoordinator</c> does — up to
    /// sixteen — stopping once a pass makes no progress, so a deferred item gets the later pass its
    /// barrier is waiting for.
    /// </summary>
    private static async Task RunToQuiescenceAsync(EffectWorker worker)
    {
        for (int pass = 0; pass < 16; pass++)
        {
            EffectWorkerResult result = await worker.RunOnceAsync(CancellationToken.None);
            if (result.Dispatched == 0) return;
        }
        throw new InvalidOperationException("The effect worker did not reach quiescence.");
    }

    private static async Task CorruptIntentDocumentAsync(Repository repository, EffectIntentIdentity identity)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(
            LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync(CancellationToken.None);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "UPDATE canonical_effect_intents SET definition_json = 'null' WHERE effect_intent_id = $intent;";
        command.Parameters.AddWithValue("$intent", identity.Value);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(CancellationToken.None));
    }

    private static EffectWorker Worker(IEffectWorkStore store, IEffectExecutor[] executors) => new(
        "plan-hydration-worker",
        store,
        new EffectExecutorRegistry(executors),
        new UnusedReconciler(),
        ScanLimit);

    private static CanonicalCausalContext Causality() => new(
        WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
        TransitionRunIdentity.New(), AttemptIdentity.New());

    private static EffectIntent Intent(
        CanonicalCausalContext causality,
        int order,
        string key,
        IReadOnlyList<EffectIntentIdentity>? dependencies = null,
        DateTimeOffset? plannedAt = null,
        string executor = "filesystem-write",
        string? semanticOperation = null) => new(
        EffectIntentIdentity.New(),
        causality,
        semanticOperation ?? $"filesystem.write.{key}",
        new EffectExecutorKey(executor),
        "1",
        new EffectTargetDescriptor("repository", key, $"{{\"relativePath\":\"{key}\"}}"),
        $"{{\"content\":\"{key}\"}}",
        new string('d', 64),
        order,
        dependencies ?? [],
        EffectRequiredness.BlockingLocal,
        new EffectCondition("absent", "{\"expected\":\"absent\"}"),
        new EffectCondition("present", "{\"expected\":\"present\"}"),
        "observe-file-before-repeat",
        $"filesystem-write:{key}",
        plannedAt ?? DateTimeOffset.UtcNow);

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-effect-hydration-").FullName;
        return new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
    }

    private sealed class ExecutionRecorder
    {
        private readonly List<EffectIntentIdentity> _executed = [];

        public IReadOnlyList<EffectIntentIdentity> Executed => _executed;

        public void Record(EffectIntentIdentity identity) => _executed.Add(identity);
    }

    private sealed class RecordingExecutor(ExecutionRecorder _recorder) : IEffectExecutor
    {
        public EffectExecutorKey Key => new("filesystem-write");
        public string Version => "1";

        public Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken)
        {
            _recorder.Record(intent.Identity);
            return Task.FromResult(new EffectExecutionObservation(
                EffectLifecycle.Succeeded, "Wrote the file.", ["file:written"], "absent", "present", true));
        }
    }

    /// <summary>
    /// Stands in for a feature executor that plans ordered child effects while the worker is still
    /// processing the scan snapshot that predates them.
    /// </summary>
    private sealed class AppendingExecutor(
        EffectExecutorKey _key,
        ExecutionRecorder _recorder,
        IEffectPlanStore _plans,
        EffectIntent _child) : IEffectExecutor
    {
        private bool _appended;

        public EffectExecutorKey Key => _key;
        public string Version => "1";

        public async Task<EffectExecutionObservation> ExecuteAsync(
            EffectIntent intent,
            CancellationToken cancellationToken)
        {
            _recorder.Record(intent.Identity);
            if (!_appended)
            {
                _appended = true;
                await _plans.AppendPlanAsync([_child], cancellationToken);
            }
            return new EffectExecutionObservation(
                EffectLifecycle.Succeeded, "Planned the child effect.", ["child:planned"], "absent", "present", true);
        }
    }

    private sealed class UnusedReconciler : IEffectReconciler
    {
        public Task<EffectReconciliationObservation> ReconcileAsync(
            EffectIntent intent,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Reconciliation is not expected in a plan-hydration test.");
    }

    /// <summary>
    /// Counts the SQL statements a store connection actually compiles, by installing a SQLite
    /// authorizer on it. SQLite consults the authorizer while preparing a statement and raises
    /// <c>SQLITE_SELECT</c> exactly once for each SELECT it compiles, so the tally is the read
    /// path's real statement count — nothing here models what the implementation ought to cost, and
    /// a per-row implementation reports its per-row tally.
    /// <para>
    /// The authorizer is the only statement-level hook SQLitePCLRaw exposes and it is per
    /// connection, which is why the store hands its connections to
    /// <see cref="CanonicalEffectWorkStore.ConnectionObserverForTesting"/>: it opens them itself,
    /// with pooling off, so a test cannot otherwise reach the handle a read ran on.
    /// </para>
    /// </summary>
    private sealed class PreparedStatementCounter
    {
        // Held for the lifetime of the counter: SQLite keeps calling this for as long as the
        // connection lives, so it must not be collected once `Watch` returns.
        private readonly delegate_authorizer _authorizer;
        private int _statements;

        public PreparedStatementCounter() => _authorizer = Authorize;

        public int Statements => Volatile.Read(ref _statements);

        public void Watch(SqliteConnection connection) => Assert.Equal(
            raw.SQLITE_OK, raw.sqlite3_set_authorizer(connection.Handle, _authorizer, null));

        private int Authorize(
            object userData, int action, utf8z first, utf8z second, utf8z database, utf8z trigger)
        {
            if (action == raw.SQLITE_SELECT) Interlocked.Increment(ref _statements);
            return raw.SQLITE_OK;
        }
    }
}
