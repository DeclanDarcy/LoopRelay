using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Execution;

public sealed class LedgerLoopHistoryStoreTests
{
    [Fact]
    public async Task Append_commits_fact_evidence_and_canonical_effect_before_filesystem_projection()
    {
        Harness harness = await NewAsync();
        var evidence = new HistoryEvidenceAttachments(
            Provider: new HistoryProviderEvidence("codex", "thread-1", "turn-1"));

        LoopHistoryRecord record = await harness.Store.AppendAsync(new LoopHistoryAppendRequest(
            LoopHistoryKind.Decisions,
            "decided things",
            harness.Causality,
            evidence));

        Assert.Equal(1, record.Sequence);
        Assert.StartsWith("hfact_", record.Identity.Value, StringComparison.Ordinal);
        Assert.Equal(evidence.Identity, record.Evidence.Identity);
        Assert.False(File.Exists(Path.Combine(harness.Root, ".agents", "decisions", "decisions.0001.md")));
        await using SqliteConnection connection = await OpenAsync(harness.Repository, readOnly: true);
        Assert.Equal(1L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM history_evidence_sets;"));
        Assert.Equal(1L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM history_evidence_items;"));
        Assert.Equal(evidence.Identity.Value, await ScalarStringAsync(connection, "SELECT evidence_set_id FROM history_evidence_sets;"));
        Assert.Equal(evidence.Provider!.Identity.Value, await ScalarStringAsync(connection, "SELECT evidence_item_id FROM history_evidence_items;"));
        Assert.Equal("Planned", await ScalarStringAsync(connection,
            "SELECT status FROM canonical_effect_intents WHERE semantic_operation_key = 'history:materialize:Decisions';"));
        Assert.Equal(WorkspaceEffectExecutorKeys.FilesystemWrite.Value, await ScalarStringAsync(connection,
            "SELECT executor_key FROM canonical_effect_intents WHERE semantic_operation_key = 'history:materialize:Decisions';"));
        Assert.Equal(0L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM canonical_projection_effects;"));
    }

    [Fact]
    public async Task Canonical_effect_worker_materializes_history_only_after_the_fact_is_durable()
    {
        Harness harness = await NewAsync();
        await harness.Store.AppendAsync(new LoopHistoryAppendRequest(
            LoopHistoryKind.Handoff,
            "handoff",
            harness.Causality));

        var workStore = new CanonicalEffectWorkStore(harness.Repository);
        var executor = new FilesystemWriteEffectExecutor(harness.Repository);
        var worker = new EffectWorker(
            "history-projection-test",
            workStore,
            new EffectExecutorRegistry([executor]),
            new FilesystemWriteEffectReconciler(harness.Repository),
            TimeSpan.FromMinutes(1));
        EffectWorkerResult result = await worker.RunOnceAsync();

        Assert.Equal(1, result.Succeeded);
        EffectWorkItem item = Assert.Single(await workStore.ReadPlanAsync(
            harness.Causality.TransitionRun, CancellationToken.None));
        Assert.Equal(EffectLifecycle.Succeeded, item.State);
        Assert.True(item.Receipt?.PostconditionSatisfied);
        Assert.Equal(
            "handoff",
            await File.ReadAllTextAsync(Path.Combine(harness.Root, ".agents", "handoffs", "handoff.0001.md")));
    }

    [Fact]
    public async Task Read_latest_roundtrips_canonical_causality_and_typed_evidence()
    {
        Harness harness = await NewAsync();
        await harness.Store.AppendAsync(new LoopHistoryAppendRequest(
            LoopHistoryKind.OperationalDelta,
            "delta",
            harness.Causality,
            new HistoryEvidenceAttachments(
                Recovery: new HistoryRecoveryEvidence(new RecoveryAttemptIdentity("recovery-1")))));

        LoopHistoryRecord? latest =
            await harness.Store.ReadLatestAsync(LoopHistoryKind.OperationalDelta);

        Assert.NotNull(latest);
        Assert.Equal(harness.Causality, latest.Causality);
        Assert.Equal("recovery-1", latest.Evidence.Recovery!.RecoveryAttempt.Value);
    }

    [Fact]
    public async Task Append_rejects_a_causal_tree_from_another_workspace()
    {
        Harness harness = await NewAsync();
        CanonicalCausalContext foreign = NewCausality(WorkspaceIdentity.New());

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Store.AppendAsync(
            new LoopHistoryAppendRequest(LoopHistoryKind.Decisions, "foreign", foreign)));
    }

    [Fact]
    public async Task Numbered_files_are_not_lazily_imported_by_canonical_reads()
    {
        Harness harness = await NewAsync();
        WriteFile(harness.Root, ".agents/deltas/operational_delta.0001.md", "legacy");

        Assert.Null(await harness.Store.ReadLatestAsync(LoopHistoryKind.OperationalDelta));
        await using SqliteConnection connection = await OpenAsync(harness.Repository, readOnly: true);
        Assert.Equal(0L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM loop_history;"));
    }

    private static async Task<Harness> NewAsync()
    {
        string root = Directory.CreateTempSubdirectory("cc-cli-ledger-history").FullName;
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(root),
            Path = root,
        };
        await using SqliteConnection connection = await OpenAsync(repository, readOnly: false, create: true);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        WorkspaceIdentity workspace = new(await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection));
        return new Harness(root, repository, new LedgerLoopHistoryStore(repository), NewCausality(workspace));
    }

    private static CanonicalCausalContext NewCausality(WorkspaceIdentity workspace) =>
        new(
            workspace,
            RunIdentity.New(),
            WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(),
            AttemptIdentity.New());

    private static async Task<SqliteConnection> OpenAsync(
        Repository repository,
        bool readOnly,
        bool create = false)
    {
        string path = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SqliteConnection connection = readOnly
            ? LoopRelayWorkspaceDatabase.OpenReadOnly(path)
            : create
                ? LoopRelayWorkspaceDatabase.OpenReadWriteCreate(path)
                : LoopRelayWorkspaceDatabase.OpenReadWrite(path);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<long> ScalarLongAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string?> ScalarStringAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private static void WriteFile(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private sealed record Harness(
        string Root,
        Repository Repository,
        LedgerLoopHistoryStore Store,
        CanonicalCausalContext Causality);
}
