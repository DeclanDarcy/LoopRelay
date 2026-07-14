using LoopRelay.Orchestration.Import;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Tests.Import;

public sealed class ImportPortfolioDetectorTests
{
    [Fact]
    public async Task Planning_and_execute_portfolio_detection_is_read_only_and_fingerprints_all_owned_surfaces()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-import-detect").FullName;
        try
        {
            Write(root, ".agents/plan.md", "plan-v1");
            Write(root, ".agents/milestones/m1.md", "- [ ] work");
            Write(root, ".agents/history/0001.md", "history");
            IReadOnlyDictionary<string, byte[]> before = Snapshot(root);

            var detector = new ImportPortfolioDetector();
            ImportDetection detection = await detector.DetectAsync(root);
            ImportPreview preview = await detector.PreviewAsync(detection);
            IReadOnlyDictionary<string, byte[]> after = Snapshot(root);

            Assert.Equal(ImportSourceKind.CompositeOwnedWorkspace, detection.SourceKind);
            Assert.Contains(detection.Adapters, item => item.SourceKind == ImportSourceKind.PlanningArtifacts);
            Assert.Contains(detection.Adapters, item => item.SourceKind == ImportSourceKind.ExecuteArtifacts);
            Assert.Contains(".agents/milestones", detection.Evidence);
            Assert.Contains(preview.Mappings, item => item.Domain == "milestones");
            Assert.Contains(preview.Mappings, item => item.Domain == "numbered-history");
            Assert.Equal(before.Keys, after.Keys);
            foreach ((string path, byte[] bytes) in before) Assert.Equal(bytes, after[path]);

            Write(root, ".agents/milestones/m1.md", "- [x] work");
            ImportDetection changed = await detector.DetectAsync(root);
            Assert.NotEqual(detection.SourceFingerprint, changed.SourceFingerprint);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Unknown_and_overlapping_authorities_fail_closed()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-import-ambiguous").FullName;
        try
        {
            var detector = new ImportPortfolioDetector();
            ImportDetection unknown = await detector.DetectAsync(root);
            Assert.Equal(ImportSourceKind.Unknown, unknown.SourceKind);
            Assert.False(unknown.CanPreview);

            Write(root, ".agents/plan.md", "legacy plan");
            string database = Path.Combine(root,
                LoopRelay.Core.Services.Persistence.LoopRelayWorkspaceDatabase.RelativeDatabasePath
                    .Replace('/', Path.DirectorySeparatorChar));
            _ = await new LoopRelay.Core.Services.Persistence.WorkspaceSchemaMigrationExecutor()
                .ExecuteAsync(database, CancellationToken.None);
            Assert.True(File.Exists(database));
            LoopRelay.Core.Services.Persistence.WorkspaceSchemaInspection inspection =
                await new LoopRelay.Core.Services.Persistence.WorkspaceSchemaReadOnlyInspector()
                    .InspectAsync(database, CancellationToken.None);
            Assert.Equal(LoopRelay.Core.Services.Persistence.WorkspaceSchemaFamily.CanonicalWorkspace,
                inspection.Family);
            Assert.Equal(LoopRelay.Core.Services.Persistence.LoopRelayWorkspaceDatabase.CurrentSchemaVersion,
                inspection.Version);
            ImportDetection ambiguous = await detector.DetectAsync(root);
            Assert.Equal(ImportSourceKind.Ambiguous, ambiguous.SourceKind);
            Assert.Contains(ambiguous.Conflicts, value => value.Contains("current canonical authority", StringComparison.Ordinal));
            await Assert.ThrowsAsync<InvalidOperationException>(() => detector.PreviewAsync(ambiguous));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Canonical_v8_is_delegated_to_storage_convergence_and_malformed_packages_are_refused()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-import-convergence").FullName;
        try
        {
            string database = Path.Combine(root,
                LoopRelayWorkspaceDatabase.RelativeDatabasePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(database)!);
            await using (SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenMigrationTarget(database))
            {
                await connection.OpenAsync();
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE schema_metadata(key text primary key,value text not null);" +
                                      "INSERT INTO schema_metadata VALUES('schema_version','8');";
                await command.ExecuteNonQueryAsync();
            }
            var detector = new ImportPortfolioDetector();
            ImportDetection migration = await detector.DetectAsync(root);
            Assert.Equal(ImportSourceKind.CanonicalMigrationRequired, migration.SourceKind);
            Assert.Equal("storage-convergence", Assert.Single(migration.Adapters).AdapterIdentity);

            File.Delete(database);
            Write(root, ".LoopRelay/imports/broken.canonical.json", "{not-json");
            ImportDetection malformed = await detector.DetectAsync(root);
            Assert.Equal(ImportSourceKind.Unknown, malformed.SourceKind);
            Assert.Contains(malformed.UnsupportedFacts,
                value => value.StartsWith("malformed-canonical-package:", StringComparison.Ordinal));
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, recursive: true); }
    }

    private static void Write(string root, string relative, string content)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static IReadOnlyDictionary<string, byte[]> Snapshot(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);
}
