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

        EffectScanRow planned = Assert.Single(await firstStore.ScanUnsettledAsync(10, DateTimeOffset.UtcNow, CancellationToken.None));
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

        IReadOnlyList<EffectScanRow> discovered = await store.ScanUnsettledAsync(10, now.AddSeconds(1), CancellationToken.None);
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

    /// <summary>
    /// A settled intent is settled because its <c>status</c> says so. The pointer into the audit
    /// receipt table is cleared here to prove that no gate consults it: both the dependency gate and
    /// the sibling barrier must answer identically with the pointer present and absent.
    /// </summary>
    [Fact]
    public async Task DependencyGateAndSiblingBarrierAreDecidedByStatusNotTheTerminalReceiptPointer()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        var causality = new CanonicalCausalContext(
            WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(), AttemptIdentity.New());
        DateTimeOffset plannedAt = DateTimeOffset.UtcNow;
        EffectIntent dependency = Intent(causality, order: 0, plannedAt, "dependency");
        EffectIntent candidate = Intent(causality, order: 2, plannedAt, "candidate", [dependency.Identity]);
        // Ordered at or before the candidate but planned after it: the durable sibling barrier.
        EffectIntent sibling = Intent(
            causality, order: 1, plannedAt.AddMinutes(1), "sibling", [dependency.Identity]);
        await store.AppendPlanAsync([dependency, candidate, sibling], CancellationToken.None);

        Assert.False(await store.DependencySatisfiedAsync(candidate, dependency.Identity, CancellationToken.None));
        await SettleAsync(store, dependency);
        // The dependency is settled, but the unsettled sibling planned after the candidate holds it back.
        Assert.False(await store.DependencySatisfiedAsync(candidate, dependency.Identity, CancellationToken.None));
        await SettleAsync(store, sibling);
        Assert.True(await store.DependencySatisfiedAsync(candidate, dependency.Identity, CancellationToken.None));

        await ExecuteAsync(repository, "UPDATE canonical_effect_intents SET terminal_receipt_id = NULL;");

        Assert.True(await store.DependencySatisfiedAsync(candidate, dependency.Identity, CancellationToken.None));
    }

    /// <summary>
    /// Membership of the unsettled scan is a question about <c>status</c> alone. A row whose status
    /// is back in the unsettled band must be rediscovered even though a terminal receipt pointer
    /// still hangs off it, because status is what every writer and every gate agrees on.
    /// </summary>
    [Fact]
    public async Task UnsettledScanMembershipIsDecidedByStatusNotTheTerminalReceiptPointer()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        EffectIntent intent = Intent();
        await store.AppendPlanAsync([intent], CancellationToken.None);
        await SettleAsync(store, intent);

        Assert.Empty(await store.ScanUnsettledAsync(10, DateTimeOffset.UtcNow, CancellationToken.None));

        await ExecuteAsync(
            repository,
            "UPDATE canonical_effect_intents SET status = 'Started' WHERE effect_intent_id = $intent;",
            ("$intent", intent.Identity.Value));

        EffectScanRow scanned = Assert.Single(
            await store.ScanUnsettledAsync(10, DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.Equal(EffectLifecycle.Started, scanned.State);
    }

    /// <summary>
    /// Crash-retry idempotence is carried by the lifecycle policy, which has no transition out of
    /// <see cref="EffectLifecycle.Succeeded"/>. With the terminal receipt pointer cleared, only a
    /// status-based refusal can still hold, and the audit table must keep exactly one receipt.
    /// </summary>
    [Fact]
    public async Task RecordingASecondReceiptForASettledIntentIsRefusedByStatusAlone()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        EffectIntent intent = Intent();
        await store.AppendPlanAsync([intent], CancellationToken.None);
        EffectWorkItem settled = await SettleAsync(store, intent);

        await ExecuteAsync(
            repository,
            "UPDATE canonical_effect_intents SET terminal_receipt_id = NULL WHERE effect_intent_id = $intent;",
            ("$intent", intent.Identity.Value));

        InvalidOperationException refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.RecordReceiptAsync(
                intent.Identity, settled.RowVersion, Receipt(intent), "worker", CancellationToken.None));
        Assert.Equal("Illegal effect lifecycle transition: Succeeded -> Succeeded.", refusal.Message);
        Assert.Equal(1L, await CountAsync(
            repository,
            "SELECT COUNT(*) FROM canonical_effect_receipts WHERE effect_intent_id = $intent;",
            ("$intent", intent.Identity.Value)));
    }

    /// <summary>
    /// Every durable gate now reads settlement off <c>status</c>: the dependency gate, the sibling
    /// barrier and the readiness count all compare the status column rather than the terminal
    /// receipt pointer. <c>AppendLifecycleAsync</c> writes <c>status</c> and never writes a receipt,
    /// and the lifecycle policy on its own permits <c>Started -&gt; Succeeded</c>, so without a guard
    /// a caller can mint a row those gates read as settled while it carries no receipt at all. That
    /// invariant used to be held only by what today's callers happen to pass; this holds it by
    /// construction, and pins that the real settle path stays open.
    /// </summary>
    [Fact]
    public async Task LifecycleAppendRefusesSuccessBecauseOnlyAReceiptMaySettleAnEffect()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        EffectIntent intent = Intent();
        await store.AppendPlanAsync([intent], CancellationToken.None);
        EffectWorkItem planned = (await store.ReadAsync(intent.Identity, CancellationToken.None))!;
        EffectLease lease = (await store.TryLeaseAsync(
            intent.Identity, planned.RowVersion, "worker", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1), CancellationToken.None))!;
        EffectWorkItem started = await store.AppendLifecycleAsync(
            intent.Identity, lease.RowVersion, EffectLifecycle.Started, "worker", "started", [],
            DateTimeOffset.UtcNow, CancellationToken.None);
        // The state machine alone does not stop this, which is exactly why the append path must.
        Assert.True(EffectLifecyclePolicy.CanTransition(EffectLifecycle.Started, EffectLifecycle.Succeeded));

        InvalidOperationException refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.AppendLifecycleAsync(
                intent.Identity, started.RowVersion, EffectLifecycle.Succeeded, "worker",
                "settled without a receipt", [], DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.Equal(
            "Effect success is recorded by receipt: use RecordReceiptAsync, not a lifecycle append.",
            refusal.Message);
        Assert.Equal(0L, await CountAsync(
            repository,
            """
            SELECT COUNT(*) FROM canonical_effect_intents
            WHERE effect_intent_id = $intent AND status = 'Succeeded' AND terminal_receipt_id IS NULL;
            """,
            ("$intent", intent.Identity.Value)));

        // The refusal leaves the row exactly as it was, and the receipt path still settles it.
        EffectWorkItem afterRefusal = (await store.ReadAsync(intent.Identity, CancellationToken.None))!;
        Assert.Equal(EffectLifecycle.Started, afterRefusal.State);
        Assert.Equal(started.RowVersion, afterRefusal.RowVersion);
        EffectWorkItem settled = await store.RecordReceiptAsync(
            intent.Identity, started.RowVersion, Receipt(intent), "worker", CancellationToken.None);
        Assert.Equal(EffectLifecycle.Succeeded, settled.State);
        Assert.Equal(1L, await CountAsync(
            repository,
            """
            SELECT COUNT(*) FROM canonical_effect_intents
            WHERE effect_intent_id = $intent AND status = 'Succeeded' AND terminal_receipt_id IS NOT NULL;
            """,
            ("$intent", intent.Identity.Value)));
    }

    private static async Task<EffectWorkItem> SettleAsync(CanonicalEffectWorkStore store, EffectIntent intent)
    {
        EffectWorkItem planned = (await store.ReadAsync(intent.Identity, CancellationToken.None))!;
        EffectLease lease = (await store.TryLeaseAsync(
            intent.Identity, planned.RowVersion, "worker", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1), CancellationToken.None))!;
        EffectWorkItem started = await store.AppendLifecycleAsync(
            intent.Identity, lease.RowVersion, EffectLifecycle.Started, "worker", "started", [],
            DateTimeOffset.UtcNow, CancellationToken.None);
        return await store.RecordReceiptAsync(
            intent.Identity, started.RowVersion, Receipt(intent), "worker", CancellationToken.None);
    }

    private static EffectReceipt Receipt(EffectIntent intent) => new(
        EffectReceiptIdentity.New(), intent.Identity, intent.Executor, intent.ExecutorVersion,
        intent.Target.Identity, "before", "after", true, null, ["evidence"], DateTimeOffset.UtcNow);

    private static async Task ExecuteAsync(
        Repository repository, string sql, params (string Name, object Value)[] parameters)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(
            LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        await using SqliteCommand command = Prepare(connection, sql, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(
        Repository repository, string sql, params (string Name, object Value)[] parameters)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(
            LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        await using SqliteCommand command = Prepare(connection, sql, parameters);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static SqliteCommand Prepare(
        SqliteConnection connection, string sql, (string Name, object Value)[] parameters)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object value) in parameters) command.Parameters.AddWithValue(name, value);
        return command;
    }

    private static EffectIntent Intent(
        CanonicalCausalContext causality,
        int order,
        DateTimeOffset plannedAt,
        string key,
        IReadOnlyList<EffectIntentIdentity>? dependencies = null) => new(
        EffectIntentIdentity.New(),
        causality,
        $"git.commit.{key}",
        new EffectExecutorKey("git-commit"),
        "1",
        new EffectTargetDescriptor("repository", ".agents", "{\"relativePath\":\".agents\"}"),
        "{\"message\":\"publish\"}",
        new string('a', 64),
        order,
        dependencies ?? [],
        EffectRequiredness.BlockingLocal,
        new EffectCondition("tree-hash", "{\"expected\":\"before\"}"),
        new EffectCondition("commit-exists", "{\"expected\":\"after\"}"),
        "observe-commit-before-repeat",
        $"git-commit:agents:{key}",
        plannedAt);

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
