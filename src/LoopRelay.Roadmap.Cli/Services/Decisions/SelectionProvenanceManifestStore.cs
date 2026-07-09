using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Services.Artifacts;

namespace LoopRelay.Roadmap.Cli.Services.Decisions;

internal sealed class SelectionProvenanceManifestStore(RoadmapArtifacts _artifacts) : ISelectionProvenanceManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<SelectionProvenanceManifest> LoadAsync()
    {
        string? content = await _artifacts.ReadAsync(RoadmapArtifactPaths.SelectionProvenanceManifest);
        if (string.IsNullOrWhiteSpace(content))
        {
            return SelectionProvenanceManifest.Empty;
        }

        try
        {
            SelectionProvenanceManifest? manifest = JsonSerializer.Deserialize<SelectionProvenanceManifest>(content, JsonOptions);
            return manifest ?? SelectionProvenanceManifest.Empty;
        }
        catch (JsonException)
        {
            return SelectionProvenanceManifest.Empty;
        }
    }

    public async Task SaveAsync(SelectionProvenanceManifest manifest)
    {
        string content = JsonSerializer.Serialize(manifest, JsonOptions) + Environment.NewLine;
        await _artifacts.WriteAsync(RoadmapArtifactPaths.SelectionProvenanceManifest, content);
    }
}
