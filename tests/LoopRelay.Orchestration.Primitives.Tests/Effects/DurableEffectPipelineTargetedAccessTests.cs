using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;

namespace LoopRelay.Orchestration.Tests.Effects;

/// <summary>
/// Pins the targeted-access behaviour of the durable effect pipeline against the real SQLite
/// store: the <c>only:</c> filter is a SQL predicate rather than a post-scan in-memory filter, so
/// a targeted run can never be truncated out of its own scan window.
/// </summary>
public sealed class DurableEffectPipelineTargetedAccessTests
{
    private const int ScanLimit = 128;

    [Fact]
    public async Task Targeted_run_settles_its_intent_when_the_unsettled_backlog_exceeds_the_scan_limit()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = Causality();
        // Every backlog intent sorts ahead of the target under the scan's
        // `ORDER BY effect_order, planned_at, effect_intent_id`, so a scan capped at the worker's
        // 128-item limit cannot see the target at all.
        EffectIntent[] backlog = Enumerable.Range(0, ScanLimit + 72)
            .Select(index => Intent(causality, order: 0, key: $"backlog-{index}"))
            .ToArray();
        EffectIntent target = Intent(causality, order: 1, key: "target");
        await store.AppendPlanAsync([.. backlog, target], CancellationToken.None);

        var executor = new RecordingExecutor();
        EffectWorkerResult result = await Worker(store, executor).RunOnceAsync(
            CancellationToken.None, only: new HashSet<EffectIntentIdentity> { target.Identity });

        Assert.Equal([target.Identity], executor.Executed);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(
            EffectLifecycle.Succeeded,
            (await store.ReadAsync(target.Identity, CancellationToken.None))!.State);
    }

    [Fact]
    public async Task Targeted_scan_hydrates_only_the_requested_intents()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = Causality();
        EffectIntent[] backlog = Enumerable.Range(0, 10)
            .Select(index => Intent(causality, order: index, key: $"backlog-{index}"))
            .ToArray();
        EffectIntent target = Intent(causality, order: 11, key: "target");
        await store.AppendPlanAsync([.. backlog, target], CancellationToken.None);

        IReadOnlyList<EffectScanRow>targeted = await store.ScanUnsettledAsync(
            ScanLimit,
            DateTimeOffset.UtcNow,
            CancellationToken.None,
            new HashSet<EffectIntentIdentity> { target.Identity });
        IReadOnlyList<EffectScanRow>unfiltered = await store.ScanUnsettledAsync(
            ScanLimit, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal([target.Identity], targeted.Select(item => item.Intent.Identity));
        Assert.Equal(backlog.Length + 1, unfiltered.Count);
    }

    [Fact]
    public async Task Targeted_scan_narrows_the_unsettled_predicate_rather_than_replacing_it()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = Causality();
        EffectIntent settled = Intent(causality, order: 0, key: "settled");
        await store.AppendPlanAsync([settled], CancellationToken.None);
        await Worker(store, new RecordingExecutor()).RunOnceAsync(CancellationToken.None);

        IReadOnlyList<EffectScanRow>naming = await store.ScanUnsettledAsync(
            ScanLimit,
            DateTimeOffset.UtcNow,
            CancellationToken.None,
            new HashSet<EffectIntentIdentity> { settled.Identity });
        IReadOnlyList<EffectScanRow>nothing = await store.ScanUnsettledAsync(
            ScanLimit, DateTimeOffset.UtcNow, CancellationToken.None, new HashSet<EffectIntentIdentity>());

        Assert.Equal(
            EffectLifecycle.Succeeded,
            (await store.ReadAsync(settled.Identity, CancellationToken.None))!.State);
        Assert.Empty(naming);
        Assert.Empty(nothing);
    }

    [Fact]
    public async Task Targeted_run_settles_selected_intents_in_lifecycle_order_and_reports_only_them_as_discovered()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = Causality();
        EffectIntent third = Intent(causality, order: 5, key: "third");
        EffectIntent first = Intent(causality, order: 1, key: "first");
        EffectIntent second = Intent(causality, order: 3, key: "second");
        EffectIntent[] backlog = Enumerable.Range(0, ScanLimit + 72)
            .Select(index => Intent(causality, order: 0, key: $"backlog-{index}"))
            .ToArray();
        await store.AppendPlanAsync([third, first, second, .. backlog], CancellationToken.None);

        var executor = new RecordingExecutor();
        EffectWorkerResult result = await Worker(store, executor).RunOnceAsync(
            CancellationToken.None,
            only: new HashSet<EffectIntentIdentity> { third.Identity, first.Identity, second.Identity });

        Assert.Equal([first.Identity, second.Identity, third.Identity], executor.Executed);
        Assert.Equal(3, result.Succeeded);
        // Pushing the filter into SQL narrows `Discovered` from "everything the scan window held"
        // to "the requested intents". No production caller reads `Discovered` from a targeted run.
        Assert.Equal(3, result.Discovered);
    }

    [Fact]
    public async Task Unfiltered_run_still_reports_the_whole_scan_window_as_discovered()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = Causality();
        await store.AppendPlanAsync(
            [.. Enumerable.Range(0, 4).Select(index => Intent(causality, order: index, key: $"item-{index}"))],
            CancellationToken.None);

        EffectWorkerResult result = await Worker(store, new RecordingExecutor()).RunOnceAsync(CancellationToken.None);

        Assert.Equal(4, result.Discovered);
        Assert.Equal(4, result.Succeeded);
    }

    private static EffectWorker Worker(IEffectWorkStore store, RecordingExecutor executor) => new(
        "targeted-access-worker",
        store,
        new EffectExecutorRegistry([executor]),
        new UnusedReconciler(),
        ScanLimit);

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
        new string('b', 64),
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
        string path = Directory.CreateTempSubdirectory("looprelay-effect-targeted-").FullName;
        return new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
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
            throw new InvalidOperationException("Reconciliation is not expected in a targeted-access test.");
    }
}
