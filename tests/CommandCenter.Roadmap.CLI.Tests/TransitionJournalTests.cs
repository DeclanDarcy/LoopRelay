using System.Text.Json;
using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class TransitionJournalTests
{
    [Fact]
    public async Task Journal_records_started_and_completed_correlation_ids()
    {
        using var repo = new TempRepo();
        var store = new TransitionJournalStore(repo.Artifacts);

        await store.AppendAsync(new TransitionJournalRecord("TransitionStarted", "abc", DateTimeOffset.UtcNow, RoadmapState.CoreReady, RoadmapState.SelectNextStrategicInitiative, "SelectNextEpic", "projection", "contract", new Dictionary<string, string>(), ["output"], 0, "Started", "None", null));
        await store.AppendAsync(new TransitionJournalRecord("TransitionCompleted", "abc", DateTimeOffset.UtcNow, RoadmapState.CoreReady, RoadmapState.SelectNextStrategicInitiative, "SelectNextEpic", "projection", "contract", new Dictionary<string, string>(), ["output"], 10, "Completed", "Select Existing Epic", null));

        string[] lines = repo.Read(RoadmapArtifactPaths.TransitionJournal).Trim().Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Contains("abc", lines[0], StringComparison.Ordinal);
        Assert.NotNull(JsonSerializer.Deserialize<TransitionJournalRecord>(lines[1], new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
