using System.Security.Cryptography;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Orchestration.Tests.Storage;

/// <summary>
/// Certifies the two verification tiers introduced for PERF W3-2: a <b>light</b> tier that routine
/// observation pays for on every kernel cycle, and the <b>deep</b> tier that the explicit
/// <c>storage</c> commands keep. The light tier may defer deep checks; it may never stop detecting
/// the conditions that refuse mutation.
/// </summary>
public sealed class WorkspaceStorageVerificationTierTests
{
    [Fact]
    public async Task Light_verification_detects_a_missing_database()
    {
        Repository repository = CreateRepository();
        string persistence = Path.GetDirectoryName(LoopRelayWorkspaceDatabase.Resolve(repository))!;
        Directory.CreateDirectory(persistence);
        await File.WriteAllTextAsync(Path.Combine(persistence, "stray.bin"), "stray");

        StorageInspection light = await new WorkspaceStorageInspector().VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Light));

        Assert.NotEqual(StorageHealth.Healthy, light.Health);
        Assert.Equal(StorageHealth.ActionRequired, light.Health);
        Assert.False(light.Exists);
        Assert.Contains(".LoopRelay/persistence/stray.bin", light.Evidence);
        Assert.Contains(light.RequiredActions, action => action.Contains("storage init", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Light_verification_detects_an_interrupted_journal_marker()
    {
        Repository repository = CreateRepository();
        await CreateCanonicalAsync(repository);
        string persistence = Path.GetDirectoryName(LoopRelayWorkspaceDatabase.Resolve(repository))!;
        // Both interrupted-artifact kinds Interrupted() recognizes. The journal marker is
        // deliberately not the live database's own hot journal: a hot journal makes SQLite refuse
        // the read-only open, which is a different (corrupt) verdict on both tiers and would hide
        // the name-listing behaviour under test.
        await File.WriteAllTextAsync(
            Path.Combine(persistence, "looprelay.sqlite3.storageop-abc.storage-stage"), "staged");
        await File.WriteAllTextAsync(Path.Combine(persistence, "archived.sqlite3-journal"), "interrupted");

        StorageInspection light = await new WorkspaceStorageInspector().VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Light));

        Assert.NotEqual(StorageHealth.Healthy, light.Health);
        Assert.Contains(".LoopRelay/persistence/looprelay.sqlite3.storageop-abc.storage-stage",
            light.InterruptedOperations);
        Assert.Contains(".LoopRelay/persistence/archived.sqlite3-journal", light.InterruptedOperations);
        Assert.Contains(light.RequiredActions,
            action => action.Contains("interrupted storage operation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Light_verification_detects_a_malformed_stamp()
    {
        Repository repository = CreateRepository();
        await CreateCanonicalAsync(repository);
        string database = LoopRelayWorkspaceDatabase.Resolve(repository);
        await ExecuteAsync(database, "UPDATE schema_metadata SET value='not-looprelay' WHERE key='schema_identity';");

        StorageInspection light = await new WorkspaceStorageInspector().VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Light));
        StorageInspection deep = await new WorkspaceStorageInspector().VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Deep));

        // The stamp is no longer well-formed for the current canonical contract, so the light tier
        // must fall back to the full classification rather than answering from the stamp - and it
        // must reach exactly the same non-healthy verdict the deep tier reaches.
        Assert.NotEqual(StorageHealth.Healthy, light.Health);
        Assert.Equal(deep.Health, light.Health);
        Assert.Equal(deep.Schema, light.Schema);
        Assert.Equal(deep.Evidence, light.Evidence);
    }

    [Fact]
    public async Task Light_verification_hashes_only_the_database_file()
    {
        Repository repository = CreateRepository();
        await CreateCanonicalAsync(repository);
        await WriteCompanionFilesAsync(repository);
        var inspector = new WorkspaceStorageInspector();

        StorageInspection light = await inspector.VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Light));

        // Three files live under the persistence directory; the light tier owes the observation
        // path only the database's own digest (it feeds the bit-identical `bytes-sha256:` evidence
        // line), and a name listing for journal-artifact detection. Reverting the tier split makes
        // this three.
        Assert.Equal(3, light.PersistenceTree.Count);
        Assert.Equal(1, inspector.FileHashInvocations);
        Assert.All(light.PersistenceTree, entry => Assert.Null(entry.Sha256));
    }

    [Fact]
    public async Task Deep_verification_hashes_every_persistence_file()
    {
        Repository repository = CreateRepository();
        await CreateCanonicalAsync(repository);
        await WriteCompanionFilesAsync(repository);
        var inspector = new WorkspaceStorageInspector();

        StorageInspection deep = await inspector.VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Deep));

        Assert.Equal(3, deep.PersistenceTree.Count);
        Assert.Equal(deep.PersistenceTree.Count, inspector.FileHashInvocations);
        Assert.All(deep.PersistenceTree, entry => Assert.NotNull(entry.Sha256));
    }

    [Fact]
    public async Task Deep_verification_is_the_default_for_an_unqualified_request()
    {
        Repository repository = CreateRepository();
        await CreateCanonicalAsync(repository);
        await WriteCompanionFilesAsync(repository);
        var inspector = new WorkspaceStorageInspector();

        StorageInspection unqualified = await inspector.VerifyAsync(new(repository.Path));

        Assert.Equal(StorageVerificationDepth.Deep, new StorageVerifyRequest(repository.Path).Depth);
        // Deep is also the ZERO value of the enum, so a depth that arrives without naming a member
        // (default(...), deserialization, a zero-initialized field) lands on the thorough tier.
        Assert.Equal(StorageVerificationDepth.Deep, default(StorageVerificationDepth));
        // Pin the fixture's tree size before comparing it to the hash count: the database plus the
        // two companion files. Without this, an empty inventory would satisfy the equality below as
        // 0 == 0 and the assertion would certify nothing.
        Assert.Equal(3, unqualified.PersistenceTree.Count);
        Assert.Equal(unqualified.PersistenceTree.Count, inspector.FileHashInvocations);
    }

    [Fact]
    public async Task Light_and_deep_agree_on_evidence_for_a_healthy_authority()
    {
        Repository repository = CreateRepository();
        await CreateCanonicalAsync(repository);
        await WriteCompanionFilesAsync(repository);

        StorageInspection light = await new WorkspaceStorageInspector().VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Light));
        StorageInspection deep = await new WorkspaceStorageInspector().VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Deep));

        // Evidence bit-identity is binding: the observation's StorageAuthoritySnapshot.Evidence is
        // serialized into OrchestrationKernel.Snapshot and hashed into the durable
        // canonical_kernel_decisions.snapshot_identity column. The light tier must therefore keep
        // producing the same six lines, `bytes-sha256:` included.
        Assert.Equal(deep.Evidence, light.Evidence);
        Assert.Equal(deep.Health, light.Health);
        Assert.Equal(deep.Schema, light.Schema);
        Assert.Equal(deep.ByteSha256, light.ByteSha256);
        Assert.Equal(deep.ByteLength, light.ByteLength);
        Assert.Equal(deep.RequiredActions, light.RequiredActions);
        Assert.Equal(deep.InterruptedOperations, light.InterruptedOperations);
    }

    [Fact]
    public async Task Deep_verification_output_is_pinned_for_a_healthy_authority()
    {
        Repository repository = CreateRepository();
        await CreateCanonicalAsync(repository);
        string database = LoopRelayWorkspaceDatabase.Resolve(repository);
        string expectedHash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(database)));

        StorageInspection deep = await new WorkspaceStorageInspector().VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Deep));

        Assert.Equal(StorageHealth.Healthy, deep.Health);
        Assert.True(deep.Exists);
        Assert.Equal(new FileInfo(database).Length, deep.ByteLength);
        Assert.Equal(expectedHash, deep.ByteSha256);
        Assert.Empty(deep.UnresolvedReferences);
        Assert.Empty(deep.InterruptedOperations);
        Assert.Empty(deep.RequiredActions);
        Assert.Equal(
            [
                $"schema:{LoopRelayWorkspaceDatabase.SchemaIdentity}",
                $"family:{WorkspaceSchemaFamily.CanonicalWorkspace}",
                $"version:{LoopRelayWorkspaceDatabase.CurrentSchemaVersion}",
                $"shape:{WorkspaceSchemaShape.CanonicalV16Complete}",
                $"shape-fingerprint:{LoopRelayWorkspaceDatabase.CanonicalV16ShapeFingerprint}",
                $"bytes-sha256:{expectedHash}",
            ],
            deep.Evidence);
    }

    [Fact]
    public async Task Deep_and_light_verification_output_is_pinned_for_a_corrupt_authority()
    {
        Repository repository = CreateRepository();
        string database = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        await File.WriteAllTextAsync(database, "not sqlite");
        string expectedHash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(database)));

        StorageInspection deep = await new WorkspaceStorageInspector().VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Deep));
        StorageInspection light = await new WorkspaceStorageInspector().VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Light));

        Assert.Equal(StorageHealth.Corrupt, deep.Health);
        Assert.Equal(StorageHealth.Corrupt, light.Health);
        Assert.Equal(expectedHash, deep.ByteSha256);
        Assert.Equal(expectedHash, light.ByteSha256);
        Assert.Equal([nameof(SqliteException), "SQLite authority is unreadable."], deep.Evidence);
        Assert.Equal(deep.Evidence, light.Evidence);
        Assert.Equal(deep.RequiredActions, light.RequiredActions);
    }

    [Fact]
    public async Task Deep_verification_still_reports_foreign_key_violations_the_light_tier_defers()
    {
        Repository repository = CreateRepository();
        await CreateCanonicalAsync(repository);
        string database = LoopRelayWorkspaceDatabase.Resolve(repository);
        // PRAGMA foreign_keys must be turned OFF to manufacture the violation at all: every
        // write-side connection in this codebase opens with foreign_keys ON (EnsureSchemaAsync sets
        // it on every call), so a dangling reference cannot be introduced through the mutation path.
        await ExecuteAsync(database, """
            PRAGMA foreign_keys = OFF;
            CREATE TABLE tier_fk_parent(id INTEGER PRIMARY KEY);
            CREATE TABLE tier_fk_child(id INTEGER PRIMARY KEY, parent INTEGER REFERENCES tier_fk_parent(id));
            INSERT INTO tier_fk_child(id, parent) VALUES (1, 999);
            """);

        StorageInspection deep = await new WorkspaceStorageInspector().VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Deep));
        StorageInspection light = await new WorkspaceStorageInspector().VerifyAsync(
            new(repository.Path, StorageVerificationDepth.Light));

        // The documented, sanctioned deferral: `PRAGMA foreign_key_check` is a deep check, and the
        // plan permits tiering to defer deep checks (never mutation guards). No mutation guard
        // consults UnresolvedReferences - the storage commands that refuse on it
        // (migrate/export/sync) all run the deep tier.
        //
        // What is true about the write path, scoped honestly: LoopRelay cannot *create* a dangling
        // reference through its own writes, because EnsureSchemaAsync executes `PRAGMA
        // foreign_keys = ON` on every write connection and SQLite then refuses the offending
        // INSERT/UPDATE/DELETE. That is why this fixture needs an explicit `foreign_keys = OFF`
        // above to manufacture the violation at all.
        //
        // What is NOT true - and must not be inferred from the above - is that violations are
        // "refused at write time regardless of what an observation reported". SQLite enforces
        // foreign keys PER STATEMENT; `foreign_keys = ON` does not validate a database that already
        // holds dangling references, and opening such a database succeeds. So for violations
        // introduced from outside LoopRelay's write path - a compatibility import, a restore from
        // backup, a hand-edited database file, or a database migrated up from a schema version that
        // did not declare the constraint - nothing refuses them at write time.
        //
        // Residual, accepted and recorded: for such a database the light tier reports Healthy, so
        // StorageVerificationResult.UsableAuthority is true and WorkflowEntryGateEvaluator
        // (WorkflowChaining.cs) no longer refuses workflow entry on it. The violation goes
        // UNDETECTED on the routine-observation path until someone runs `storage verify`, which is
        // the deep tier asserted below. Detection is deferred, not preserved.
        Assert.NotEmpty(deep.UnresolvedReferences);
        Assert.Equal(StorageHealth.ActionRequired, deep.Health);
        Assert.Contains(deep.RequiredActions,
            action => action.Contains("foreign-key references", StringComparison.Ordinal));
        Assert.Empty(light.UnresolvedReferences);
    }

    [Fact]
    public async Task Routine_observation_requests_the_light_tier()
    {
        Repository repository = CreateRepository();
        await CreateCanonicalAsync(repository);
        await WriteCompanionFilesAsync(repository);
        var inspector = new WorkspaceStorageInspector();

        StorageVerificationResult verification = await new WorkspaceStorageVerifierAdapter(inspector)
            .VerifyAsync(repository.Path, CancellationToken.None);

        // The adapter is the only thing between the inspector and RepositoryObserver, so this is
        // the wiring assertion for "routine observation pays the light tier". Restoring the deep
        // request here makes the count three.
        Assert.Equal(1, inspector.FileHashInvocations);
        Assert.Equal(StorageAuthorityKind.CanonicalSqlite, verification.Authority);
        Assert.True(verification.UsableAuthority);
    }

    private static async Task WriteCompanionFilesAsync(Repository repository)
    {
        string persistence = Path.GetDirectoryName(LoopRelayWorkspaceDatabase.Resolve(repository))!;
        await File.WriteAllTextAsync(Path.Combine(persistence, "companion-one.bin"), "one");
        await File.WriteAllTextAsync(Path.Combine(persistence, "companion-two.bin"), "two");
    }

    private static async Task CreateCanonicalAsync(Repository repository)
    {
        string path = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenMigrationTarget(path);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
    }

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("storage-tier-").FullName;
        return new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
    }

    private static async Task ExecuteAsync(string databasePath, string sql)
    {
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenMigrationTarget(databasePath);
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
