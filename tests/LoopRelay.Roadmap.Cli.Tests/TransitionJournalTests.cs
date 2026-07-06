using System.Text.Json;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Repositories;
using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class TransitionJournalTests
{
    [Fact]
    public async Task Journal_records_started_and_completed_correlation_ids()
    {
        using var repo = new TempRepo();
        var store = new Cli.TransitionJournalStore(repo.Artifacts);

        await store.AppendAsync(new Cli.TransitionJournalRecord("TransitionStarted", "abc", DateTimeOffset.UtcNow, Cli.RoadmapState.CoreReady, Cli.RoadmapState.SelectNextStrategicInitiative, "SelectNextEpic", "projection", "contract", new Dictionary<string, string>(), ["output"], 0, "Started", "None", null));
        await store.AppendAsync(new Cli.TransitionJournalRecord("TransitionCompleted", "abc", DateTimeOffset.UtcNow, Cli.RoadmapState.CoreReady, Cli.RoadmapState.SelectNextStrategicInitiative, "SelectNextEpic", "projection", "contract", new Dictionary<string, string>(), ["output"], 10, "Completed", "Select Existing Epic", null));

        string[] lines = repo.Read(Cli.RoadmapArtifactPaths.TransitionJournal).Trim().Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Contains("abc", lines[0], StringComparison.Ordinal);
        Assert.NotNull(JsonSerializer.Deserialize<Cli.TransitionJournalRecord>(lines[1], new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void Journal_deserializes_legacy_records_without_input_snapshot()
    {
        const string legacy = """
            {
              "event": "TransitionStarted",
              "correlationId": "abc",
              "timestamp": "2026-01-01T00:00:00+00:00",
              "previousState": 1,
              "attemptedState": 2,
              "prompt": "SelectNextEpic",
              "projection": "projection",
              "promptContractKey": "contract",
              "inputArtifactHashes": {
                ".agents/roadmap.md": "hash"
              },
              "outputPaths": [
                ".agents/selection.md"
              ],
              "durationMilliseconds": 0,
              "result": "Started",
              "parserDecision": "None",
              "errorMessage": null
            }
            """;

        Cli.TransitionJournalRecord? record = JsonSerializer.Deserialize<Cli.TransitionJournalRecord>(
            legacy,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(record);
        Assert.Null(record.InputSnapshot);
        Assert.Equal("hash", record.InputArtifactHashes[Cli.RoadmapArtifactPaths.RoadmapFile]);
    }

    [Fact]
    public async Task Prompt_transition_reuses_snapshot_when_inputs_change_during_execution()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "completion");
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapFile, "roadmap v1");
        var runtime = new MutatingRuntime(
            onRuntimePrompt: () => repo.Write(Cli.RoadmapArtifactPaths.RoadmapFile, "roadmap v2"),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.TransitionJournalRecord[] records = ReadJournal(repo)
            .Where(record => record.Prompt == "SelectNextEpic")
            .ToArray();
        Cli.TransitionJournalRecord started = records.Single(record => record.Event == "TransitionStarted");
        Cli.TransitionJournalRecord completed = records.Single(record => record.Event == "TransitionCompleted");

        Assert.NotNull(started.InputSnapshot);
        Assert.NotNull(completed.InputSnapshot);
        Assert.Equal(started.InputSnapshot.SnapshotHash, completed.InputSnapshot.SnapshotHash);
        Assert.Equal(started.InputArtifactHashes, completed.InputArtifactHashes);
        Assert.Equal(Cli.RoadmapHash.Sha256("roadmap v1"), started.InputArtifactHashes[Cli.RoadmapArtifactPaths.RoadmapFile]);
        Assert.Equal("roadmap v2", repo.Read(Cli.RoadmapArtifactPaths.RoadmapFile));
    }

    [Fact]
    public async Task Prompt_failure_reuses_started_snapshot()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "completion");
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapFile, "roadmap");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Failed("selection failed"));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);

        Cli.TransitionJournalRecord[] records = ReadJournal(repo)
            .Where(record => record.Prompt == "SelectNextEpic")
            .ToArray();
        Cli.TransitionJournalRecord started = records.Single(record => record.Event == "TransitionStarted");
        Cli.TransitionJournalRecord failed = records.Single(record => record.Event == "TransitionFailed");

        Assert.NotNull(started.InputSnapshot);
        Assert.NotNull(failed.InputSnapshot);
        Assert.Equal(started.InputSnapshot.SnapshotHash, failed.InputSnapshot.SnapshotHash);
        Assert.Equal(started.InputArtifactHashes, failed.InputArtifactHashes);
    }

    [Fact]
    public async Task Bootstrap_journal_records_archived_epic_input_hashes()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapFile, "roadmap");
        const string archivedEpicPath = ".agents/archive/epics/001-done.md";
        const string archivedEpic = """
            # Epic: Done

            ## Completion Evidence

            Verified.
            """;
        repo.Write(archivedEpicPath, archivedEpic);
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateRoadmapCompletionContext")),
            ScriptedAgentRuntime.Completed("# Roadmap Completion Context"),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.TransitionJournalRecord started = ReadJournal(repo).Single(record =>
            record.Event == "TransitionStarted" &&
            record.Prompt == "CreateRoadmapCompletionContext");
        Assert.Equal(Cli.RoadmapHash.Sha256(archivedEpic), started.InputArtifactHashes[archivedEpicPath]);
        Assert.NotNull(started.InputSnapshot);
        Assert.Contains(started.InputSnapshot.ArtifactInputs, input =>
            input.Path == archivedEpicPath &&
            input.Roles == Cli.TransitionInputRole.CompletedEpic);
    }

    private static Cli.TransitionJournalRecord[] ReadJournal(TempRepo repo) =>
        repo.Read(Cli.RoadmapArtifactPaths.TransitionJournal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<Cli.TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .ToArray();

    private static string StrategicInvestigationSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Strategic Investigation Required |
        | Recommended Initiative | Investigate A |
        | Initiative Type | Strategic Investigation |
        | Confidence | Medium |
        | Primary Reason | Evidence is insufficient |
        """;

    private sealed class MutatingRuntime(Action onRuntimePrompt, params AgentTurnResult[] results) : IAgentRuntime
    {
        private readonly Queue<AgentTurnResult> results = new(results);

        public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken cancellationToken = default) =>
            Task.FromResult<IAgentSession>(new MutatingSession(this));

        public Task<AgentTurnResult> RunOneShotAsync(
            AgentSessionSpec spec,
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default)
        {
            AgentTurnResult result = results.Dequeue();
            if (prompt.Contains("# Roadmap Runtime Prompt Context", StringComparison.Ordinal))
            {
                onRuntimePrompt();
            }

            return Task.FromResult(result);
        }

        public ValueTask CloseSessionAsync(IAgentSession session) => session.DisposeAsync();

        private sealed class MutatingSession(MutatingRuntime runtime) : IAgentSession
        {
            public SessionIdentity SessionId { get; } = SessionIdentity.New();
            public string RepositoryId => "repo";
            public SessionRole Role => SessionRole.Planning;
            public AgentSessionMode Mode => AgentSessionMode.OneShot;
            public AgentProcessState State => AgentProcessState.Exited;
            public int CompletedTurns => 0;
            public AgentTokenUsage TotalUsage => AgentTokenUsage.Zero;
            public string? ThreadId => null;

            public Task<AgentTurnResult> RunTurnAsync(string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken cancellationToken = default) =>
                runtime.RunOneShotAsync(Cli.AgentSpecs.ReadOnlyPlanning(new Repository { Id = Guid.NewGuid(), Path = Directory.GetCurrentDirectory() }), prompt, onChunk, cancellationToken);

            public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
