using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Roadmap.Cli.Services.Persistence;

internal sealed class SqliteExecutionPreparationManifestStore(Repository repository)
    : SqliteDomainStore(repository), IExecutionPreparationManifestStore
{
    public async Task<ExecutionPreparationManifest> LoadAsync()
    {
        await using SqliteConnection connection = await OpenAsync();
        string? json = await ScalarStringAsync(
            connection,
            "SELECT document_json FROM execution_preparation_manifest WHERE id = 1;");
        return json is null
            ? ExecutionPreparationManifest.Empty
            : Deserialize<ExecutionPreparationManifest>(json);
    }

    public async Task SaveAsync(ExecutionPreparationManifest manifest)
    {
        await using SqliteConnection connection = await OpenAsync();
        await ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO execution_preparation_manifest (id, document_json, updated_at)
            VALUES (1, $document_json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                document_json = excluded.document_json,
                updated_at = excluded.updated_at;
            """,
            parameters:
            [
                ("$document_json", JsonSerializer.Serialize(manifest, JsonOptions)),
                ("$updated_at", Format(DateTimeOffset.UtcNow)),
            ]);
    }
}

internal sealed class SqliteSelectionProvenanceManifestStore(Repository repository)
    : SqliteDomainStore(repository), ISelectionProvenanceManifestStore
{
    public async Task<SelectionProvenanceManifest> LoadAsync()
    {
        await using SqliteConnection connection = await OpenAsync();
        string? json = await ScalarStringAsync(
            connection,
            "SELECT document_json FROM selection_provenance_manifest WHERE id = 1;");
        return json is null
            ? SelectionProvenanceManifest.Empty
            : Deserialize<SelectionProvenanceManifest>(json);
    }

    public async Task SaveAsync(SelectionProvenanceManifest manifest)
    {
        await using SqliteConnection connection = await OpenAsync();
        await ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO selection_provenance_manifest (id, document_json, updated_at)
            VALUES (1, $document_json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                document_json = excluded.document_json,
                updated_at = excluded.updated_at;
            """,
            parameters:
            [
                ("$document_json", JsonSerializer.Serialize(manifest, JsonOptions)),
                ("$updated_at", Format(DateTimeOffset.UtcNow)),
            ]);
    }
}

internal sealed class SqliteProjectionManifestStore(Repository repository)
    : SqliteDomainStore(repository), IProjectionManifestStore
{
    public async Task<ProjectionManifest> LoadAsync()
    {
        await using SqliteConnection connection = await OpenAsync();
        var entries = new List<ProjectionManifestEntryDto>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT document_json FROM projection_manifest_entries ORDER BY runtime_prompt;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(Deserialize<ProjectionManifestEntryDto>(reader.GetString(0), RoadmapJson.Options));
        }

        var document = new ProjectionManifestPersistenceDocument(
            ProjectionManifestPersistenceDocument.CurrentSchemaVersion,
            entries);
        IReadOnlyList<string> errors = ProjectionManifestPersistenceDocument.Validate(document);
        if (errors.Count > 0)
        {
            throw new RoadmapStepException($"SQLite projection manifest is invalid: {string.Join("; ", errors)}");
        }

        return document.ToDomain();
    }

    public async Task SaveAsync(ProjectionManifest manifest)
    {
        ProjectionManifestPersistenceDocument document = ProjectionManifestPersistenceDocument.FromDomain(manifest);
        IReadOnlyList<string> errors = ProjectionManifestPersistenceDocument.Validate(document);
        if (errors.Count > 0)
        {
            throw new RoadmapStepException($"Projection manifest cannot be saved to SQLite: {string.Join("; ", errors)}");
        }

        await using SqliteConnection connection = await OpenAsync();
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "DELETE FROM projection_manifest_entries;");
        foreach (ProjectionManifestEntryDto entry in document.Entries)
        {
            await InsertEntryAsync(connection, transaction, entry);
        }

        await transaction.CommitAsync();
    }

    public async Task UpsertAsync(ProjectionManifestEntry entry)
    {
        ProjectionManifestEntryDto dto = ProjectionManifestEntryDto.FromDomain(entry);
        await using SqliteConnection connection = await OpenAsync();
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync();
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM projection_manifest_entries WHERE runtime_prompt = $runtime_prompt;",
            parameters: [("$runtime_prompt", dto.RuntimePromptName)]);
        await InsertEntryAsync(connection, transaction, dto);
        await transaction.CommitAsync();
    }

    private static Task InsertEntryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProjectionManifestEntryDto entry) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO projection_manifest_entries (runtime_prompt, document_json, updated_at)
            VALUES ($runtime_prompt, $document_json, $updated_at);
            """,
            parameters:
            [
                ("$runtime_prompt", entry.RuntimePromptName),
                ("$document_json", JsonSerializer.Serialize(entry, RoadmapJson.Options)),
                ("$updated_at", Format(DateTimeOffset.UtcNow)),
            ]);
}
