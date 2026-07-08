using System.Text.Json;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.ProjectionArtifacts;

namespace LoopRelay.Projections.Services.ProjectionArtifacts;

internal sealed class StructuredJsonDocumentStore<TDocument>(
    ProjectionArtifacts _artifacts,
    string path,
    string expectedSchemaVersion,
    Func<TDocument, string?> _getSchemaVersion,
    Func<TDocument, IReadOnlyList<string>> _validate)
    where TDocument : class
{
    public async Task<TDocument?> LoadAsync()
    {
        string? content = await _artifacts.ReadAsync(path);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        TDocument document;
        try
        {
            document = JsonSerializer.Deserialize<TDocument>(content, ProjectionJson.Options)
                ?? throw new ProjectionException($"Canonical structured persistence is empty: {path}.");
        }
        catch (JsonException exception)
        {
            throw new ProjectionException($"Canonical structured persistence is invalid JSON at {path}: {exception.Message}");
        }

        Validate(document);
        return document;
    }

    public async Task SaveAsync(TDocument document)
    {
        Validate(document);
        string content = JsonSerializer.Serialize(document, ProjectionJson.Options) + Environment.NewLine;
        await _artifacts.WriteAsync(path, content);
    }

    private void Validate(TDocument document)
    {
        string? schemaVersion = _getSchemaVersion(document);
        if (!string.Equals(schemaVersion, expectedSchemaVersion, StringComparison.Ordinal))
        {
            throw new ProjectionException(
                $"Canonical structured persistence at {path} has unsupported schema version `{schemaVersion ?? "null"}`; expected `{expectedSchemaVersion}`.");
        }

        IReadOnlyList<string> errors = _validate(document);
        if (errors.Count > 0)
        {
            throw new ProjectionException(
                $"Canonical structured persistence at {path} failed validation: {string.Join("; ", errors)}");
        }
    }
}
