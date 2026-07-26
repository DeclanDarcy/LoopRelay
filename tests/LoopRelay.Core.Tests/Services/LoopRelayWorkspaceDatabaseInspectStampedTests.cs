using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Tests.Services;

/// <summary>
/// Covers PERF-15: <see cref="LoopRelayWorkspaceDatabase.InspectStampedAsync"/> must answer from
/// the <c>schema_metadata</c> stamp alone on a well-formed canonical-v16 database - issuing none
/// of the ~190 structural probes <see cref="LoopRelayWorkspaceDatabase.InspectSchemaAsync"/> runs -
/// while remaining indistinguishable from full classification in its result, and falling back to
/// full classification whenever the stamp is absent, malformed, or internally inconsistent.
/// </summary>
[Collection("WorkspaceDatabaseCounters")]
public sealed class LoopRelayWorkspaceDatabaseInspectStampedTests
{
    [Fact]
    public async Task InspectStamped_OnStampedDb_IssuesNoTableProbes()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        int baseline = LoopRelayWorkspaceDatabase.ShapeRequirementProbes;
        WorkspaceSchemaInspection stamped =
            await LoopRelayWorkspaceDatabase.InspectStampedAsync(connection);

        Assert.Equal(WorkspaceSchemaShape.CanonicalV16Complete, stamped.Shape);
        Assert.Equal(baseline, LoopRelayWorkspaceDatabase.ShapeRequirementProbes);

        // Guards against a vacuous pass: if the counter never moved for anything, the assertion
        // above would hold no matter what InspectStampedAsync did. Full classification on the same
        // connection must move it, proving the counter observes the probes the fast path skipped.
        await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.True(
            LoopRelayWorkspaceDatabase.ShapeRequirementProbes > baseline,
            "InspectSchemaAsync must issue shape-requirement probes for the counter to be meaningful.");
    }

    [Fact]
    public async Task InspectStamped_OnStampedDb_MatchesFullClassificationFieldForField()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        WorkspaceSchemaInspection full = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        WorkspaceSchemaInspection stamped = await LoopRelayWorkspaceDatabase.InspectStampedAsync(connection);

        // WorkspaceSchemaInspection is a record, so this is field-for-field value equality -
        // including the diagnostic string and the observed shape fingerprint the fast path must
        // reproduce without probing for it.
        Assert.Equal(full, stamped);
    }

    [Fact]
    public async Task InspectStamped_OnUnstampedDb_FallsBackToFullClassification()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE schema_metadata(key text primary key, value text not null);
            INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '3');
            CREATE TABLE session_continuity_profiles(profile_digest text primary key);
            CREATE TABLE decision_session_scopes(scope_id text primary key);
            """);

        WorkspaceSchemaInspection full = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        WorkspaceSchemaInspection stamped = await LoopRelayWorkspaceDatabase.InspectStampedAsync(connection);

        Assert.Equal(WorkspaceSchemaFamily.LegacyContinuity, stamped.Family);
        Assert.Equal(full, stamped);
    }

    [Fact]
    public async Task InspectStamped_OnEmptyDb_FallsBackToFullClassification()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();

        WorkspaceSchemaInspection full = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        WorkspaceSchemaInspection stamped = await LoopRelayWorkspaceDatabase.InspectStampedAsync(connection);

        Assert.Equal(WorkspaceSchemaShape.Empty, stamped.Shape);
        Assert.Equal(full, stamped);
    }

    [Fact]
    public async Task InspectStamped_OnTamperedFingerprint_FallsBackToFullClassification()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        // The stamp now disagrees with the physical shape. Trusting it would report a healthy v16
        // database; full classification must run instead and reject it, because corrupt-stamp
        // detection has to keep blocking mutation.
        await ExecuteAsync(
            connection,
            "UPDATE schema_metadata SET value = 'tampered-fingerprint' WHERE key = 'schema_shape';");

        WorkspaceSchemaInspection full = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        WorkspaceSchemaInspection stamped = await LoopRelayWorkspaceDatabase.InspectStampedAsync(connection);

        Assert.Equal(WorkspaceSchemaShape.CorruptCanonicalV16, stamped.Shape);
        Assert.Equal(full, stamped);
    }

    [Fact]
    public async Task InspectStamped_TrustsStampOverPhysicalShape_IsTheDocumentedTradeOff()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        // A stamp that still claims a complete v16 shape while the physical shape underneath it has
        // lost a required table. This is the case the fast path cannot detect by construction (it
        // reads only the stamp), so it is recorded here as the deliberate, documented limit of the
        // trade-off rather than left as an unexamined gap: InspectStampedAsync reports healthy.
        await ExecuteAsync(connection, "DROP TABLE canonical_runtime_prerequisites;");

        WorkspaceSchemaInspection full = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        WorkspaceSchemaInspection stamped = await LoopRelayWorkspaceDatabase.InspectStampedAsync(connection);

        Assert.Equal(WorkspaceSchemaShape.CorruptCanonicalV16, full.Shape);
        Assert.Equal(WorkspaceSchemaShape.CanonicalV16Complete, stamped.Shape);
    }

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-inspect-stamped-").FullName;
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
