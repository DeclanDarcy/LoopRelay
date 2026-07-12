using System.Globalization;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Cli.Services.Execution;

internal sealed record HistoryProjectionEffectResult(
    string EffectId,
    string Status,
    string TargetPath,
    string? Failure = null);

/// <summary>
/// Effect Coordinator consumer for history materialization. The history fact is already durable
/// before this runner observes its projection effect.
/// </summary>
internal sealed class HistoryProjectionEffectRunner(
    IArtifactStore _artifacts,
    Repository _repository)
{
    public async Task<IReadOnlyList<HistoryProjectionEffectResult>> RunPendingAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        IReadOnlyList<ProjectionWorkItem> pending = await ReadPendingAsync(connection, cancellationToken);
        var results = new List<HistoryProjectionEffectResult>(pending.Count);
        foreach (ProjectionWorkItem item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SetStateAsync(connection, item.EffectId, "Started", null, cancellationToken);
            try
            {
                string target = ArtifactPath.ResolveRepositoryPath(_repository, item.TargetPath);
                await _artifacts.WriteAsync(target, item.Content);
                await SetStateAsync(connection, item.EffectId, "Completed", null, cancellationToken);
                results.Add(new HistoryProjectionEffectResult(item.EffectId, "Completed", item.TargetPath));
            }
            catch (Exception exception)
            {
                await SetStateAsync(connection, item.EffectId, "Failed", exception.Message, CancellationToken.None);
                results.Add(new HistoryProjectionEffectResult(
                    item.EffectId,
                    "Failed",
                    item.TargetPath,
                    exception.Message));
            }
        }

        return results;
    }

    private static async Task<IReadOnlyList<ProjectionWorkItem>> ReadPendingAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var items = new List<ProjectionWorkItem>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT effect.effect_id, effect.target_path, fact.body, effect.content_hash
            FROM canonical_projection_effects effect
            JOIN loop_history fact ON fact.history_id = effect.history_id
            WHERE effect.status IN ('Planned', 'Failed')
            ORDER BY effect.planned_at, effect.effect_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string body = reader.GetString(2);
            string hash = reader.GetString(3);
            if (!string.Equals(hash, LoopRelay.Cli.Abstractions.Persistence.LoopHistoryRecord.ComputeContentHash(body), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Projection effect `{reader.GetString(0)}` references a history body with a mismatched hash.");
            }

            items.Add(new ProjectionWorkItem(reader.GetString(0), reader.GetString(1), body));
        }

        return items;
    }

    private static async Task SetStateAsync(
        SqliteConnection connection,
        string effectId,
        string status,
        string? failure,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE canonical_projection_effects
            SET status = $status,
                started_at = CASE WHEN $status = 'Started' THEN $now ELSE started_at END,
                completed_at = CASE WHEN $status = 'Completed' THEN $now ELSE completed_at END,
                failure = $failure
            WHERE effect_id = $effect;
            """;
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$failure", (object?)failure ?? DBNull.Value);
        command.Parameters.AddWithValue("$effect", effectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record ProjectionWorkItem(string EffectId, string TargetPath, string Content);
}
