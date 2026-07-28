using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Tests.Services;

/// <summary>
/// Covers the read-side admission memo <see cref="LoopRelayWorkspaceDatabase.InspectMemoizedAsync"/>
/// added for Task 2.1 (routing <c>LedgerLoopHistoryStore</c> reads through the schema-admission
/// memo instead of <see cref="LoopRelayWorkspaceDatabase.InspectSchemaAsync"/>'s ~190-probe
/// inspection). It must answer straight from <c>VerifiedSchemas</c> plus a 2-SELECT live-stamp
/// re-check when this process has already admitted the database, return <see langword="null"/>
/// whenever the memo is cold or stale, and never itself attempt a write - it exists specifically
/// for callers holding a read-only connection.
/// </summary>
[Collection("WorkspaceDatabaseCounters")]
public sealed class LoopRelayWorkspaceDatabaseInspectMemoizedTests
{
    [Fact]
    public async Task InspectMemoized_OnAdmittedStore_MatchesFullClassificationWithoutFullVerification()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        int baseline = LoopRelayWorkspaceDatabase.ShapeRequirementProbes;
        WorkspaceSchemaInspection? memoized = await LoopRelayWorkspaceDatabase.InspectMemoizedAsync(connection);

        Assert.NotNull(memoized);
        // The memo answers without issuing any of the ~190 individual shape-requirement probes a
        // from-scratch classification costs - mirroring the guard InspectStampedTests uses for the
        // same reason (FullVerificationRuns cannot discriminate this: it is only ever incremented
        // by EnsureSchemaAsync's slow path, which neither this call nor InspectSchemaAsync below
        // goes through, so it would hold unchanged no matter what InspectMemoizedAsync did).
        Assert.Equal(baseline, LoopRelayWorkspaceDatabase.ShapeRequirementProbes);

        // Guards against a vacuous pass: if the counter never moved for anything, the assertion
        // above would hold no matter what InspectMemoizedAsync did. Full classification on the same
        // connection must move it, proving the counter observes the probes the memo path skipped.
        WorkspaceSchemaInspection full = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.True(
            LoopRelayWorkspaceDatabase.ShapeRequirementProbes > baseline,
            "InspectSchemaAsync must issue shape-requirement probes for the counter to be meaningful.");
        Assert.Equal(full, memoized);
    }

    [Fact]
    public async Task InspectMemoized_OnNeverAdmittedDatabase_ReturnsNull()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        // A brand-new temp path is guaranteed not to be a key in the process-wide memo yet, so this
        // exercises the cold-memo path without needing to reset any shared state. The file still
        // has to exist for SQLite to open it read-only, so materialize an empty one first - the
        // memo is process-static, not file-static, so creating the file this way never admits it.
        await using (SqliteConnection create = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await create.OpenAsync();
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync();

        WorkspaceSchemaInspection? memoized = await LoopRelayWorkspaceDatabase.InspectMemoizedAsync(connection);

        Assert.Null(memoized);
    }

    /// <summary>
    /// The fail-closed case this method exists to preserve: a database this process already
    /// admitted (and therefore memoized) has its persisted stamp altered afterward. The live
    /// 2-SELECT re-check inside <see cref="LoopRelayWorkspaceDatabase.InspectMemoizedAsync"/> must
    /// notice the mismatch and refuse to answer from the cache, rather than trusting a memo that no
    /// longer describes the file on disk.
    /// </summary>
    [Fact]
    public async Task InspectMemoized_OnTamperedVersionStamp_ReturnsNull()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        await ExecuteAsync(connection, "UPDATE schema_metadata SET value = '999' WHERE key = 'schema_version';");

        WorkspaceSchemaInspection? memoized = await LoopRelayWorkspaceDatabase.InspectMemoizedAsync(connection);

        Assert.Null(memoized);
    }

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-inspect-memoized-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }

    private static string CreateDatabasePath(Repository repository)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        return databasePath;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
