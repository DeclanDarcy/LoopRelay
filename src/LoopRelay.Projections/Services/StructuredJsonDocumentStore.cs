using System.Text.Json;
using LoopRelay.Projections.Models;

namespace LoopRelay.Projections.Services;

internal sealed class StructuredJsonDocumentStore<TDocument>(
    ProjectionArtifacts artifacts,
    string path,
    string expectedSchemaVersion,
    Func<TDocument, string?> getSchemaVersion,
    Func<TDocument, IReadOnlyList<string>> validate)
    where TDocument : class
{
    public async Task<TDocument?> LoadAsync()
    {
        string? content = await artifacts.ReadAsync(path);
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
        await artifacts.WriteAsync(path, content);
    }

    private void Validate(TDocument document)
    {
        string? schemaVersion = getSchemaVersion(document);
        if (!string.Equals(schemaVersion, expectedSchemaVersion, StringComparison.Ordinal))
        {
            throw new ProjectionException(
                $"Canonical structured persistence at {path} has unsupported schema version `{schemaVersion ?? "null"}`; expected `{expectedSchemaVersion}`.");
        }

        IReadOnlyList<string> errors = validate(document);
        if (errors.Count > 0)
        {
            throw new ProjectionException(
                $"Canonical structured persistence at {path} failed validation: {string.Join("; ", errors)}");
        }
    }
}
