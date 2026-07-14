using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Completion.Models.Archive;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;

namespace LoopRelay.Completion.Services.ArtifactStorage;

public sealed class CompletedEpicArchiveRecoveryService(IArtifactStore _store, Repository _repository)
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CompletedEpicArchiveRecoveryResult> LoadAsync(
        int archiveIndex,
        string archiveRoot = CompletionArtifactPaths.CompletedEpicsDirectory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var artifacts = new CompletionArtifacts(_store, _repository);
        string archiveDirectory = $"{archiveRoot}/{archiveIndex}";
        string metadataPath = $"{archiveDirectory}/archive-metadata.json";
        string? metadata = await artifacts.ReadAsync(metadataPath);
        IReadOnlyList<CompletedEpicArchiveRecord> records = metadata is null
            ? await RecoverFromMaterializedFilesAsync(artifacts, archiveDirectory)
            : ParseMetadata(metadataPath, metadata);

        return new CompletedEpicArchiveRecoveryResult(
            archiveIndex,
            archiveDirectory,
            records.OrderBy(record => record.ExportPath, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<CompletedEpicArchiveRecord> ParseMetadata(string metadataPath, string metadata)
    {
        ArchiveMetadataDocument document;
        try
        {
            document = JsonSerializer.Deserialize<ArchiveMetadataDocument>(metadata, MetadataJsonOptions)
                ?? throw new JsonException("Archive metadata was null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Completed epic archive metadata is invalid at `{metadataPath}`: {exception.Message}", exception);
        }

        if (!string.Equals(document.SchemaVersion, "completed-epic-archive.v1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Completed epic archive metadata at `{metadataPath}` has unsupported schema `{document.SchemaVersion}`.");
        }

        return document.Records
            .Select(record => new CompletedEpicArchiveRecord(
                record.Domain,
                record.LogicalPath,
                record.ExportPath,
                record.ContentHash))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CompletedEpicArchiveRecord>> RecoverFromMaterializedFilesAsync(
        CompletionArtifacts artifacts,
        string archiveDirectory)
    {
        var records = new List<CompletedEpicArchiveRecord>();
        await AddRecordsAsync(artifacts, records, archiveDirectory, "decisions", "loop_history", CompletionArtifactPaths.DecisionsDirectory);
        await AddRecordsAsync(artifacts, records, archiveDirectory, "deltas", "loop_history", CompletionArtifactPaths.DeltasDirectory);
        await AddRecordsAsync(artifacts, records, archiveDirectory, "handoffs", "loop_history", CompletionArtifactPaths.HandoffsDirectory);
        await AddRecordsAsync(artifacts, records, archiveDirectory, "evidence/execution", "execution_evidence", CompletionArtifactPaths.ExecutionEvidenceDirectory);
        return records;
    }

    private static async Task AddRecordsAsync(
        CompletionArtifacts artifacts,
        List<CompletedEpicArchiveRecord> records,
        string archiveDirectory,
        string archiveSubdirectory,
        string domain,
        string originalDirectory)
    {
        string materializedDirectory = $"{archiveDirectory}/{archiveSubdirectory}";
        IReadOnlyList<string> files = await artifacts.ListAsync(materializedDirectory, "*.md");
        foreach (string exportPath in files.Order(StringComparer.Ordinal))
        {
            string? body = await artifacts.ReadAsync(exportPath);
            if (body is null)
            {
                continue;
            }

            records.Add(new CompletedEpicArchiveRecord(
                domain,
                $"{originalDirectory}/{Path.GetFileName(exportPath)}",
                exportPath,
                Sha256(body)));
        }
    }

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private sealed record ArchiveMetadataDocument(
        string SchemaVersion,
        IReadOnlyList<ArchiveMetadataRecord> Records);

    private sealed record ArchiveMetadataRecord(
        string Domain,
        string LogicalPath,
        string ExportPath,
        string ContentHash);
}
