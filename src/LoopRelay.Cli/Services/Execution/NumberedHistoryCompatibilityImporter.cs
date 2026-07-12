using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Cli.Services.Execution;

internal sealed record NumberedHistoryImportResult(
    string ImportId,
    int ImportedFacts,
    string SourceDigest);

/// <summary>
/// Explicit, journaled Compatibility Authority operation for pre-ledger numbered history files.
/// </summary>
internal sealed class NumberedHistoryCompatibilityImporter(
    ILegacyLoopHistoryStore _source,
    LedgerLoopHistoryStore _target,
    Repository _repository)
{
    public async Task<NumberedHistoryImportResult> ImportAsync(
        CanonicalCausalContext importCausality,
        CancellationToken cancellationToken = default)
    {
        var sourceFacts = new List<LegacyLoopHistoryRecord>();
        foreach (LoopHistoryKind kind in Enum.GetValues<LoopHistoryKind>())
        {
            sourceFacts.AddRange(await _source.ReadAllLegacyAsync(kind, cancellationToken));
        }

        sourceFacts = sourceFacts
            .OrderBy(fact => fact.Kind)
            .ThenBy(fact => fact.Sequence)
            .ToList();
        string sourceDigest = ComputeDigest(sourceFacts);
        string importId = CausalUlid.NewId("import");
        string planHash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes($"NumberedHistory:v1:{sourceDigest}:{sourceFacts.Count}")));
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        await RecordOperationAsync(
            connection, importId, sourceDigest, planHash, "Planned", cancellationToken);
        await RecordEventAsync(connection, importId, "Planned", [], cancellationToken);
        await UpdateStateAsync(connection, importId, "Started", cancellationToken);
        await RecordEventAsync(connection, importId, "Started", [], cancellationToken);

        try
        {
            int imported = 0;
            foreach (LegacyLoopHistoryRecord fact in sourceFacts)
            {
                if (fact.Content is null)
                {
                    throw new InvalidDataException($"Legacy history file `{fact.RelativePath}` could not be read.");
                }

                LoopHistoryRecord appended = await _target.AppendAsync(
                    new LoopHistoryAppendRequest(
                        fact.Kind,
                        fact.Content,
                        importCausality,
                        new HistoryEvidenceAttachments(
                            Repository: new HistoryRepositoryEvidence(null, [fact.RelativePath]))),
                    cancellationToken);
                await MarkExistingProjectionCompletedAsync(
                    connection, appended.Identity, cancellationToken);
                imported++;
            }

            await UpdateStateAsync(connection, importId, "Verified", cancellationToken);
            await RecordEventAsync(
                connection,
                importId,
                "Verified",
                [$"facts:{imported}", $"source:{sourceDigest}"],
                cancellationToken);
            await UpdateStateAsync(connection, importId, "Completed", cancellationToken);
            await RecordEventAsync(connection, importId, "Completed", [$"facts:{imported}"], cancellationToken);
            await MarkWorkspaceCanonicalAsync(connection, cancellationToken);
            return new NumberedHistoryImportResult(importId, imported, sourceDigest);
        }
        catch (Exception exception)
        {
            await UpdateStateAsync(connection, importId, "Failed", CancellationToken.None, exception.Message);
            await RecordEventAsync(connection, importId, "Failed", [exception.Message], CancellationToken.None);
            throw;
        }
    }

    private static string ComputeDigest(IReadOnlyList<LegacyLoopHistoryRecord> facts)
    {
        string canonical = string.Join('\n', facts.Select(fact =>
            $"{fact.Kind}|{fact.Sequence}|{fact.RelativePath}|{LoopHistoryRecord.ComputeContentHash(fact.Content ?? string.Empty)}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static async Task RecordOperationAsync(
        SqliteConnection connection,
        string importId,
        string sourceDigest,
        string planHash,
        string state,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO compatibility_import_operations (
                import_id, source_schema_identity, source_schema_family, source_schema_version,
                source_digest, plan_hash, state, planned_at, diagnostic_json
            ) VALUES (
                $id, 'looprelay.numbered-history', 'NumberedHistoryFiles', 1,
                $digest, $plan, $state, $at, '{}'
            );
            """;
        command.Parameters.AddWithValue("$id", importId);
        command.Parameters.AddWithValue("$digest", sourceDigest);
        command.Parameters.AddWithValue("$plan", planHash);
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateStateAsync(
        SqliteConnection connection,
        string importId,
        string state,
        CancellationToken cancellationToken,
        string? diagnostic = null)
    {
        string now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE compatibility_import_operations
            SET state = $state,
                started_at = CASE WHEN $state = 'Started' THEN $now ELSE started_at END,
                verified_at = CASE WHEN $state = 'Verified' THEN $now ELSE verified_at END,
                completed_at = CASE WHEN $state IN ('Completed', 'Failed') THEN $now ELSE completed_at END,
                diagnostic_json = $diagnostic
            WHERE import_id = $id;
            """;
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue("$now", now);
        command.Parameters.AddWithValue("$diagnostic", JsonSerializer.Serialize(new { diagnostic }));
        command.Parameters.AddWithValue("$id", importId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RecordEventAsync(
        SqliteConnection connection,
        string importId,
        string state,
        IReadOnlyList<string> evidence,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO compatibility_import_events (
                event_id, import_id, state, recorded_at, evidence_json
            ) VALUES ($event, $import, $state, $at, $evidence);
            """;
        command.Parameters.AddWithValue("$event", CausalUlid.NewId("impevent"));
        command.Parameters.AddWithValue("$import", importId);
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$evidence", JsonSerializer.Serialize(evidence));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkExistingProjectionCompletedAsync(
        SqliteConnection connection,
        HistoryFactIdentity historyIdentity,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE canonical_projection_effects
            SET status = 'Completed', completed_at = $at
            WHERE history_id = $history;
            """;
        command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$history", historyIdentity.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkWorkspaceCanonicalAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO workspace_metadata (key, value) VALUES ('persistence_state', 'canonical')
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
