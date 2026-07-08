using System.Text.Json;
using LoopRelay.Roadmap.Cli.Models;

namespace LoopRelay.Roadmap.Cli.Services;

internal sealed class TransitionJournalStore(RoadmapArtifacts artifacts)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task AppendAsync(TransitionJournalRecord record)
    {
        string? existing = await artifacts.ReadAsync(RoadmapArtifactPaths.TransitionJournal);
        string line = JsonSerializer.Serialize(record, JsonOptions);
        string content = string.IsNullOrEmpty(existing) ? line + Environment.NewLine : existing.TrimEnd() + Environment.NewLine + line + Environment.NewLine;
        await artifacts.WriteAsync(RoadmapArtifactPaths.TransitionJournal, content);
    }
}
