using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Execution;

public sealed class LedgerLoopHistoryStoreTests
{
    [Fact]
    public async Task Append_writes_the_ledger_fact_with_identity_and_lineage_and_projects_the_numbered_file()
    {
        (LedgerLoopHistoryStore store, string root, Repository repository) = New();
        var lineage = new LoopHistoryLineage("run_test", "tr_test", "att_test");

        LoopHistoryRecord record = await store.AppendAsync(LoopHistoryKind.Decisions, "decided things", lineage);

        Assert.Equal(1, record.Sequence);
        Assert.Equal(".agents/decisions/decisions.0001.md", record.RelativePath);
        string projected = Path.Combine(root, ".agents", "decisions", "decisions.0001.md");
        Assert.True(File.Exists(projected), "Expected the numbered projection file to be written.");
        Assert.Equal("decided things", await File.ReadAllTextAsync(projected));

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT kind, sequence, logical_path, body, content_hash, history_id, run_id, transition_run_id, attempt_id
            FROM loop_history;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Decisions", reader.GetString(0));
        Assert.Equal(1, reader.GetInt32(1));
        Assert.Equal(".agents/decisions/decisions.0001.md", reader.GetString(2));
        Assert.Equal("decided things", reader.GetString(3));
        Assert.Equal(ConsumedInputFile.HashContent("decided things"), reader.GetString(4));
        Assert.StartsWith("hist_", reader.GetString(5), StringComparison.Ordinal);
        Assert.Equal("run_test", reader.GetString(6));
        Assert.Equal("tr_test", reader.GetString(7));
        Assert.Equal("att_test", reader.GetString(8));
        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task Append_imports_pre_ledger_numbered_files_and_continues_the_sequence()
    {
        (LedgerLoopHistoryStore store, string root, Repository repository) = New();
        WriteFile(root, ".agents/handoffs/handoff.0003.md", "pre-ledger handoff");

        LoopHistoryRecord record = await store.AppendAsync(LoopHistoryKind.Handoff, "new handoff");

        Assert.Equal(4, record.Sequence);
        Assert.Equal(".agents/handoffs/handoff.0004.md", record.RelativePath);
        Assert.True(File.Exists(Path.Combine(root, ".agents", "handoffs", "handoff.0004.md")));
        // The pre-ledger file is untouched: projections are additive, never overwritten.
        Assert.Equal("pre-ledger handoff", await File.ReadAllTextAsync(Path.Combine(root, ".agents", "handoffs", "handoff.0003.md")));

        // The pre-ledger file became an append-only ledger fact with stable identity and null
        // lineage (its writer predates lineage), so the whole history lives in one ledger.
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT body, history_id, run_id FROM loop_history WHERE kind = 'Handoff' AND sequence = 3;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("pre-ledger handoff", reader.GetString(0));
        Assert.StartsWith("hist_", reader.GetString(1), StringComparison.Ordinal);
        Assert.True(reader.IsDBNull(2));
    }

    [Fact]
    public async Task Read_latest_imports_pre_ledger_numbered_files_once_and_reads_the_ledger()
    {
        (LedgerLoopHistoryStore store, string root, Repository repository) = New();
        // Create the workspace database first (the CLI-entry schema gate guarantees this in
        // production), then plant a pre-ledger numbered file.
        await store.AppendAsync(LoopHistoryKind.Decisions, "unrelated decision");
        WriteFile(root, ".agents/deltas/operational_delta.0001.md", "pre-ledger delta");

        LoopHistoryRecord? latest = await store.ReadLatestAsync(LoopHistoryKind.OperationalDelta);

        Assert.NotNull(latest);
        Assert.Equal("pre-ledger delta", latest.Content);
        Assert.Equal(1, latest.Sequence);

        // The import is one-way, once: rereading neither duplicates the fact nor rereads the file.
        File.Delete(Path.Combine(root, ".agents", "deltas", "operational_delta.0001.md"));
        LoopHistoryRecord? reread = await store.ReadLatestAsync(LoopHistoryKind.OperationalDelta);
        Assert.NotNull(reread);
        Assert.Equal("pre-ledger delta", reread.Content);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM loop_history WHERE kind = 'OperationalDelta';";
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Read_latest_verifies_the_content_hash()
    {
        (LedgerLoopHistoryStore store, string _, Repository repository) = New();
        await store.AppendAsync(LoopHistoryKind.Decisions, "original");

        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(LoopRelayWorkspaceDatabase.Resolve(repository)))
        {
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE loop_history SET body = 'tampered' WHERE sequence = 1;";
            await command.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ReadLatestAsync(LoopHistoryKind.Decisions));
    }

    [Fact]
    public async Task Read_latest_returns_null_when_no_workspace_database_exists()
    {
        (LedgerLoopHistoryStore store, string _, Repository _) = New();

        Assert.Null(await store.ReadLatestAsync(LoopHistoryKind.Decisions));
    }

    private static (LedgerLoopHistoryStore Store, string Root, Repository Repository) New()
    {
        string root = Directory.CreateTempSubdirectory("cc-cli-ledger-history").FullName;
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(root),
            Path = root,
        };
        return (new LedgerLoopHistoryStore(new FileSystemArtifactStore(), repository), root, repository);
    }

    private static void WriteFile(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
