using System.Globalization;
using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Chaining;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Persistence;

public sealed class CanonicalKernelDecisionStore(Repository _repository) : IKernelDecisionStore
{
    public async Task AppendAsync(KernelDecisionFact decision, CancellationToken cancellationToken)
    {
        string database = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenMigrationTarget(database);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO canonical_kernel_decisions(
                decision_id,catalog_identity,snapshot_identity,root_run_id,workflow_instance_id,
                transition_run_id,attempt_id,eligible_json,rejected_json,selected_action,outcome,evidence_json,recorded_at
            ) VALUES($id,$catalog,$snapshot,$run,$workflow,$transition,$attempt,$eligible,$rejected,$action,$outcome,$evidence,$at)
            ON CONFLICT(decision_id) DO NOTHING;
            """;
        Add(("$id", decision.Identity.Value), ("$catalog", decision.CatalogIdentity),
            ("$snapshot", decision.SnapshotIdentity), ("$run", decision.RootRun.Value),
            ("$workflow", decision.WorkflowInstance?.Value), ("$transition", decision.TransitionRun?.Value),
            ("$attempt", decision.Attempt?.Value), ("$eligible", JsonSerializer.Serialize(decision.EligibleAlternatives)),
            ("$rejected", JsonSerializer.Serialize(decision.RejectedAlternatives)),
            ("$action", decision.SelectedAction), ("$outcome", decision.Outcome.ToString()),
            ("$evidence", JsonSerializer.Serialize(decision.Evidence)),
            ("$at", decision.RecordedAt.ToString("O", CultureInfo.InvariantCulture)));
        await command.ExecuteNonQueryAsync(cancellationToken);
        void Add(params (string Name, object? Value)[] values)
        {
            foreach ((string name, object? value) in values)
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }

    public async Task<IReadOnlyList<KernelDecisionFact>> ReadAsync(CancellationToken cancellationToken = default)
    {
        string database = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(database)) return [];
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenReadOnly(database);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT decision_id,catalog_identity,snapshot_identity,root_run_id,workflow_instance_id,
                   transition_run_id,attempt_id,eligible_json,rejected_json,selected_action,outcome,evidence_json,recorded_at
            FROM canonical_kernel_decisions ORDER BY recorded_at,decision_id;
            """;
        var result = new List<KernelDecisionFact>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(new(reader.GetString(0)), reader.GetString(1), reader.GetString(2), new(reader.GetString(3)),
                reader.IsDBNull(4) ? null : new(reader.GetString(4)),
                reader.IsDBNull(5) ? null : new(reader.GetString(5)),
                reader.IsDBNull(6) ? null : new(reader.GetString(6)),
                JsonSerializer.Deserialize<string[]>(reader.GetString(7)) ?? [],
                JsonSerializer.Deserialize<string[]>(reader.GetString(8)) ?? [], reader.GetString(9),
                Enum.Parse<WorkflowStopReason>(reader.GetString(10)),
                JsonSerializer.Deserialize<string[]>(reader.GetString(11)) ?? [],
                DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        return result;
    }
}
