using LoopRelay.Cli.Services.Import;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Import;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Import;

public sealed class CanonicalImportGatewayTests
{
    [Fact]
    public async Task Legacy_continuity_import_preserves_domain_rows_and_retires_runtime_source()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-import-legacy").FullName;
        try
        {
            Repository repository = new() { Id = Guid.NewGuid(), Name = "fixture", Path = root };
            string database = LoopRelayWorkspaceDatabase.Resolve(repository);
            _ = await new WorkspaceSchemaMigrationExecutor().ExecuteAsync(database, CancellationToken.None);
            await using (SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenMigrationTarget(database))
            {
                await connection.OpenAsync();
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO decision_session_scopes(
                        scope_id,workspace_id,workflow_identity,prepared_epic_causal_id,
                        executable_plan_causal_id,session_role,contract_version,lifecycle_state,created_at
                    ) VALUES('scope_legacyfixture','workspace_fixture','Execute','epic_fixture',
                        'plan_fixture','decision','1','Active','2026-01-01T00:00:00Z');
                    DELETE FROM schema_metadata WHERE key IN ('schema_identity','schema_family');
                    UPDATE schema_metadata SET value='3' WHERE key='schema_version';
                    DROP TABLE workspace_identity;
                    """;
                await command.ExecuteNonQueryAsync();
            }
            var gateway = new CanonicalImportGateway(repository);
            ImportResult detected = await gateway.DetectAsync(root);
            ImportResult previewed = await gateway.PreviewAsync(detected.Detection!.Identity);
            ImportPreview preview = previewed.Preview!;
            _ = await gateway.ApproveAsync(new ImportApproval(preview.Identity,
                preview.Detection.SourceFingerprint, "test-operator", ["authenticated", "mutation-authorized"],
                null, DateTimeOffset.UtcNow));

            ImportResult completed = await gateway.ExecuteAsync(preview.Identity);

            Assert.True(completed.Lifecycle == ImportLifecycle.Completed,
                $"{completed.Lifecycle}: {completed.Explanation} [{string.Join(", ", completed.Evidence)}]");
            Assert.Equal(ImportSourceKind.LegacyContinuityV3, completed.Detection!.SourceKind);
            Assert.True(CanonicalSourceAuthorityGuard.IsCanonicalOnly(database));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(database)!,
                "looprelay.sqlite3.legacy-nonauthoritative-*"));
            await using (SqliteConnection canonical = WorkspaceDatabaseConnectionFactory.OpenReadOnly(database))
            {
                await canonical.OpenAsync();
                await using SqliteCommand count = canonical.CreateCommand();
                count.CommandText = "SELECT COUNT(*) FROM decision_session_scopes WHERE scope_id='scope_legacyfixture';";
                Assert.Equal(1L, Convert.ToInt64(await count.ExecuteScalarAsync()));
            }
            SqliteConnection.ClearAllPools();
        }
        finally { await DeleteRootAsync(root); }
    }

    [Fact]
    public async Task Approved_filesystem_import_promotes_verified_authority_and_reuses_receipt()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-import-gateway").FullName;
        try
        {
            Write(root, ".agents/plan.md", "# executable plan");
            Write(root, ".agents/details.md", "# details");
            Write(root, ".agents/state.json", "{\"state\":\"prepared\"}");
            Write(root, ".agents/decisions.json", "[]");
            Write(root, ".agents/artifacts/lifecycle.json", "{}");
            Write(root, ".agents/splits.json", "[]");
            Write(root, ".agents/projections/manifest.json", "{}");
            Write(root, ".agents/journal/transitions.jsonl", "{}");
            Write(root, ".agents/execution-preparation.json", "{}");
            Write(root, ".agents/milestones/m1.md", "- [x] imported");
            Write(root, ".agents/decision-session.json", "{}");
            Write(root, ".agents/history/0001.md", "history");
            Write(root, ".agents/handoff.md", "handoff");
            Write(root, ".agents/evidence.md", "evidence");
            Write(root, ".agents/archive/epics/1/evidence.md", "completed");
            Repository repository = new() { Id = Guid.NewGuid(), Name = "fixture", Path = root };
            var gateway = new CanonicalImportGateway(repository);

            ImportResult detected = await gateway.DetectAsync(root);
            ImportResult previewed = await gateway.PreviewAsync(detected.Detection!.Identity);
            ImportPreview preview = previewed.Preview!;
            ImportResult approved = await gateway.ApproveAsync(new ImportApproval(
                preview.Identity, preview.Detection.SourceFingerprint, "test-operator",
                ["authenticated", "mutation-authorized"], null, DateTimeOffset.UtcNow));
            ImportResult completed = await new CanonicalImportGateway(repository).ExecuteAsync(preview.Identity);
            ImportResult repeated = await new CanonicalImportGateway(repository).ExecuteAsync(preview.Identity);

            Assert.Equal(ImportLifecycle.Approved, approved.Lifecycle);
            Assert.Equal(ImportLifecycle.Completed, completed.Lifecycle);
            Assert.Equal(completed.Receipt!.Identity, repeated.Receipt!.Identity);
            string database = LoopRelayWorkspaceDatabase.Resolve(repository);
            Assert.True(CanonicalSourceAuthorityGuard.IsCanonicalOnly(database));
            Assert.Throws<InvalidOperationException>(() =>
                CanonicalSourceAuthorityGuard.RejectLegacyReader(database, "disabled-fixture-reader"));
            Assert.True(Directory.Exists(Path.Combine(root, ".LoopRelay", "import-staging")));
            Assert.Contains(completed.Receipt.Evidence, value => value.StartsWith("effectreceipt_", StringComparison.Ordinal));

            var adapter = new ImportAdapterDescriptor("planning-artifacts", "1", ImportSourceKind.PlanningArtifacts,
                ["plan"], ["canonical-product"], [], ["partial-plan"], "fixture parity");
            var importStore = new CanonicalImportStore(database);
            await importStore.RecordAdapterExhaustionAsync(adapter, "portfolio-v1",
                new Dictionary<string, string> { ["partial-plan"] = completed.Receipt.Identity.Value },
                new Dictionary<string, string> { ["partial-plan"] = "canonical-only-pass" },
                new Dictionary<string, string> { ["partial-plan"] = "adapter-disabled-pass" },
                CancellationToken.None);
            await importStore.RecordAdapterExhaustionAsync(adapter, "portfolio-v2",
                new Dictionary<string, string> { ["partial-plan"] = completed.Receipt.Identity.Value },
                new Dictionary<string, string> { ["partial-plan"] = "canonical-only-pass" },
                new Dictionary<string, string> { ["partial-plan"] = "adapter-disabled-pass" },
                CancellationToken.None);
            ImportAdapterExhaustion exhaustion = (await importStore.ReadActiveAdapterExhaustionAsync(
                adapter.AdapterIdentity, adapter.Version, CancellationToken.None))!;
            Assert.Equal("portfolio-v2", exhaustion.PortfolioFingerprint);

            await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenReadOnly(database);
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM canonical_import_receipts;";
            Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            command.CommandText = "SELECT DISTINCT domain FROM canonical_import_mappings ORDER BY domain;";
            var domains = new List<string>();
            await using (SqliteDataReader reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) domains.Add(reader.GetString(0));
            Assert.Contains("roadmap-state", domains);
            Assert.Contains("decision-ledger", domains);
            Assert.Contains("artifact-lifecycle", domains);
            Assert.Contains("split-family-order", domains);
            Assert.Contains("selection-provenance", domains);
            Assert.Contains("projection-manifests", domains);
            Assert.Contains("execution-preparation", domains);
            Assert.Contains("transition-journal", domains);
            Assert.Contains("milestones", domains);
            Assert.Contains("decision-sessions", domains);
            Assert.Contains("numbered-history", domains);
            Assert.Contains("completion-archives", domains);
        }
        finally { await DeleteRootAsync(root); }
    }

    [Fact]
    public async Task Source_change_invalidates_preview_before_approval_or_canonical_write()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-import-stale").FullName;
        try
        {
            Write(root, ".agents/plan.md", "v1");
            Repository repository = new() { Id = Guid.NewGuid(), Name = "fixture", Path = root };
            var gateway = new CanonicalImportGateway(repository);
            ImportResult detected = await gateway.DetectAsync(root);
            ImportResult previewed = await gateway.PreviewAsync(detected.Detection!.Identity);
            Write(root, ".agents/plan.md", "v2");

            ImportResult result = await gateway.ApproveAsync(new ImportApproval(
                previewed.Preview!.Identity, previewed.Preview.Detection.SourceFingerprint, "test-operator",
                ["authenticated"], null, DateTimeOffset.UtcNow));

            Assert.Equal(ImportLifecycle.Refused, result.Lifecycle);
            Assert.False(File.Exists(LoopRelayWorkspaceDatabase.Resolve(repository)));
        }
        finally { await DeleteRootAsync(root); }
    }

    [Fact]
    public async Task Ambiguous_detection_persists_an_import_conflict_interaction_and_never_previews()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-import-conflict").FullName;
        try
        {
            Write(root, ".agents/plan.md", "legacy");
            Repository repository = new() { Id = Guid.NewGuid(), Name = "fixture", Path = root };
            _ = await new WorkspaceSchemaMigrationExecutor().ExecuteAsync(
                LoopRelayWorkspaceDatabase.Resolve(repository), CancellationToken.None);
            var gateway = new CanonicalImportGateway(repository);

            ImportResult result = await gateway.DetectAsync(root);

            Assert.Equal(ImportLifecycle.ApprovalRequired, result.Lifecycle);
            Assert.Equal(ImportSourceKind.Ambiguous, result.Detection!.SourceKind);
            Assert.Contains(result.Evidence, value => value.StartsWith("interaction:", StringComparison.Ordinal));
        }
        finally { await DeleteRootAsync(root); }
    }

    private static void Write(string root, string relative, string content)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static async Task DeleteRootAsync(string root)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(50);
            }
        }
    }
}
