using System.Text.Json;
using LoopRelay.Roadmap.Cli;
using DecisionLedgerStore = LoopRelay.Roadmap.Cli.DecisionLedgerStore;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class DecisionRecorderTests
{
    [Fact]
    public async Task Append_allocates_decision_id_and_preserves_entry_fields()
    {
        using var repo = new TempRepo();
        var recorder = new Cli.DecisionRecorder(new DecisionLedgerStore(repo.Artifacts));
        DateTimeOffset before = DateTimeOffset.UtcNow;

        await recorder.AppendAsync(
            Cli.RoadmapState.SelectNextStrategicInitiative,
            "SelectNextEpic",
            ".agents/projections/select-next-epic.md",
            Cli.RoadmapArtifactPaths.Selection,
            "Select Existing Epic",
            "High",
            "Selection rationale.");

        DateTimeOffset after = DateTimeOffset.UtcNow;
        Cli.DecisionLedgerEntry entry = Assert.Single(ReadEntries(repo));
        Assert.Equal("D0001", entry.DecisionId);
        Assert.InRange(entry.Timestamp, before, after);
        Assert.Equal(Cli.RoadmapState.SelectNextStrategicInitiative, entry.State);
        Assert.Equal("SelectNextEpic", entry.Transition);
        Assert.Equal("SelectNextEpic", entry.Prompt);
        Assert.Equal(".agents/projections/select-next-epic.md", entry.ProjectionPath);
        Assert.Empty(entry.InputArtifactPaths);
        Assert.Equal([Cli.RoadmapArtifactPaths.Selection], entry.OutputArtifactPaths);
        Assert.Equal("Select Existing Epic", entry.Decision);
        Assert.Equal("High", entry.Confidence);
        Assert.Equal("Selection rationale.", entry.RationaleExcerpt);
    }

    [Fact]
    public async Task Append_allocates_next_id_from_existing_ledger()
    {
        using var repo = new TempRepo();
        var store = new DecisionLedgerStore(repo.Artifacts);
        await store.AppendAsync(Entry("D0007"));
        var recorder = new Cli.DecisionRecorder(store);

        await recorder.AppendAsync(
            Cli.RoadmapState.CompletionEvaluationAndContextUpdate,
            "EvaluateEpicCompletionAndDrift",
            ".agents/projections/evaluate-epic-completion-and-drift.md",
            ".agents/evidence/evaluations/epic-completion-and-drift.0001.md",
            "Continue",
            "Unclear",
            "Evidence still needs work.");

        Cli.DecisionLedgerEntry[] entries = [..ReadEntries(repo)];
        Assert.Equal(["D0007", "D0008"], entries.Select(entry => entry.DecisionId));
        Assert.Equal("D0008", await store.LastDecisionIdAsync());
        Assert.Equal("Continue", entries.Single(entry => entry.DecisionId == "D0008").Decision);
    }

    [Fact]
    public async Task Append_keeps_output_argument_as_single_ledger_output()
    {
        using var repo = new TempRepo();
        var recorder = new Cli.DecisionRecorder(new DecisionLedgerStore(repo.Artifacts));

        await recorder.AppendAsync(
            Cli.RoadmapState.CompletionEvaluationAndContextUpdate,
            "CompletionCertificationRouting",
            "projection-path",
            "first-output.md, second-output.md",
            "Roadmap Completion Context Updated",
            "Unclear",
            "Completion context updated.");

        Cli.DecisionLedgerEntry entry = Assert.Single(ReadEntries(repo));
        Assert.Equal(["first-output.md, second-output.md"], entry.OutputArtifactPaths);
    }

    private static IReadOnlyList<Cli.DecisionLedgerEntry> ReadEntries(TempRepo repo)
    {
        Cli.DecisionLedgerPersistenceDocument document = JsonSerializer.Deserialize<Cli.DecisionLedgerPersistenceDocument>(
            repo.Read(Cli.RoadmapArtifactPaths.DecisionLedgerJson),
            Cli.RoadmapJson.Options)!;
        return document.ToDomain();
    }

    private static Cli.DecisionLedgerEntry Entry(string decisionId) =>
        new(
            decisionId,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            Cli.RoadmapState.SelectNextStrategicInitiative,
            "SelectNextEpic",
            "SelectNextEpic",
            "projection",
            [],
            [Cli.RoadmapArtifactPaths.Selection],
            "Select Existing Epic",
            "High",
            "reason");
}
