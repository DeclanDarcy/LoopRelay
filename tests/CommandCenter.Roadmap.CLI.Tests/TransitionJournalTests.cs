using System.Text.Json;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;
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

        TransitionJournalRecord? record = JsonSerializer.Deserialize<TransitionJournalRecord>(
            legacy,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(record);
        Assert.Null(record.InputSnapshot);
        Assert.Equal("hash", record.InputArtifactHashes[RoadmapArtifactPaths.RoadmapFile]);
    }

    [Fact]
    public async Task Prompt_transition_reuses_snapshot_when_inputs_change_during_execution()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "completion");
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap v1");
        var runtime = new MutatingRuntime(
            onRuntimePrompt: () => repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap v2"),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        TransitionJournalRecord[] records = ReadJournal(repo)
            .Where(record => record.Prompt == "SelectNextEpic")
            .ToArray();
        TransitionJournalRecord started = records.Single(record => record.Event == "TransitionStarted");
        TransitionJournalRecord completed = records.Single(record => record.Event == "TransitionCompleted");

        Assert.NotNull(started.InputSnapshot);
        Assert.NotNull(completed.InputSnapshot);
        Assert.Equal(started.InputSnapshot.SnapshotHash, completed.InputSnapshot.SnapshotHash);
        Assert.Equal(started.InputArtifactHashes, completed.InputArtifactHashes);
        Assert.Equal(RoadmapHash.Sha256("roadmap v1"), started.InputArtifactHashes[RoadmapArtifactPaths.RoadmapFile]);
        Assert.Equal("roadmap v2", repo.Read(RoadmapArtifactPaths.RoadmapFile));
    }

    [Fact]
    public async Task Prompt_failure_reuses_started_snapshot()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "completion");
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Failed("selection failed"));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);

        TransitionJournalRecord[] records = ReadJournal(repo)
            .Where(record => record.Prompt == "SelectNextEpic")
            .ToArray();
        TransitionJournalRecord started = records.Single(record => record.Event == "TransitionStarted");
        TransitionJournalRecord failed = records.Single(record => record.Event == "TransitionFailed");

        Assert.NotNull(started.InputSnapshot);
        Assert.NotNull(failed.InputSnapshot);
        Assert.Equal(started.InputSnapshot.SnapshotHash, failed.InputSnapshot.SnapshotHash);
        Assert.Equal(started.InputArtifactHashes, failed.InputArtifactHashes);
    }

    private static TransitionJournalRecord[] ReadJournal(TempRepo repo) =>
        repo.Read(RoadmapArtifactPaths.TransitionJournal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
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
                runtime.RunOneShotAsync(AgentSpecs.ReadOnlyPlanning(new Repository { Id = Guid.NewGuid(), Path = Directory.GetCurrentDirectory() }), prompt, onChunk, cancellationToken);

            public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
