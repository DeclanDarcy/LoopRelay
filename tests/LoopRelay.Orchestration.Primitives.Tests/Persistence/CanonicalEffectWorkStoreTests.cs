using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Tests.Persistence;

public sealed class CanonicalEffectWorkStoreTests
{
    [Fact]
    public async Task PlanLeaseLifecycleAndReceiptRoundTripAcrossStoreRestart()
    {
        Repository repository = CreateRepository();
        EffectIntent intent = Intent();
        var firstStore = new CanonicalEffectWorkStore(repository);
        await firstStore.AppendPlanAsync([intent], CancellationToken.None);

        EffectWorkItem planned = Assert.Single(await firstStore.ScanUnsettledAsync(10, DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.Equal(EffectLifecycle.Planned, planned.State);
        EffectLease lease = Assert.IsType<EffectLease>(await firstStore.TryLeaseAsync(
            intent.Identity, planned.RowVersion, "worker-a", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), CancellationToken.None));
        Assert.Null(await firstStore.TryLeaseAsync(
            intent.Identity, planned.RowVersion, "worker-b", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), CancellationToken.None));

        EffectWorkItem started = await firstStore.AppendLifecycleAsync(
            intent.Identity, lease.RowVersion, EffectLifecycle.Started, "worker-a", "started", [], DateTimeOffset.UtcNow, CancellationToken.None);
        var receipt = new EffectReceipt(
            EffectReceiptIdentity.New(), intent.Identity, intent.Executor, intent.ExecutorVersion,
            intent.Target.Identity, "before", "after", true, "commit:abc", ["git-observation"], DateTimeOffset.UtcNow);
        await firstStore.RecordReceiptAsync(
            intent.Identity, started.RowVersion, receipt, "worker-a", CancellationToken.None);

        var restarted = new CanonicalEffectWorkStore(repository);
        EffectWorkItem settled = Assert.IsType<EffectWorkItem>(await restarted.ReadAsync(intent.Identity, CancellationToken.None));
        Assert.Equal(EffectLifecycle.Succeeded, settled.State);
        Assert.Equal(receipt.Identity, settled.Receipt!.Identity);
        Assert.Equal([EffectLifecycle.Planned, EffectLifecycle.Leased, EffectLifecycle.Started, EffectLifecycle.Succeeded],
            settled.Events.Select(item => item.State));
        Assert.Empty(await restarted.ScanUnsettledAsync(10, DateTimeOffset.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task RepeatedPlanAppendIsIdempotentAndDoesNotDuplicateLifecycleFacts()
    {
        Repository repository = CreateRepository();
        EffectIntent intent = Intent();
        var store = new CanonicalEffectWorkStore(repository);

        await store.AppendPlanAsync([intent], CancellationToken.None);
        await store.AppendPlanAsync([intent], CancellationToken.None);

        EffectWorkItem item = Assert.IsType<EffectWorkItem>(await store.ReadAsync(intent.Identity, CancellationToken.None));
        Assert.Single(item.Events);
        Assert.Equal(EffectLifecycle.Planned, item.Events[0].State);
    }

    [Fact]
    public async Task IdempotentReplanMayMintANewCandidateIdentityWithoutDuplicatingWork()
    {
        Repository repository = CreateRepository();
        EffectIntent original = Intent();
        var store = new CanonicalEffectWorkStore(repository);
        await store.AppendPlanAsync([original], CancellationToken.None);
        var replanned = new EffectIntent(
            EffectIntentIdentity.New(), original.Causality, original.SemanticOperationKey,
            original.Executor, original.ExecutorVersion, original.Target, original.TypedPayload,
            original.TypedPayloadHash, original.Order, original.Dependencies, original.Requiredness,
            original.Precondition, original.Postcondition, original.ReconciliationPolicy,
            original.IdempotencyKey, original.PlannedAt.AddMinutes(1));

        await store.AppendPlanAsync([replanned], CancellationToken.None);

        IReadOnlyList<EffectWorkItem> plan = await store.ReadPlanAsync(
            original.Causality.TransitionRun, CancellationToken.None);
        EffectWorkItem item = Assert.Single(plan);
        Assert.Equal(original.Identity, item.Intent.Identity);
        Assert.Single(item.Events);
    }

    [Fact]
    public async Task ExpiredLeaseIsDiscoverableAndPreservesPreviousStateForRecovery()
    {
        Repository repository = CreateRepository();
        EffectIntent intent = Intent();
        var store = new CanonicalEffectWorkStore(repository);
        await store.AppendPlanAsync([intent], CancellationToken.None);
        EffectWorkItem planned = (await store.ReadAsync(intent.Identity, CancellationToken.None))!;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        EffectLease first = (await store.TryLeaseAsync(
            intent.Identity, planned.RowVersion, "dead-worker", now, TimeSpan.FromMilliseconds(1), CancellationToken.None))!;
        EffectWorkItem leased = (await store.ReadAsync(intent.Identity, CancellationToken.None))!;

        IReadOnlyList<EffectWorkItem> discovered = await store.ScanUnsettledAsync(10, now.AddSeconds(1), CancellationToken.None);
        Assert.Single(discovered);
        EffectLease replacement = (await store.TryLeaseAsync(
            intent.Identity, leased.RowVersion, "restart-worker", now.AddSeconds(1), TimeSpan.FromMinutes(1), CancellationToken.None))!;

        Assert.Equal(EffectLifecycle.Planned, replacement.PreviousState);
        Assert.True(replacement.RowVersion > first.RowVersion);
    }

    [Fact]
    public async Task RestartedWorkerReconcilesUnknownSQLiteWorkWithoutRedispatch()
    {
        Repository repository = CreateRepository();
        EffectIntent intent = Intent();
        var store = new CanonicalEffectWorkStore(repository);
        await store.AppendPlanAsync([intent], CancellationToken.None);
        var executor = new ThrowAfterMutationExecutor();
        var reconciler = new SucceededReconciler();

        await new EffectWorker("worker-before-crash", store, new EffectExecutorRegistry([executor]), reconciler,
            TimeSpan.FromMinutes(1)).RunOnceAsync();
        Assert.Equal(EffectLifecycle.Unknown, (await store.ReadAsync(intent.Identity, CancellationToken.None))!.State);

        var restartedStore = new CanonicalEffectWorkStore(repository);
        EffectWorkerResult restarted = await new EffectWorker(
            "worker-after-restart", restartedStore, new EffectExecutorRegistry([executor]), reconciler,
            TimeSpan.FromMinutes(1)).RunOnceAsync();

        Assert.Equal(1, executor.Calls);
        Assert.Equal(1, reconciler.Calls);
        Assert.Equal(1, restarted.Succeeded);
        Assert.Equal(EffectLifecycle.Succeeded,
            (await restartedStore.ReadAsync(intent.Identity, CancellationToken.None))!.State);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(
            LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM canonical_effect_reconciliation_attempts WHERE effect_intent_id = $intent;";
        command.Parameters.AddWithValue("$intent", intent.Identity.Value);
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    private static EffectIntent Intent() => new(
        EffectIntentIdentity.New(),
        new CanonicalCausalContext(
            WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(), AttemptIdentity.New()),
        "git.commit.agents",
        new EffectExecutorKey("git-commit"),
        "1",
        new EffectTargetDescriptor("repository", ".agents", "{\"relativePath\":\".agents\"}"),
        "{\"message\":\"publish\"}",
        new string('a', 64),
        0,
        [],
        EffectRequiredness.BlockingLocal,
        new EffectCondition("tree-hash", "{\"expected\":\"before\"}"),
        new EffectCondition("commit-exists", "{\"expected\":\"after\"}"),
        "observe-commit-before-repeat",
        "git-commit:agents:test",
        DateTimeOffset.UtcNow);

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-effect-store-").FullName;
        return new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
    }

    private sealed class ThrowAfterMutationExecutor : IEffectExecutor
    {
        public EffectExecutorKey Key => new("git-commit");
        public string Version => "1";
        public int Calls { get; private set; }
        public Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken)
        {
            Calls++;
            throw new IOException("Mutation escaped; receipt channel failed.");
        }
    }

    private sealed class SucceededReconciler : IEffectReconciler
    {
        public int Calls { get; private set; }
        public Task<EffectReconciliationObservation> ReconcileAsync(EffectIntent intent, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new EffectReconciliationObservation(
                EffectReconciliationVerdict.Succeeded, "Commit independently observed.",
                ["git:commit:abc"], "tree:before", "tree:after", "commit:abc"));
        }
    }
}
