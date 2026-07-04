using System.Text.Json;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using CommandCenter.Orchestration.Streaming;

namespace CommandCenter.Orchestration.Tests;

/// <summary>
/// Stream LIFECYCLE contracts for the three orchestration SSE streams (m8 slice "Stream contracts"). The
/// payload contract (per-event field shapes) is governed by the stream-trace goldens + consumer verification;
/// this suite governs the SEPARATE lifecycle contract by running REAL orchestrator scenarios through the §7
/// harness and asserting ordering, terminal-event, failure-event, and Last-Event-ID reconnect/replay
/// guarantees. The FAITHFULNESS cross-check binds the hand-authored goldens to the real producer: every
/// emitted envelope Type is one of the golden's event types for that stream, and each event's data
/// property-name set equals the golden entry's field-name set (minus `type`), so a producer change that
/// adds/removes a field fails the contract.
/// </summary>
public sealed class OrchestrationStreamLifecycleTests
{
    private const string PlanBody = "PLAN BODY";
    private const string OperationalContext = "OPERATIONAL CONTEXT";
    private const string Handoff = "EXECUTION SESSION REPORT";

    // ---- 1. ORDERING + 2. TERMINAL + 5. FAITHFULNESS: plan write run ----

    [Fact]
    public async Task Plan_write_run_orders_turn_started_before_deltas_before_completed_terminal()
    {
        var runtime = new FakeAgentRuntime { ScriptedChunks = new[] { "Plan A ", "Plan B" } };
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);
        runtime.OnTurn = () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), "RENDERED PLAN");

        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        await orchestrator.PlanningTurnTask;

        List<OrchestratorStreamEvent> events = await DrainUntilAsync(orchestrator.PlanningStream, "completed", "failed");

        Assert.Equal("turn-started", events[0].Type);
        int firstDelta = events.FindIndex(e => e.Type == "delta");
        int completed = events.FindIndex(e => e.Type == "completed");
        Assert.True(firstDelta > 0, "turn-started must precede the first delta");
        Assert.True(firstDelta < completed, "deltas must precede completed");
        Assert.Equal("completed", events[^1].Type); // terminal is completed for a successful run
        await AssertNoFurtherFramesAsync(orchestrator.PlanningStream, events[^1].Sequence);
        AssertFaithfulToGolden(events, "plan-stream.golden.json");
    }

    [Fact]
    public async Task Plan_write_run_terminal_is_failed_with_reason_and_detail_when_the_turn_does_not_complete()
    {
        // A non-Completed turn drives the producer's { reason, detail } failure (the representative the golden
        // pins). The other failure path (turn completes but no plan written) publishes { reason } only.
        var runtime = new FakeAgentRuntime { TurnState = AgentTurnState.Failed, TurnOutput = "boom" };
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        await orchestrator.PlanningTurnTask;

        List<OrchestratorStreamEvent> events = await DrainUntilAsync(orchestrator.PlanningStream, "completed", "failed");

        OrchestratorStreamEvent terminal = events[^1];
        Assert.Equal("failed", terminal.Type);
        Assert.False(string.IsNullOrEmpty(Field(terminal, "reason")));
        Assert.Equal("boom", Field(terminal, "detail"));
        await AssertNoFurtherFramesAsync(orchestrator.PlanningStream, terminal.Sequence);
        AssertFaithfulToGolden(events, "plan-stream.golden.json");
    }

    // ---- 1. ORDERING + 2. TERMINAL + 5. FAITHFULNESS: execute run ----

    [Fact]
    public async Task Execute_run_orders_run_started_first_and_a_completed_terminal_last()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var publisher = new FakePlanArtifactPublisher();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator =
            OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, publisher: publisher);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        ScriptMilestoneExtraction(runtime, store, repository);
        ScriptStartExecution(runtime, store, repository, "HANDOFF V1");

        Task<List<OrchestratorStreamEvent>> drain = DrainUntilAsync(orchestrator.ExecutionStream, "completed", "failed");
        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;
        List<OrchestratorStreamEvent> events = await drain;

        Assert.Equal("run-started", events[0].Type);
        Assert.Equal("completed", events[^1].Type);

        // The producer's real success order (RunExecutionAsync): run-started -> milestones-extracted ->
        // committed -> lifecycle -> handoff-rotated -> completed. Pin each intermediate terminal-bearing event
        // is present and in that strictly increasing position.
        int runStarted = events.FindIndex(e => e.Type == "run-started");
        int milestonesExtracted = events.FindIndex(e => e.Type == "milestones-extracted");
        int committed = events.FindIndex(e => e.Type == "committed");
        int lifecycle = events.FindIndex(e => e.Type == "lifecycle");
        int handoffRotated = events.FindIndex(e => e.Type == "handoff-rotated");
        int completed = events.FindIndex(e => e.Type == "completed");
        Assert.True(runStarted >= 0, "run-started must be present");
        Assert.True(milestonesExtracted >= 0, "milestones-extracted must be present");
        Assert.True(committed >= 0, "committed must be present");
        Assert.True(lifecycle >= 0, "lifecycle must be present");
        Assert.True(handoffRotated >= 0, "handoff-rotated must be present");
        Assert.True(completed >= 0, "completed must be present");
        Assert.True(runStarted < milestonesExtracted, "run-started must precede milestones-extracted");
        Assert.True(milestonesExtracted < committed, "milestones-extracted must precede committed");
        Assert.True(committed < lifecycle, "committed must precede lifecycle");
        Assert.True(lifecycle < handoffRotated, "lifecycle must precede handoff-rotated");
        Assert.True(handoffRotated < completed, "handoff-rotated must precede completed");

        await AssertNoFurtherFramesAsync(orchestrator.ExecutionStream, events[^1].Sequence);
        AssertFaithfulToGolden(events, "execution-stream.golden.json");
    }

    [Fact]
    public async Task Execute_run_terminal_is_failed_with_a_reason_and_phase_when_milestone_extraction_fails()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "extract boom"));

        Task<List<OrchestratorStreamEvent>> drain = DrainUntilAsync(orchestrator.ExecutionStream, "completed", "failed");
        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;
        List<OrchestratorStreamEvent> events = await drain;

        OrchestratorStreamEvent terminal = events[^1];
        Assert.Equal("failed", terminal.Type);
        Assert.False(string.IsNullOrEmpty(Field(terminal, "reason")));
        Assert.Equal("ExtractMilestones", Field(terminal, "phase")); // the producer includes phase on execution failures
        await AssertNoFurtherFramesAsync(orchestrator.ExecutionStream, terminal.Sequence);
        AssertFaithfulToGolden(events, "execution-stream.golden.json");
    }

    // ---- 1. ORDERING + 2. TERMINAL + 3. FAILURE + 5. FAITHFULNESS: decision run ----

    [Fact]
    public async Task Decision_run_orders_run_started_before_review_ready_terminal()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await SeedDecisionAsync(store, repository);
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                          // seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "PROPOSED DECISIONS")); // proposal

        await orchestrator.BeginDecisionRunAsync(repository);
        await orchestrator.DecisionRunTask;
        List<OrchestratorStreamEvent> events = await DrainUntilAsync(orchestrator.DecisionStream, "review-ready", "failed");

        Assert.Equal("run-started", events[0].Type);
        int runStarted = events.FindIndex(e => e.Type == "run-started");
        int reviewReady = events.FindIndex(e => e.Type == "review-ready");
        Assert.True(runStarted < reviewReady, "run-started must precede review-ready");
        Assert.Equal("review-ready", events[^1].Type); // terminal of a successful decision run
        await AssertNoFurtherFramesAsync(orchestrator.DecisionStream, events[^1].Sequence);
        AssertFaithfulToGolden(events, "decision-stream.golden.json");
    }

    [Fact]
    public async Task Decision_run_failed_event_carries_a_reason_and_phase_when_seeding_fails()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await SeedDecisionAsync(store, repository);
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "seed boom"));

        await orchestrator.BeginDecisionRunAsync(repository);
        await orchestrator.DecisionRunTask;
        List<OrchestratorStreamEvent> events = await DrainUntilAsync(orchestrator.DecisionStream, "review-ready", "failed");

        OrchestratorStreamEvent terminal = events[^1];
        Assert.Equal("failed", terminal.Type);
        Assert.False(string.IsNullOrEmpty(Field(terminal, "reason")));
        Assert.False(string.IsNullOrEmpty(Field(terminal, "phase"))); // the producer includes phase on decision failures
        await AssertNoFurtherFramesAsync(orchestrator.DecisionStream, terminal.Sequence);
        AssertFaithfulToGolden(events, "decision-stream.golden.json");
    }

    // ---- 5. FAITHFULNESS: the previously-unbound decision event types (transfer route + submit) ----

    [Fact]
    public async Task Transfer_routed_decision_run_binds_run_started_phase_and_transferred_events_to_the_producer()
    {
        // Drive a REAL Transfer-routed decision run so the producer emits run-started(route=Transfer), the
        // transfer phase events, and `transferred` — binding those event types' field-name+kind sets to the
        // golden (they are unreachable through the warm Continue path the other decision scenarios take).
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator =
            OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedDecisionLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository); // primes the warm process (transfer-eligible)
        ScriptTransferTurns(runtime, store, repository, proposal: "NEXT DECISIONS");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // Drain through the seed run's review-ready AND the transfer run's review-ready (the transfer is the tail).
        List<OrchestratorStreamEvent> all = await DrainDecisionTerminalsAsync(orchestrator.DecisionStream, 2);
        int firstReview = all.FindIndex(e => e.Type == "review-ready");
        List<OrchestratorStreamEvent> transferEvents = all.Skip(firstReview + 1).ToList();

        // The transfer run announced route=Transfer, streamed the transfer phases, and emitted `transferred`.
        Assert.Equal("Transfer", Field(transferEvents.First(e => e.Type == "run-started"), "route"));
        Assert.Contains(transferEvents, e => e.Type == "transferred");
        AssertFaithfulToGolden(transferEvents, "decision-stream.golden.json");
    }

    [Fact]
    public async Task Submit_emits_a_producer_bound_submitted_event_on_the_decision_stream()
    {
        // BeginSubmitDecisionsAsync publishes `submitted` on the DECISION stream (and starts a continuation on
        // the EXECUTION stream — that is fine, we only drain the decision stream for the submitted frame). This
        // binds the `submitted` event type (unreachable through the decision-run scenarios) to the producer.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await SeedDecisionLoopAsync(orchestrator, store, repository);
        // The continuation one-shot writes the next live handoff so the loop completes cleanly.
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO")));
        // The auto-started next decision run reuses the warm process: seed then propose.
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "NEXT DECISIONS"));

        Task<List<OrchestratorStreamEvent>> submitted = DrainUntilAsync(orchestrator.DecisionStream, "submitted");
        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        List<OrchestratorStreamEvent> events = await submitted;
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        OrchestratorStreamEvent submittedEvent = events.Single(e => e.Type == "submitted");
        Assert.Equal(OrchestrationArtifactPaths.Decisions, Field(submittedEvent, "path"));
        AssertFaithfulToGolden(new[] { submittedEvent }, "decision-stream.golden.json");
    }

    // ---- 4. RECONNECT / REPLAY (Last-Event-ID contract) ----

    [Fact]
    public async Task Sequences_are_strictly_monotonic_from_one_and_replay_after_a_sequence_yields_only_later_frames()
    {
        var runtime = new FakeAgentRuntime { ScriptedChunks = new[] { "A ", "B" } };
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);
        runtime.OnTurn = () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), "RENDERED PLAN");

        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        await orchestrator.PlanningTurnTask;

        List<OrchestratorStreamEvent> all = await DrainUntilAsync(orchestrator.PlanningStream, "completed", "failed");

        // Sequences are strictly monotonic, starting at 1.
        Assert.Equal(1, all[0].Sequence);
        for (int i = 1; i < all.Count; i++)
        {
            Assert.Equal(all[i - 1].Sequence + 1, all[i].Sequence);
        }

        // Last-Event-ID replay: subscribing afterSequence: k yields ONLY frames with Sequence > k.
        long k = all[1].Sequence;
        List<OrchestratorStreamEvent> replayed = await DrainAfterAsync(orchestrator.PlanningStream, k, all[^1].Sequence);
        Assert.NotEmpty(replayed);
        Assert.All(replayed, frame => Assert.True(frame.Sequence > k));
        Assert.Equal(all.Where(e => e.Sequence > k).Select(e => e.Sequence), replayed.Select(e => e.Sequence));
    }

    // ---- faithfulness cross-check ----

    // Binds the real producer to the hand-authored golden: every emitted event's Type is a golden event type,
    // its data property-NAME set equals the golden entry's field-name set (minus `type`), AND each field's
    // actual JsonValueKind matches the golden's kind. The kind check means a producer RETYPE (e.g. `pushed`
    // bool->string, `count` int->string) fails the contract, not just an add/remove of a field. Booleans are
    // normalized so True/False compare equal regardless of value.
    private static void AssertFaithfulToGolden(IReadOnlyList<OrchestratorStreamEvent> events, string goldenFileName)
    {
        Dictionary<string, Dictionary<string, JsonValueKind>> goldenFields = ReadGoldenFieldKinds(goldenFileName);
        foreach (OrchestratorStreamEvent streamEvent in events)
        {
            Assert.True(
                goldenFields.ContainsKey(streamEvent.Type),
                $"Emitted event type '{streamEvent.Type}' is not represented in {goldenFileName}.");

            Dictionary<string, JsonValueKind> actualFields = JsonDocument.Parse(streamEvent.Data).RootElement
                .EnumerateObject()
                .ToDictionary(property => property.Name, property => NormalizeKind(property.Value.ValueKind), StringComparer.Ordinal);
            Dictionary<string, JsonValueKind> expectedFields = goldenFields[streamEvent.Type];

            Assert.True(
                actualFields.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedFields.Keys),
                $"Event '{streamEvent.Type}' data fields [{string.Join(",", actualFields.Keys.OrderBy(f => f))}] " +
                $"do not equal golden fields [{string.Join(",", expectedFields.Keys.OrderBy(f => f))}].");

            foreach ((string name, JsonValueKind expectedKind) in expectedFields)
            {
                Assert.True(
                    actualFields[name] == expectedKind,
                    $"Event '{streamEvent.Type}' field '{name}' has value-kind {actualFields[name]} but golden expects {expectedKind}.");
            }
        }
    }

    private static Dictionary<string, Dictionary<string, JsonValueKind>> ReadGoldenFieldKinds(string goldenFileName)
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ContractFixtures", goldenFileName));
        JsonElement root = JsonDocument.Parse(json).RootElement;
        var result = new Dictionary<string, Dictionary<string, JsonValueKind>>(StringComparer.Ordinal);
        foreach (JsonElement entry in root.GetProperty("events").EnumerateArray())
        {
            string type = entry.GetProperty("type").GetString()!;
            Dictionary<string, JsonValueKind> fields = entry.EnumerateObject()
                .Where(property => property.Name != "type")
                .ToDictionary(property => property.Name, property => NormalizeKind(property.Value.ValueKind), StringComparer.Ordinal);
            result[type] = fields;
        }

        return result;
    }

    // Collapse JsonValueKind.True / JsonValueKind.False to a single Boolean kind so a boolean field compares
    // equal regardless of its value (a representative `pushed = true` must accept a real `pushed = false`).
    private static JsonValueKind NormalizeKind(JsonValueKind kind) =>
        kind is JsonValueKind.True or JsonValueKind.False ? JsonValueKind.True : kind;

    // ---- harness helpers (copied per §7) ----

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    private static async Task WritePlanAsync(
        RepositoryOrchestrator orchestrator, FakeAgentRuntime runtime, FakeArtifactStore store, Repository repository)
    {
        runtime.OnTurn = () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), PlanBody);
        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        await orchestrator.PlanningTurnTask;
        runtime.OnTurn = null;
    }

    private static void ScriptMilestoneExtraction(
        FakeAgentRuntime runtime, FakeArtifactStore store, Repository repository)
    {
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: async () =>
        {
            await store.WriteAsync(Resolve(repository, $"{OrchestrationArtifactPaths.MilestonesDirectory}/m1.md"), "milestone");
            await store.WriteAsync(Resolve(repository, $"{OrchestrationArtifactPaths.MilestonesDirectory}/m2.md"), "milestone");
        }));
    }

    private static void ScriptStartExecution(
        FakeAgentRuntime runtime, FakeArtifactStore store, Repository repository, string handoff)
    {
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), handoff)));
    }

    private static async Task SeedDecisionAsync(FakeArtifactStore store, Repository repository)
    {
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), Handoff);
    }

    // The minimal pre-state the continuation/transfer loop needs (copied from RepositoryOrchestrator{Continuation,
    // Transfer}Tests): a cached plan, an operational context (the decision gate requires it), and a first rotated
    // handoff to build on.
    private static async Task SeedDecisionLoopAsync(RepositoryOrchestrator orchestrator, FakeArtifactStore store, Repository repository)
    {
        orchestrator.RecordPlan(PlanBody);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), Handoff);
    }

    // A prior Continue decision run primes the warm process (decisionSeeded == true) so a later Transfer is
    // eligible (copied from RepositoryOrchestratorTransferTests).
    private static async Task SeedWarmDecisionSessionAsync(RepositoryOrchestrator orchestrator, FakeAgentRuntime runtime, Repository repository)
    {
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                         // StartDecisionSession seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "SEED DECISIONS")); // initial proposal
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;
    }

    // Scripts the two one-shots (continuation, then context rewrite) and three decision-session turns (delta,
    // reseed, proposal) a happy-path transfer consumes after a submit (copied from RepositoryOrchestratorTransferTests).
    private static void ScriptTransferTurns(FakeAgentRuntime runtime, FakeArtifactStore store, Repository repository, string proposal)
    {
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO")));
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), "REWRITTEN CONTEXT")));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "OPERATIONAL DELTA"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: proposal));
    }

    // Drains until the Nth terminal (review-ready or failed) so a test can reach a transfer run's terminal that
    // follows the seed run's review-ready (copied from RepositoryOrchestratorTransferTests).
    private static async Task<List<OrchestratorStreamEvent>> DrainDecisionTerminalsAsync(OrchestratorStreamChannel stream, int terminalCount)
    {
        var events = new List<OrchestratorStreamEvent>();
        int seen = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (OrchestratorStreamEvent streamEvent in stream.SubscribeAsync(0, cts.Token))
        {
            events.Add(streamEvent);
            if (streamEvent.Type is "review-ready" or "failed" && ++seen >= terminalCount)
            {
                break;
            }
        }

        return events;
    }

    private static async Task<List<OrchestratorStreamEvent>> DrainUntilAsync(
        OrchestratorStreamChannel stream, params string[] terminalTypes)
    {
        var events = new List<OrchestratorStreamEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (OrchestratorStreamEvent streamEvent in stream.SubscribeAsync(0, cts.Token))
        {
            events.Add(streamEvent);
            if (terminalTypes.Contains(streamEvent.Type))
            {
                break;
            }
        }

        return events;
    }

    private static async Task<List<OrchestratorStreamEvent>> DrainAfterAsync(
        OrchestratorStreamChannel stream, long afterSequence, long lastSequence)
    {
        var events = new List<OrchestratorStreamEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (OrchestratorStreamEvent streamEvent in stream.SubscribeAsync(afterSequence, cts.Token))
        {
            events.Add(streamEvent);
            if (streamEvent.Sequence >= lastSequence)
            {
                break;
            }
        }

        return events;
    }

    // Asserts nothing was published after the terminal: a replay strictly after the terminal sequence yields no
    // frames within a short drain window.
    private static async Task AssertNoFurtherFramesAsync(OrchestratorStreamChannel stream, long terminalSequence)
    {
        Assert.Equal(terminalSequence, stream.LastSequence);
        var events = new List<OrchestratorStreamEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        try
        {
            await foreach (OrchestratorStreamEvent streamEvent in stream.SubscribeAsync(terminalSequence, cts.Token))
            {
                events.Add(streamEvent);
            }
        }
        catch (OperationCanceledException)
        {
            // expected: no terminal-after frame ever arrives, so the subscription is cancelled by the window.
        }

        Assert.Empty(events);
    }

    private static string? Field(OrchestratorStreamEvent streamEvent, string property) =>
        JsonDocument.Parse(streamEvent.Data).RootElement.TryGetProperty(property, out JsonElement value)
            ? value.GetString()
            : null;
}
