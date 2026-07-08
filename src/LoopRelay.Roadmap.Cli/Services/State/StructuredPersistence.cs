using System.Text.Json;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Services.Artifacts;

namespace LoopRelay.Roadmap.Cli.Services.State;

internal sealed class StructuredDocumentStore<TDocument>(
    RoadmapArtifacts _artifacts,
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
            document = JsonSerializer.Deserialize<TDocument>(content, RoadmapJson.Options)
                ?? throw new RoadmapStepException($"Canonical structured persistence is empty: {path}.");
        }
        catch (JsonException exception)
        {
            throw new RoadmapStepException($"Canonical structured persistence is invalid JSON at {path}: {exception.Message}");
        }

        Validate(document);
        return document;
    }

    public async Task SaveAsync(TDocument document)
    {
        Validate(document);
        string content = JsonSerializer.Serialize(document, RoadmapJson.Options) + Environment.NewLine;
        await _artifacts.WriteAsync(path, content);
    }

    public void Validate(TDocument document)
    {
        string? schemaVersion = _getSchemaVersion(document);
        if (!string.Equals(schemaVersion, expectedSchemaVersion, StringComparison.Ordinal))
        {
            throw new RoadmapStepException(
                $"Canonical structured persistence at {path} has unsupported schema version `{schemaVersion ?? "null"}`; expected `{expectedSchemaVersion}`.");
        }

        IReadOnlyList<string> errors = _validate(document);
        if (errors.Count > 0)
        {
            throw new RoadmapStepException(
                $"Canonical structured persistence at {path} failed validation: {string.Join("; ", errors)}");
        }
    }
}
