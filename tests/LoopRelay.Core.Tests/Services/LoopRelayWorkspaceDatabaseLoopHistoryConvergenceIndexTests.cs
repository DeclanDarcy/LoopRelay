using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Tests.Services;

/// <summary>
/// Covers the registration of <c>idx_loop_history_kind_content_hash</c> - the partial unique index
/// backstopping loop-history convergence - in the canonical-v16 shape contract.
///
/// <para>
/// The index has always been in <see cref="LoopRelayWorkspaceDatabase"/>'s v9 DDL block, but until
/// it appeared in a <c>ShapeRequirement</c> list it was invisible to the shape machinery: a
/// database already stamped complete classified complete, took the zero-transaction
/// structurally-complete branch, never re-ran the DDL, and so reported itself healthy while
/// physically lacking the constraint. These tests prove the contract now covers it, that the
/// fingerprint moved, and that the index physically lands on both the fresh-create path and the
/// migrate-an-already-complete-database path - each proved by probing <c>sqlite_master</c>.
/// </para>
/// </summary>
[Collection("WorkspaceDatabaseCounters")]
public sealed class LoopRelayWorkspaceDatabaseLoopHistoryConvergenceIndexTests
{
    private const string ConvergenceIndex = "idx_loop_history_kind_content_hash";

    /// <summary>
    /// Asserts the fingerprint change live rather than against a golden string.
    ///
    /// <para>
    /// <c>ComputeShapeFingerprint</c> hashes the distinct, ordered requirement-token set, and the
    /// pre-change v16 contract was exactly today's contract minus this one index token. So the
    /// <em>observed</em> fingerprint of a physically complete v16 database with only this index
    /// dropped is, byte for byte, the fingerprint the old contract produced - which makes the
    /// inequality below a direct live comparison of old contract against new.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Registering_the_convergence_index_moved_the_canonical_v16_shape_fingerprint()
    {
        await using SqliteConnection connection = await CreateCanonicalDatabaseAsync();
        Assert.Equal(
            LoopRelayWorkspaceDatabase.CanonicalV16ShapeFingerprint,
            await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_shape';"));

        await ExecuteAsync(connection, $"DROP INDEX {ConvergenceIndex};");
        LoopRelayWorkspaceDatabase.ResetSchemaVerificationCacheForTesting();
        WorkspaceSchemaInspection withoutIndex = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);

        // The token is genuinely in the contract: dropping only this index moves the observed
        // fingerprint and drops the database out of `CanonicalV16Complete`.
        Assert.NotEqual(LoopRelayWorkspaceDatabase.CanonicalV16ShapeFingerprint, withoutIndex.ShapeFingerprint);
        Assert.Equal(WorkspaceSchemaShape.CorruptCanonicalV16, withoutIndex.Shape);

        // Earlier contracts are deliberately untouched, so genuine pre-v16 databases - none of
        // which can carry an index introduced after they were stamped - stay migratable.
        Assert.NotEqual(
            LoopRelayWorkspaceDatabase.CanonicalV15ShapeFingerprint,
            LoopRelayWorkspaceDatabase.CanonicalV16ShapeFingerprint);
        Assert.Equal(16, LoopRelayWorkspaceDatabase.CurrentSchemaVersion);
    }

    [Fact]
    public async Task EnsureSchemaAsync_creates_the_convergence_index_on_a_fresh_database()
    {
        await using SqliteConnection connection = await CreateCanonicalDatabaseAsync();

        Assert.True(
            await IndexExistsAsync(connection, ConvergenceIndex),
            $"Expected the fresh-create path to leave `{ConvergenceIndex}` in sqlite_master.");
        Assert.True(
            await IndexIsUniqueAsync(connection, ConvergenceIndex),
            $"Expected `{ConvergenceIndex}` to be registered as a unique index.");
        Assert.True(
            await IndexIsPartialAsync(connection, ConvergenceIndex),
            $"Expected `{ConvergenceIndex}` to be registered as a partial index.");
    }

    /// <summary>
    /// The path the Critical finding was about: a database that was already stamped complete before
    /// the index existed. It must stop short-circuiting on its stamp, take the migration branch, and
    /// come out with the index physically present.
    /// </summary>
    [Fact]
    public async Task EnsureSchemaAsync_creates_the_convergence_index_on_a_previously_complete_database()
    {
        await using SqliteConnection connection = await CreateCanonicalDatabaseAsync();
        string workspaceId = await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection);

        await RevertToPreConvergenceIndexV15ShapeAsync(connection);
        await SeedLoopHistoryAsync(connection);
        Assert.False(await IndexExistsAsync(connection, ConvergenceIndex));

        LoopRelayWorkspaceDatabase.ResetSchemaVerificationCacheForTesting();
        WorkspaceSchemaInspection before = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaShape.CanonicalV15Complete, before.Shape);

        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.True(
            await IndexExistsAsync(connection, ConvergenceIndex),
            $"Expected the migration branch to leave `{ConvergenceIndex}` in sqlite_master.");
        Assert.True(await IndexIsUniqueAsync(connection, ConvergenceIndex));
        Assert.True(await IndexIsPartialAsync(connection, ConvergenceIndex));

        WorkspaceSchemaInspection after = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaShape.CanonicalV16Complete, after.Shape);
        Assert.Equal(LoopRelayWorkspaceDatabase.CanonicalV16ShapeFingerprint, after.ShapeFingerprint);
        Assert.Equal(workspaceId, await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection));

        // The partial predicate survived the round trip: the two seeded rows that repeat
        // (kind, content_hash) with a NULL history_id are still there, because the index only
        // constrains canonical rows.
        Assert.Equal(2L, await ScalarLongAsync(
            connection,
            "SELECT COUNT(*) FROM loop_history WHERE history_id IS NULL AND kind = 'decisions';"));

        // And the constraint is live, not merely present.
        SqliteException duplicate = await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
            connection,
            """
            INSERT INTO loop_history (kind, sequence, logical_path, body, content_hash, created_at, history_id)
            VALUES ('loop', 99, 'loop/99.md', 'body', 'hash-canonical-a', '2026-07-12T00:00:09.0000000Z', 'history-dup');
            """));
        Assert.Contains("UNIQUE constraint failed", duplicate.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The sharp edge, recorded rather than hidden: <c>CREATE UNIQUE INDEX</c> is evaluated against
    /// the rows already in the table, so a previously-complete database that somehow accumulated two
    /// canonical loop-history rows sharing <c>(kind, content_hash)</c> cannot migrate. The failure
    /// is loud and the whole migration transaction rolls back, leaving the file exactly as it was -
    /// it is never half-converged, and never silently complete.
    /// </summary>
    [Fact]
    public async Task Migration_fails_loudly_when_a_previously_complete_database_already_holds_duplicate_canonical_rows()
    {
        await using SqliteConnection connection = await CreateCanonicalDatabaseAsync();

        await RevertToPreConvergenceIndexV15ShapeAsync(connection);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO loop_history (kind, sequence, logical_path, body, content_hash, created_at, history_id)
            VALUES
                ('loop', 1, 'loop/1.md', 'body', 'hash-duplicate', '2026-07-12T00:00:00.0000000Z', 'history-one'),
                ('loop', 2, 'loop/2.md', 'body', 'hash-duplicate', '2026-07-12T00:00:01.0000000Z', 'history-two');
            """);

        LoopRelayWorkspaceDatabase.ResetSchemaVerificationCacheForTesting();
        SqliteException failure = await Assert.ThrowsAsync<SqliteException>(
            () => LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection));
        Assert.Contains("UNIQUE constraint failed", failure.Message, StringComparison.Ordinal);

        // Rolled back whole: still v15, still no index, both rows intact.
        Assert.False(await IndexExistsAsync(connection, ConvergenceIndex));
        Assert.Equal("15", await ScalarStringAsync(
            connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        Assert.Equal(2L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM loop_history;"));
    }

    /// <summary>
    /// The one case registration cannot convert into a migration, stated in a test so it is not
    /// folklore: a database stamped at schema version 16 carrying the <em>pre-change</em> v16
    /// fingerprint. Version 16 is <c>CurrentSchemaVersion</c>, so the classifier has no
    /// "out of date, migrate me" reading available - a v16 stamp that does not match the v16
    /// fingerprint is by construction a corrupt v16, and <c>EnsureSchemaAsync</c> refuses to mutate
    /// it. That is a loud, diagnosable block rather than the silent false-complete the registration
    /// was added to eliminate; under the fresh-start ruling such databases are recreated, not
    /// migrated.
    /// </summary>
    [Fact]
    public async Task A_v16_database_stamped_at_the_pre_change_fingerprint_is_blocked_loudly_rather_than_reported_complete()
    {
        await using SqliteConnection connection = await CreateCanonicalDatabaseAsync();

        await ExecuteAsync(connection, $"DROP INDEX {ConvergenceIndex};");
        LoopRelayWorkspaceDatabase.ResetSchemaVerificationCacheForTesting();
        string preChangeFingerprint =
            (await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection)).ShapeFingerprint!;
        await ExecuteAsync(
            connection,
            "UPDATE schema_metadata SET value = $shape WHERE key = 'schema_shape';",
            ("$shape", preChangeFingerprint));

        LoopRelayWorkspaceDatabase.ResetSchemaVerificationCacheForTesting();
        WorkspaceSchemaInspection inspection = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.NotEqual(WorkspaceSchemaShape.CanonicalV16Complete, inspection.Shape);
        Assert.Equal(WorkspaceSchemaShape.CorruptCanonicalV16, inspection.Shape);
        Assert.Equal(WorkspaceSchemaFamily.Unknown, inspection.Family);

        InvalidOperationException blocked = await Assert.ThrowsAsync<InvalidOperationException>(
            () => LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection));
        Assert.Contains("Unsupported workspace schema", blocked.Message, StringComparison.Ordinal);
    }

    private static async Task<SqliteConnection> CreateCanonicalDatabaseAsync()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-loop-history-index-").FullName;
        Repository repository = new()
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        LoopRelayWorkspaceDatabase.ResetSchemaVerificationCacheForTesting();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        return connection;
    }

    /// <summary>
    /// Physically reconstructs a database that a build predating the convergence index would have
    /// left behind and stamped complete: the v16 recovery-scope additions reverted, the convergence
    /// index absent, and a legitimate canonical-v15 version and shape stamp on top.
    /// </summary>
    private static async Task RevertToPreConvergenceIndexV15ShapeAsync(SqliteConnection connection)
    {
        await ExecuteAsync(connection, $"DROP INDEX IF EXISTS {ConvergenceIndex};");
        await ExecuteAsync(connection, "DROP INDEX IF EXISTS idx_recovery_action_events_scope;");
        await ExecuteAsync(connection, "ALTER TABLE canonical_recovery_action_events DROP COLUMN scope_id;");
        await ExecuteAsync(connection, "UPDATE schema_metadata SET value = '15' WHERE key = 'schema_version';");
        await ExecuteAsync(
            connection,
            "UPDATE schema_metadata SET value = $shape WHERE key = 'schema_shape';",
            ("$shape", LoopRelayWorkspaceDatabase.CanonicalV15ShapeFingerprint));
    }

    private static async Task SeedLoopHistoryAsync(SqliteConnection connection) =>
        await ExecuteAsync(
            connection,
            """
            INSERT INTO loop_history (kind, sequence, logical_path, body, content_hash, created_at, history_id)
            VALUES
                ('loop', 1, 'loop/1.md', 'alpha', 'hash-canonical-a', '2026-07-12T00:00:00.0000000Z', 'history-a'),
                ('loop', 2, 'loop/2.md', 'beta', 'hash-canonical-b', '2026-07-12T00:00:01.0000000Z', 'history-b'),
                ('decisions', 1, 'decisions/1.md', 'same', 'hash-repeated', '2026-07-12T00:00:02.0000000Z', NULL),
                ('decisions', 2, 'decisions/2.md', 'same', 'hash-repeated', '2026-07-12T00:00:03.0000000Z', NULL);
            """);

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarStringAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? scalar = await command.ExecuteScalarAsync();
        return scalar is null or DBNull ? null : Convert.ToString(scalar);
    }

    private static async Task<long> ScalarLongAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> IndexExistsAsync(SqliteConnection connection, string index)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $index;";
        command.Parameters.AddWithValue("$index", index);
        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task<bool> IndexIsUniqueAsync(SqliteConnection connection, string index) =>
        await ScalarLongAsync(
            connection,
            $"SELECT COUNT(*) FROM pragma_index_list('loop_history') WHERE name = '{index}' AND \"unique\" = 1;") == 1;

    private static async Task<bool> IndexIsPartialAsync(SqliteConnection connection, string index) =>
        await ScalarLongAsync(
            connection,
            $"SELECT COUNT(*) FROM pragma_index_list('loop_history') WHERE name = '{index}' AND partial = 1;") == 1;
}
