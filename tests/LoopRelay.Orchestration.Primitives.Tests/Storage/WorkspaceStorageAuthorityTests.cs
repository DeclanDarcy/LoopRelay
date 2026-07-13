using System.Security.Cryptography;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Orchestration.Tests.Storage;

public sealed class WorkspaceStorageAuthorityTests
{
    [Fact]
    public async Task Repeated_verify_is_byte_and_tree_inventory_preserving()
    {
        Repository repository = CreateRepository();
        await CreateCanonicalAsync(repository);
        string path = LoopRelayWorkspaceDatabase.Resolve(repository);
        byte[] before = await File.ReadAllBytesAsync(path);
        var inspector = new WorkspaceStorageInspector();

        StorageInspection first = await inspector.VerifyAsync(new(repository.Path));
        StorageInspection second = await inspector.VerifyAsync(new(repository.Path));
        byte[] after = await File.ReadAllBytesAsync(path);

        Assert.Equal(StorageHealth.Healthy, first.Health);
        Assert.Equal(first.ByteSha256, second.ByteSha256);
        Assert.Equal(first.Schema, second.Schema);
        Assert.Equal(first.PersistenceTree, second.PersistenceTree);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(before)), Convert.ToHexString(SHA256.HashData(after)));
        Assert.Equal(before, after);
        Assert.DoesNotContain(second.PersistenceTree, item => item.RelativePath.EndsWith("-wal", StringComparison.Ordinal));
        Assert.DoesNotContain(second.PersistenceTree, item => item.RelativePath.EndsWith("-shm", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Recognized_v8_reports_migration_chain_without_mutation()
    {
        Repository repository = CreateRepository();
        string path = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenMigrationTarget(path))
        {
            await connection.OpenAsync();
            await ExecuteAsync(connection, "CREATE TABLE schema_metadata(key text primary key, value text not null); INSERT INTO schema_metadata VALUES ('schema_version','8');");
        }
        byte[] before = await File.ReadAllBytesAsync(path);

        StorageInspection result = await new WorkspaceStorageInspector().VerifyAsync(new(repository.Path));

        Assert.Equal(StorageHealth.ActionRequired, result.Health);
        Assert.Equal(8, result.Schema!.Version);
        Assert.Contains(result.RequiredActions, action => action.Contains("9 -> 10 -> 11 -> 12 -> 13 -> 14 -> 15", StringComparison.Ordinal));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Corrupt_bytes_are_typed_and_never_repaired()
    {
        Repository repository = CreateRepository();
        string path = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "not sqlite");
        byte[] before = await File.ReadAllBytesAsync(path);

        StorageInspection result = await new WorkspaceStorageInspector().VerifyAsync(new(repository.Path));

        Assert.Equal(StorageHealth.Corrupt, result.Health);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Storage_operation_plan_and_events_are_restart_discoverable()
    {
        Repository repository = CreateRepository();
        await CreateCanonicalAsync(repository);
        var store = new CanonicalStorageOperationStore(repository);
        StorageOperationPlan plan = Plan();
        await store.PersistPlanAsync(plan);
        await store.AppendEventAsync(new StorageOperationEvent(plan.Identity, StorageOperationLifecycle.Effecting,
            "effect started", ["effect-1"], DateTimeOffset.UtcNow));

        StorageOperationPlan restored = Assert.Single(await new CanonicalStorageOperationStore(repository).ReadInterruptedAsync());
        Assert.Equal(plan.Identity, restored.Identity);
        Assert.Equal(plan.SourceFingerprint, restored.SourceFingerprint);

        await store.PersistReceiptAsync(new StorageOperationReceipt(plan.Identity, "target", [], ["verified"], DateTimeOffset.UtcNow));
        Assert.Empty(await store.ReadInterruptedAsync());
    }

    [Fact]
    public async Task Semantic_export_rehydrates_fresh_and_compares_every_domain()
    {
        Repository source = CreateRepository();
        await CreateCanonicalAsync(source);
        string sourcePath = LoopRelayWorkspaceDatabase.Resolve(source);
        await using (SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenMigrationTarget(sourcePath))
        {
            await connection.OpenAsync();
            await ExecuteAsync(connection, "INSERT INTO workspace_metadata(key,value) VALUES ('semantic-null-test','unknown');");
        }
        var codec = new CanonicalStorageExportCodec();
        CanonicalStorageExportPackage package = await codec.ExportAsync(sourcePath);
        string encoded = codec.Encode(package);
        CanonicalStorageExportPackage decoded = codec.Decode(encoded);
        Assert.Equal(package.Manifest.PackageSha256, decoded.Manifest.PackageSha256);
        Assert.Equal(package.Manifest.LogicalFingerprint, decoded.Manifest.LogicalFingerprint);
        Assert.Empty(CanonicalStorageExportCodec.Compare(package, decoded));
        Repository target = CreateRepository();
        string targetPath = LoopRelayWorkspaceDatabase.Resolve(target);

        await codec.RehydrateFreshAsync(package, targetPath);
        CanonicalStorageExportPackage roundTrip = await codec.ExportAsync(targetPath);

        Assert.Empty(CanonicalStorageExportCodec.Compare(package, roundTrip));
        Assert.Equal(package.Manifest.LogicalFingerprint, roundTrip.Manifest.LogicalFingerprint);
        Assert.NotEmpty(package.Manifest.DomainHashes);
        Assert.Equal(package.Domains.Count, package.Manifest.DomainRowCounts.Count);
    }

    private static StorageOperationPlan Plan()
    {
        var causality = new CanonicalCausalContext(WorkspaceIdentity.New(), RunIdentity.New(),
            WorkflowInstanceIdentity.New(), TransitionRunIdentity.New(), AttemptIdentity.New());
        return new StorageOperationPlan(StorageOperationIdentity.New(), StorageOperationKind.Sync, causality,
            "source", "target", ["pre"], ["post"], $"sync:{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
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
        string path = Directory.CreateTempSubdirectory("storage-authority-").FullName;
        return new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
