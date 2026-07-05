using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class DecisionLedgerTests
{
    [Fact]
    public async Task Appends_decision_entries()
    {
        using var repo = new TempRepo();
        var store = new DecisionLedgerStore(repo.Artifacts);
        string id = await store.NextDecisionIdAsync();

        await store.AppendAsync(new DecisionLedgerEntry(id, DateTimeOffset.UtcNow, RoadmapState.SelectNextStrategicInitiative, "SelectNextEpic", "SelectNextEpic", "projection", ["input"], ["output"], "Select Existing Epic", "High", "reason"));

        Assert.Equal("D0001", await store.LastDecisionIdAsync());
        Assert.Contains("Select Existing Epic", repo.Read(RoadmapArtifactPaths.DecisionLedger), StringComparison.Ordinal);
    }
}
