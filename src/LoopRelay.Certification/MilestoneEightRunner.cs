using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Certification;

public sealed class MilestoneEightRunner
{
    private static readonly string[] ExpectedTables =
    [
        "artifact_lifecycle", "canonical_blockers", "canonical_effect_records", "canonical_gate_evaluations",
        "canonical_product_records", "canonical_recovery_markers", "canonical_stage_states",
        "canonical_transition_evidence", "canonical_transition_runs", "canonical_workflow_chain_runs",
        "canonical_workflow_states", "completed_epic_archives", "completed_epic_records", "decision_ledger",
        "decision_session_active", "decision_session_legacy_imports", "decision_session_lineage",
        "decision_session_resume", "decision_session_scopes", "decision_session_turns", "execution_evidence",
        "execution_preparation_manifest", "loop_history", "projection_manifest_entries", "roadmap_state",
        "schema_metadata", "selection_provenance_manifest", "session_continuity_profiles",
        "session_recovery_attempts", "session_recovery_plans", "session_recovery_sources",
        "session_telemetry_events", "session_transition_correlations", "split_families",
        "split_family_children", "split_family_dependency_order", "sync_markers", "transition_journal",
        "workflow_transactions", "workspace_metadata",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<MilestoneEightCertificationResult> RunAsync(
        string cliPath,
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        string root = Path.Combine(authorityRoot, "milestone-8", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var cases = new List<PersistenceLifecycleCaseResult>();
        IReadOnlyList<string> tables = [];
        IReadOnlyDictionary<string, IReadOnlyList<string>> columns =
            new Dictionary<string, IReadOnlyList<string>>();
        try
        {
            string healthy = NewCase(root, "healthy-inventory");
            ProcessResult init = await RunCliAsync(cliPath, healthy, ["storage", "init"], cancellationToken);
            string database = Database(healthy);
            (tables, columns) = await InspectSchemaAsync(database, cancellationToken);
            bool exactInventory = tables.SequenceEqual(ExpectedTables, StringComparer.Ordinal) &&
                columns.Count == ExpectedTables.Length && columns.All(pair => pair.Value.Count > 0);
            string workspaceIdBefore = await WorkspaceIdAsync(database, cancellationToken);
            ProcessResult sync = await RunCliAsync(cliPath, healthy, ["storage", "sync"], cancellationToken);
            string workspaceIdAfter = await WorkspaceIdAsync(database, cancellationToken);
            cases.Add(Case(
                "schema-and-workspace-identity-inventory",
                true,
                nonMutating: false,
                init.ExitCode == 0 && sync.ExitCode == 0 && exactInventory &&
                    workspaceIdBefore == workspaceIdAfter && workspaceIdBefore.Length == 32,
                $"tables:{tables.Count}",
                $"columns:{columns.Sum(pair => pair.Value.Count)}",
                $"schema-version:{await SchemaVersionAsync(database, cancellationToken)}",
                $"workspace-id-digest:{Hash(workspaceIdBefore)}"));

            Dictionary<string, string> verifyBefore = SnapshotFiles(healthy);
            ProcessResult verify = await RunCliAsync(cliPath, healthy, ["storage", "verify"], cancellationToken);
            Dictionary<string, string> verifyAfter = SnapshotFiles(healthy);
            bool verifyStable = SameSnapshot(verifyBefore, verifyAfter);
            cases.Add(Case(
                "public-verify-byte-for-byte-non-mutating",
                true,
                verifyStable,
                verify.ExitCode == 0 && verifyStable,
                $"files:{verifyBefore.Count}",
                $"exit:{verify.ExitCode}"));

            Dictionary<string, string> exportBefore = SnapshotFiles(healthy);
            ProcessResult export = await RunCliAsync(cliPath, healthy, ["storage", "export"], cancellationToken);
            bool exportStable = SameSnapshot(exportBefore, SnapshotFiles(healthy));
            cases.Add(Case(
                "declared-narrow-export-is-explicit-no-op",
                true,
                exportStable,
                export.ExitCode == 0 && exportStable &&
                    export.StandardOutput.Contains("no filesystem mutations", StringComparison.Ordinal),
                $"exit:{export.ExitCode}",
                $"stable:{exportStable}"));

            string imported = NewCase(root, "missing-import");
            ProcessResult import = await RunCliAsync(cliPath, imported, ["storage", "import"], cancellationToken);
            bool importCreated = File.Exists(Database(imported)) && await SchemaVersionAsync(Database(imported), cancellationToken) == 3;
            cases.Add(Case(
                "public-import-creates-current-canonical-authority",
                true,
                nonMutating: false,
                import.ExitCode == 0 && importCreated,
                $"exit:{import.ExitCode}",
                $"created:{importCreated}"));

            string unsupported = await InitializedCaseAsync(root, cliPath, "unsupported-schema", cancellationToken);
            await ExecuteAsync(Database(unsupported),
                "UPDATE schema_metadata SET value = '999' WHERE key = 'schema_version';", cancellationToken);
            string unsupportedBefore = Hash(await File.ReadAllBytesAsync(Database(unsupported), cancellationToken));
            ProcessResult unsupportedVerify = await RunCliAsync(cliPath, unsupported, ["storage", "verify"], cancellationToken);
            string unsupportedAfter = Hash(await File.ReadAllBytesAsync(Database(unsupported), cancellationToken));
            bool unsupportedStable = unsupportedBefore == unsupportedAfter;
            cases.Add(Case(
                "unsupported-schema-blocks-without-repair",
                true,
                unsupportedStable,
                unsupportedVerify.ExitCode == 4 && unsupportedStable &&
                    unsupportedVerify.StandardOutput.Contains("999", StringComparison.Ordinal),
                $"exit:{unsupportedVerify.ExitCode}",
                $"stable:{unsupportedStable}"));

            string corrupt = await InitializedCaseAsync(root, cliPath, "corrupt-database", cancellationToken);
            await File.WriteAllBytesAsync(Database(corrupt), [0x4c, 0x52, 0x00, 0xff, 0x01], cancellationToken);
            string corruptBefore = Hash(await File.ReadAllBytesAsync(Database(corrupt), cancellationToken));
            ProcessResult corruptVerify = await RunCliAsync(cliPath, corrupt, ["storage", "verify"], cancellationToken);
            string corruptAfter = Hash(await File.ReadAllBytesAsync(Database(corrupt), cancellationToken));
            bool corruptStable = corruptBefore == corruptAfter;
            cases.Add(Case(
                "corrupt-authority-blocks-without-repair",
                true,
                corruptStable,
                corruptVerify.ExitCode == 4 && corruptStable &&
                    corruptVerify.StandardOutput.Contains("Corruption:", StringComparison.Ordinal),
                $"exit:{corruptVerify.ExitCode}",
                $"stable:{corruptStable}"));

            string partial = await InitializedCaseAsync(root, cliPath, "partial-transaction", cancellationToken);
            await ExecuteAsync(Database(partial), """
                INSERT INTO workflow_transactions (
                    transaction_id, workflow_name, correlation_id, status, started_at, completed_at, marker_json
                ) VALUES ('tx-m8', 'Plan', 'm8', 'Started', '2026-07-10T00:00:00Z', NULL, '{}');
                """, cancellationToken);
            Dictionary<string, string> partialBefore = SnapshotFiles(partial);
            ProcessResult partialVerify = await RunCliAsync(cliPath, partial, ["storage", "verify"], cancellationToken);
            bool partialStable = SameSnapshot(partialBefore, SnapshotFiles(partial));
            cases.Add(Case(
                "partial-transaction-blocks-non-mutating",
                true,
                partialStable,
                partialVerify.ExitCode == 4 && partialStable &&
                    partialVerify.StandardOutput.Contains("tx-m8", StringComparison.Ordinal),
                $"exit:{partialVerify.ExitCode}",
                $"stable:{partialStable}"));

            string concurrent = await InitializedCaseAsync(root, cliPath, "concurrent-writers", cancellationToken);
            Task[] writers = Enumerable.Range(0, 8)
                .Select(index => WriteMetadataAsync(Database(concurrent), index, cancellationToken))
                .ToArray();
            await Task.WhenAll(writers);
            long writerCount = await ScalarAsync(
                Database(concurrent), "SELECT COUNT(*) FROM workspace_metadata WHERE key LIKE 'm8.writer.%';", cancellationToken);
            ProcessResult concurrentVerify = await RunCliAsync(cliPath, concurrent, ["storage", "verify"], cancellationToken);
            cases.Add(Case(
                "concurrent-writers-preserve-all-logical-rows",
                true,
                nonMutating: false,
                writerCount == writers.Length && concurrentVerify.ExitCode == 0,
                $"writers:{writers.Length}",
                $"rows:{writerCount}",
                $"verify-exit:{concurrentVerify.ExitCode}"));

            IReadOnlyList<string> privacy = PrivacyScanner.Scan(
                string.Join("\n", cases.SelectMany(item => item.Evidence)), authorityRoot);
            CertificationClassification classification = cases.All(item => item.Passed) && privacy.Count == 0
                ? CertificationClassification.Passed
                : CertificationClassification.ProductRegression;
            var result = new MilestoneEightCertificationResult(
                CertificationRunner.ResultSchemaVersion, classification, tables, columns, cases, privacy);
            string evidencePath = Path.Combine(authorityRoot, "evidence", "milestone-8.latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
            await using FileStream stream = File.Create(evidencePath);
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
            return result;
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static PersistenceLifecycleCaseResult Case(
        string identity, bool publicPath, bool nonMutating, bool passed, params string[] evidence) =>
        new(identity, publicPath, nonMutating, passed, evidence);

    private static string NewCase(string root, string name)
    {
        string path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "README.md"), $"# {name}\n");
        return path;
    }

    private static async Task<string> InitializedCaseAsync(
        string root, string cli, string name, CancellationToken token)
    {
        string path = NewCase(root, name);
        ProcessResult init = await RunCliAsync(cli, path, ["storage", "init"], token);
        if (init.ExitCode != 0) throw new InvalidOperationException($"storage init failed for {name}");
        return path;
    }

    private static string Database(string repository) => Path.Combine(
        repository, LoopRelayWorkspaceDatabase.RelativeDatabasePath.Replace('/', Path.DirectorySeparatorChar));

    private static async Task<(IReadOnlyList<string> Tables, IReadOnlyDictionary<string, IReadOnlyList<string>> Columns)>
        InspectSchemaAsync(string database, CancellationToken token)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(database);
        await connection.OpenAsync(token);
        var tables = new List<string>();
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) tables.Add(reader.GetString(0));
        }
        var columns = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (string table in tables)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info([{table}]);";
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
            var names = new List<string>();
            while (await reader.ReadAsync(token)) names.Add(reader.GetString(1));
            columns[table] = names;
        }
        return (tables, columns);
    }

    private static async Task<string> WorkspaceIdAsync(string database, CancellationToken token)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(database);
        await connection.OpenAsync(token);
        return await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection, token);
    }

    private static async Task<int> SchemaVersionAsync(string database, CancellationToken token)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(database);
        await connection.OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM schema_metadata WHERE key='schema_version';";
        return int.Parse(Convert.ToString(await command.ExecuteScalarAsync(token))!);
    }

    private static async Task ExecuteAsync(string database, string sql, CancellationToken token)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(database);
        await connection.OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task WriteMetadataAsync(string database, int index, CancellationToken token)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(database);
        await connection.OpenAsync(token);
        await using (SqliteCommand busy = connection.CreateCommand())
        {
            busy.CommandText = "PRAGMA busy_timeout=10000;";
            await busy.ExecuteNonQueryAsync(token);
        }
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO workspace_metadata (key, value) VALUES ($key, $value);";
        command.Parameters.AddWithValue("$key", $"m8.writer.{index}");
        command.Parameters.AddWithValue("$value", index.ToString());
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<long> ScalarAsync(string database, string sql, CancellationToken token)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(database);
        await connection.OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(token));
    }

    private static Dictionary<string, string> SnapshotFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), path => Hash(File.ReadAllBytes(path)), StringComparer.Ordinal);

    private static bool SameSnapshot(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right) => left.Count == right.Count &&
        left.All(pair => right.TryGetValue(pair.Key, out string? value) && value == pair.Value);

    private static Task<ProcessResult> RunCliAsync(
        string cliPath, string repository, IReadOnlyList<string> arguments, CancellationToken token)
    {
        var all = new List<string>();
        string file = cliPath;
        if (cliPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            file = "dotnet";
            all.Add(cliPath);
        }
        all.AddRange(["--repo", repository]);
        all.AddRange(arguments);
        return RunProcessAsync(file, all, repository, token);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string file, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken token)
    {
        var start = new ProcessStartInfo
        {
            FileName = file,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"{file} did not start.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static string Hash(string value) => Hash(System.Text.Encoding.UTF8.GetBytes(value));
    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
