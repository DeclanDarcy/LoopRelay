using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Services.Artifacts;

namespace LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;

internal sealed class ExecutionPreparationManifestStore(RoadmapArtifacts _artifacts) : IExecutionPreparationManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<ExecutionPreparationManifest> LoadAsync()
    {
        string? content = await _artifacts.ReadAsync(RoadmapArtifactPaths.ExecutionPreparationManifest);
        if (string.IsNullOrWhiteSpace(content))
        {
            return ExecutionPreparationManifest.Empty;
        }

        try
        {
            ExecutionPreparationManifest? manifest = JsonSerializer.Deserialize<ExecutionPreparationManifest>(content, JsonOptions);
            return manifest ?? ExecutionPreparationManifest.Empty;
        }
        catch (JsonException)
        {
            return ExecutionPreparationManifest.Empty;
        }
    }

    public async Task SaveAsync(ExecutionPreparationManifest manifest)
    {
        string content = JsonSerializer.Serialize(manifest, JsonOptions) + Environment.NewLine;
        await _artifacts.WriteAsync(RoadmapArtifactPaths.ExecutionPreparationManifest, content);
    }
}
