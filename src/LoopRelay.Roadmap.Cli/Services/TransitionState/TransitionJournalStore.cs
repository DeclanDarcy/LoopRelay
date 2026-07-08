using System.Text.Json;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Services.Artifacts;

namespace LoopRelay.Roadmap.Cli.Services.TransitionState;

internal sealed class TransitionJournalStore(RoadmapArtifacts artifacts)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RoadmapArtifacts _artifacts = artifacts;

    public async Task AppendAsync(TransitionJournalRecord record)
    {
        string? existing = await _artifacts.ReadAsync(RoadmapArtifactPaths.TransitionJournal);
        string line = JsonSerializer.Serialize(record, JsonOptions);
        string content = string.IsNullOrEmpty(existing) ? line + Environment.NewLine : existing.TrimEnd() + Environment.NewLine + line + Environment.NewLine;
        await _artifacts.WriteAsync(RoadmapArtifactPaths.TransitionJournal, content);
    }
}
