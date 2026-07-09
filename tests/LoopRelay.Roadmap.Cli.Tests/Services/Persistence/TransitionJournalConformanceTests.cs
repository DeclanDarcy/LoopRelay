using System.Text.Json;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.TransitionState;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Persistence;

public sealed class TransitionJournalConformanceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Journal_appends_started_completed_and_failed_records_in_order_as_jsonl()
    {
        using var repo = new TempRepo();
        ITransitionJournalStore store = new TransitionJournalStore(repo.Artifacts);
        TransitionInputSnapshot snapshot = Snapshot("SelectNextEpic");

        await store.AppendAsync(Record("TransitionStarted", "correlation-001", "Started", "None", null, 0, snapshot));
        await store.AppendAsync(Record("TransitionCompleted", "correlation-001", "Completed", "Select Existing Epic", null, 25, snapshot));
        await store.AppendAsync(Record("TransitionFailed", "correlation-002", "Failed", "None", "selection failed", 30, snapshot));

        string journal = repo.Read(RoadmapArtifactPaths.TransitionJournal);
        Assert.EndsWith(Environment.NewLine, journal, StringComparison.Ordinal);
        string[] lines = journal.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.All(lines, line => Assert.DoesNotContain('\n', line));
        Assert.Contains("\"correlationId\":\"correlation-001\"", lines[0], StringComparison.Ordinal);

        TransitionJournalRecord[] records = lines
            .Select(line => JsonSerializer.Deserialize<TransitionJournalRecord>(line, JsonOptions)!)
            .ToArray();

        Assert.Equal(["TransitionStarted", "TransitionCompleted", "TransitionFailed"], records.Select(record => record.Event).ToArray());
        Assert.Equal(["Started", "Completed", "Failed"], records.Select(record => record.Result).ToArray());
        Assert.Equal("Select Existing Epic", records[1].ParserDecision);
        Assert.Equal(25, records[1].DurationMilliseconds);
        Assert.Equal("selection failed", records[2].ErrorMessage);
        Assert.All(records, record => Assert.NotNull(record.InputSnapshot));
        Assert.Equal(snapshot.SnapshotHash, records[0].InputSnapshot!.SnapshotHash);
        Assert.Equal(snapshot.ToInputArtifactHashes(), records[1].InputArtifactHashes);
    }

    [Fact]
    public async Task Journal_trims_existing_trailing_whitespace_before_appending_next_record()
    {
        using var repo = new TempRepo();
        TransitionJournalRecord started = Record("TransitionStarted", "correlation-001", "Started", "None");
        TransitionJournalRecord completed = Record("TransitionCompleted", "correlation-001", "Completed", "None", durationMilliseconds: 10);
        string startedJson = JsonSerializer.Serialize(started, JsonOptions);
        string completedJson = JsonSerializer.Serialize(completed, JsonOptions);
        repo.Write(RoadmapArtifactPaths.TransitionJournal, startedJson + Environment.NewLine + Environment.NewLine + "  \t");

        await new TransitionJournalStore(repo.Artifacts).AppendAsync(completed);

        Assert.Equal(
            startedJson + Environment.NewLine + completedJson + Environment.NewLine,
            repo.Read(RoadmapArtifactPaths.TransitionJournal));
    }

    [Fact]
    public void Legacy_journal_records_without_input_snapshot_remain_deserializable()
    {
        const string legacyJson = """
            {"event":"TransitionStarted","correlationId":"correlation-legacy","timestamp":"2026-01-01T00:00:00+00:00","previousState":1,"attemptedState":2,"prompt":"SelectNextEpic","projection":".agents/projections/select-next-epic.md","promptContractKey":"SelectNextEpic","inputArtifactHashes":{".agents/roadmap/001-roadmap.md":"roadmap-hash"},"outputPaths":[".agents/selection.md"],"durationMilliseconds":0,"result":"Started","parserDecision":"None","errorMessage":null}
            """;

        TransitionJournalRecord? record = JsonSerializer.Deserialize<TransitionJournalRecord>(legacyJson, JsonOptions);

        Assert.NotNull(record);
        Assert.Equal("TransitionStarted", record!.Event);
        Assert.Equal("correlation-legacy", record.CorrelationId);
        Assert.Null(record.InputSnapshot);
        Assert.Equal("roadmap-hash", record.InputArtifactHashes[".agents/roadmap/001-roadmap.md"]);
    }

    private static TransitionJournalRecord Record(
        string eventName,
        string correlationId,
        string result,
        string parserDecision,
        string? errorMessage = null,
        long durationMilliseconds = 0,
        TransitionInputSnapshot? snapshot = null) =>
        new(
            eventName,
            correlationId,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            RoadmapState.CoreReady,
            RoadmapState.SelectNextStrategicInitiative,
            "SelectNextEpic",
            RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
            "SelectNextEpic",
            snapshot?.ToInputArtifactHashes() ?? new SortedDictionary<string, string>(StringComparer.Ordinal),
            [RoadmapArtifactPaths.Selection],
            durationMilliseconds,
            result,
            parserDecision,
            errorMessage,
            snapshot);

    private static TransitionInputSnapshot Snapshot(string prompt)
    {
        string projectionPath = RoadmapArtifactPaths.ProjectionPaths[prompt];
        return new TransitionInputSnapshot(
            prompt,
            new TransitionProjectionIdentity(prompt, projectionPath, "projection-hash"),
            [
                new TransitionArtifactInput(
                    projectionPath,
                    TransitionInputRole.Projection,
                    Required: true,
                    TransitionInputPresence.Present,
                    "projection-hash"),
                new TransitionArtifactInput(
                    ".agents/roadmap/001-roadmap.md",
                    TransitionInputRole.RoadmapSource,
                    Required: true,
                    TransitionInputPresence.Present,
                    "roadmap-hash"),
            ],
            "prompt-context-hash",
            "secondary-input-hash",
            "snapshot-hash");
    }
}
