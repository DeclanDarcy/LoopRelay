using System.Text.Json;
using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

public sealed class DecisionLedgerTests
{
    [Fact]
    public async Task Appends_decision_entries()
    {
        using var repo = new TempRepo();
        var store = new Cli.Services.DecisionLedgerStore(repo.Artifacts);
        string id = await store.NextDecisionIdAsync();

        await store.AppendAsync(new DecisionLedgerEntry(id, DateTimeOffset.UtcNow, RoadmapState.SelectNextStrategicInitiative, "SelectNextEpic", "SelectNextEpic", "projection", ["input"], ["output"], "Select Existing Epic", "High", "reason"));

        Assert.Equal("D0001", await store.LastDecisionIdAsync());
        string ledgerJson = repo.Read(RoadmapArtifactPaths.DecisionLedgerJson);
        Assert.Contains("Select Existing Epic", ledgerJson, StringComparison.Ordinal);
        Assert.Contains("\"SchemaVersion\": \"decision-ledger.v1\"", ledgerJson, StringComparison.Ordinal);
        Assert.False(Exists(repo, RoadmapArtifactPaths.DecisionLedger));
    }

    [Fact]
    public async Task Uses_json_for_decision_ids_when_markdown_projection_drifted()
    {
        using var repo = new TempRepo();
        var store = new Cli.Services.DecisionLedgerStore(repo.Artifacts);
        await store.AppendAsync(Entry("D0001"));
        repo.Write(RoadmapArtifactPaths.DecisionLedger, """
                                                        # Decision Ledger

                                                        ## D9999

                                                        | Field | Value |
                                                        |---|---|
                                                        | Timestamp | 2026-01-01T00:00:00.0000000+00:00 |
                                                        """);

        Assert.Equal("D0001", await store.LastDecisionIdAsync());
        Assert.Equal("D0002", await store.NextDecisionIdAsync());
    }

    [Fact]
    public async Task Preserves_delimiter_bearing_values_in_structured_ledger()
    {
        using var repo = new TempRepo();
        var store = new Cli.Services.DecisionLedgerStore(repo.Artifacts);

        await store.AppendAsync(Entry(
            "D0001",
            inputPaths: ["input|a", "C:\\input\\b<br>literal"],
            outputPaths: ["output|a"],
            rationale: "reason line 1\nreason line 2 | pipe \\ slash"));

        DecisionLedgerPersistenceDocument document = JsonSerializer.Deserialize<DecisionLedgerPersistenceDocument>(
            repo.Read(RoadmapArtifactPaths.DecisionLedgerJson),
            RoadmapJson.Options)!;

        DecisionLedgerEntry loaded = Assert.Single(document.ToDomain());
        Assert.Equal(["input|a", "C:\\input\\b<br>literal"], loaded.InputArtifactPaths);
        Assert.Equal("reason line 1\nreason line 2 | pipe \\ slash", loaded.RationaleExcerpt);
    }

    [Fact]
    public async Task Migrates_valid_legacy_ledger()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.DecisionLedger, """
                                                        # Decision Ledger

                                                        ## D0007

                                                        | Field | Value |
                                                        |---|---|
                                                        | Timestamp | 2026-01-01T00:00:00.0000000+00:00 |
                                                        | State | SelectNextStrategicInitiative |
                                                        | Transition | SelectNextEpic |
                                                        | Prompt | SelectNextEpic |
                                                        | Projection Path | projection |
                                                        | Input Artifact Paths | input\|a<br>C:\input\b |
                                                        | Output Artifact Paths | output\|a |
                                                        | Decision / Disposition | Select Existing Epic |
                                                        | Confidence | High |
                                                        | Rationale Excerpt | reason |
                                                        """);

        var store = new Cli.Services.DecisionLedgerStore(repo.Artifacts);

        Assert.Equal("D0007", await store.LastDecisionIdAsync());
        Assert.Equal("D0008", await store.NextDecisionIdAsync());
        Assert.True(Exists(repo, RoadmapArtifactPaths.DecisionLedgerJson));
    }

    [Fact]
    public async Task Rejects_malformed_legacy_ledger_without_migration()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.DecisionLedger, """
                                                        # Decision Ledger

                                                        ## D0001

                                                        | Field | Value |
                                                        |---|---|
                                                        | Decision / Disposition | Select | Existing Epic |
                                                        """);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => new Cli.Services.DecisionLedgerStore(repo.Artifacts).LastDecisionIdAsync());

        Assert.Contains("cannot be migrated", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Exists(repo, RoadmapArtifactPaths.DecisionLedgerJson));
    }

    private static DecisionLedgerEntry Entry(
        string decisionId,
        IReadOnlyList<string>? inputPaths = null,
        IReadOnlyList<string>? outputPaths = null,
        string rationale = "reason") =>
        new(
            decisionId,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            RoadmapState.SelectNextStrategicInitiative,
            "SelectNextEpic",
            "SelectNextEpic",
            "projection",
            inputPaths ?? ["input"],
            outputPaths ?? ["output"],
            "Select Existing Epic",
            "High",
            rationale);

    private static bool Exists(TempRepo repo, string relativePath) =>
        File.Exists(Path.Combine(repo.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
