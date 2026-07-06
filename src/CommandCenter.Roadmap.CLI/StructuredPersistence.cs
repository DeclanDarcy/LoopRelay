using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandCenter.Roadmap.Cli;

internal sealed class StructuredDocumentStore<TDocument>(
    RoadmapArtifacts artifacts,
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
        await artifacts.WriteAsync(path, content);
    }

    public void Validate(TDocument document)
    {
        string? schemaVersion = getSchemaVersion(document);
        if (!string.Equals(schemaVersion, expectedSchemaVersion, StringComparison.Ordinal))
        {
            throw new RoadmapStepException(
                $"Canonical structured persistence at {path} has unsupported schema version `{schemaVersion ?? "null"}`; expected `{expectedSchemaVersion}`.");
        }

        IReadOnlyList<string> errors = validate(document);
        if (errors.Count > 0)
        {
            throw new RoadmapStepException(
                $"Canonical structured persistence at {path} failed validation: {string.Join("; ", errors)}");
        }
    }
}

internal static class RoadmapJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
