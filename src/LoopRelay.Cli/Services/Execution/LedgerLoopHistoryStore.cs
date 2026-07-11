using System.Globalization;
using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Services;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Cli.Services.Execution;

/// <summary>
/// History authority for decision/handoff/delta history on the canonical path: every append is
/// a `loop_history` ledger row (stable `hist_` identity, content hash, causal lineage) and the
/// numbered `.agents` file is written afterwards as a derived projection at the same sequence.
/// Pre-ledger numbered files (written by earlier builds of this same path) are imported into the
/// ledger once, append-only with null lineage, so upgraded workspaces keep their history; after
/// that, reads resolve from the ledger only — a numbered file never acts as history here.
/// </summary>
internal sealed class LedgerLoopHistoryStore(IArtifactStore _store, Repository _repository) : ILoopHistoryStore
{
    public async Task<LoopHistoryRecord> AppendAsync(LoopHistoryKind kind, string content, LoopHistoryLineage? lineage = null)
    {
        LoopHistorySpec spec = GetSpec(kind);
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        int sequence;
        string relativePath;
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
            await using SqliteTransaction transaction =
                (SqliteTransaction)await connection.BeginTransactionAsync();
            int ledgerHighest = await BackfillNumberedFilesAsync(connection, transaction, spec);
            sequence = ledgerHighest + 1;
            relativePath = spec.HistoricalPath(sequence);
            // A colliding projection target aborts before anything durable is written; after the
            // commit below, the fact is history and a projection failure must not erase it.
            if (await _store.ExistsAsync(Resolve(relativePath)))
            {
                throw new IOException($"Historical artifact already exists: {relativePath}");
            }

            await InsertAsync(
                connection,
                transaction,
                spec,
                sequence,
                relativePath,
                content,
                DateTimeOffset.UtcNow,
                lineage);
            await transaction.CommitAsync();
        }

        try
        {
            await _store.WriteAsync(Resolve(relativePath), content);
        }
        catch
        {
            // The ledger row is the history fact and it is already durable; the numbered file is
            // a derived projection that can be re-derived from the ledger, so its write failure
            // must not fail the rotation.
        }

        return new LoopHistoryRecord(kind, sequence, relativePath, content);
    }

    public async Task<LoopHistoryRecord?> ReadLatestAsync(LoopHistoryKind kind)
    {
        LoopHistorySpec spec = GetSpec(kind);
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return null;
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        await using (SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync())
        {
            await BackfillNumberedFilesAsync(connection, transaction, spec);
            await transaction.CommitAsync();
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT sequence, logical_path, body, content_hash
            FROM loop_history
            WHERE kind = $kind
            ORDER BY sequence DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$kind", spec.KindToken);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        int sequence = reader.GetInt32(0);
        string relativePath = reader.GetString(1);
        string body = reader.GetString(2);
        string hash = reader.GetString(3);
        if (!string.Equals(hash, ConsumedInputFile.HashContent(body), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Loop history hash mismatch for `{relativePath}`.");
        }

        return new LoopHistoryRecord(kind, sequence, relativePath, body);
    }

    // One-way, once: numbered files above the ledger's highest sequence were written by pre-M4
    // builds where the file WAS the history; importing them keeps one append-only history without
    // ever selecting a file as read authority. Returns the ledger's highest sequence afterwards.
    private async Task<int> BackfillNumberedFilesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LoopHistorySpec spec)
    {
        int ledgerHighest = await LedgerHighestSequenceAsync(connection, transaction, spec);
        int fileHighest = await FileHighestSequenceAsync(spec);
        for (int sequence = ledgerHighest + 1; sequence <= fileHighest; sequence++)
        {
            string relativePath = spec.HistoricalPath(sequence);
            string absolutePath = Resolve(relativePath);
            string? content = await _store.ReadAsync(absolutePath);
            if (content is null)
            {
                continue;
            }

            await InsertAsync(
                connection,
                transaction,
                spec,
                sequence,
                relativePath,
                content,
                new DateTimeOffset(File.GetLastWriteTimeUtc(absolutePath), TimeSpan.Zero),
                lineage: null);
        }

        return Math.Max(ledgerHighest, fileHighest);
    }

    private static async Task InsertAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LoopHistorySpec spec,
        int sequence,
        string relativePath,
        string content,
        DateTimeOffset createdAt,
        LoopHistoryLineage? lineage)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO loop_history (
                kind, sequence, logical_path, body, content_hash, created_at,
                history_id, run_id, transition_run_id, attempt_id
            )
            VALUES (
                $kind, $sequence, $logical_path, $body, $content_hash, $created_at,
                $history_id, $run_id, $transition_run_id, $attempt_id
            );
            """;
        command.Parameters.AddWithValue("$kind", spec.KindToken);
        command.Parameters.AddWithValue("$sequence", sequence);
        command.Parameters.AddWithValue("$logical_path", relativePath);
        command.Parameters.AddWithValue("$body", content);
        command.Parameters.AddWithValue("$content_hash", ConsumedInputFile.HashContent(content));
        command.Parameters.AddWithValue("$created_at", createdAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$history_id", CausalUlid.NewId("hist"));
        command.Parameters.AddWithValue("$run_id", (object?)lineage?.RunId ?? DBNull.Value);
        command.Parameters.AddWithValue("$transition_run_id", (object?)lineage?.TransitionRunId ?? DBNull.Value);
        command.Parameters.AddWithValue("$attempt_id", (object?)lineage?.AttemptId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private string Resolve(string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(_repository, relativePath);

    private static async Task<int> LedgerHighestSequenceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LoopHistorySpec spec)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(sequence), 0) FROM loop_history WHERE kind = $kind;";
        command.Parameters.AddWithValue("$kind", spec.KindToken);
        object? scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }

    private async Task<int> FileHighestSequenceAsync(LoopHistorySpec spec)
    {
        IReadOnlyList<string> files = await _store.ListAsync(Resolve(spec.Directory), spec.SearchPattern);
        int max = 0;
        foreach (string file in files)
        {
            string[] parts = Path.GetFileName(file).Split('.');
            if (parts.Length < 3 || !string.Equals(parts[0], spec.BaseName, StringComparison.Ordinal))
            {
                continue;
            }

            string segment = parts[^2];
            if (segment.Length == 4 && int.TryParse(segment, out int parsed) && parsed > 0)
            {
                max = Math.Max(max, parsed);
            }
        }

        return max;
    }

    private static LoopHistorySpec GetSpec(LoopHistoryKind kind) => kind switch
    {
        LoopHistoryKind.Decisions => new LoopHistorySpec(
            "Decisions",
            OrchestrationArtifactPaths.DecisionsDirectory,
            OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
            "decisions",
            OrchestrationArtifactPaths.HistoricalDecision),
        LoopHistoryKind.Handoff => new LoopHistorySpec(
            "Handoff",
            OrchestrationArtifactPaths.HandoffsDirectory,
            OrchestrationArtifactPaths.HistoricalHandoffSearchPattern,
            "handoff",
            OrchestrationArtifactPaths.HistoricalHandoff),
        LoopHistoryKind.OperationalDelta => new LoopHistorySpec(
            "OperationalDelta",
            OrchestrationArtifactPaths.DeltasDirectory,
            OrchestrationArtifactPaths.HistoricalDeltaSearchPattern,
            "operational_delta",
            OrchestrationArtifactPaths.HistoricalDelta),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private readonly record struct LoopHistorySpec(
        string KindToken,
        string Directory,
        string SearchPattern,
        string BaseName,
        Func<int, string> HistoricalPath);
}
