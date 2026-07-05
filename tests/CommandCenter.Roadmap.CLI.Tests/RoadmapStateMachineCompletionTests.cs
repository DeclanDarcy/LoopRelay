using System.Text.Json;
using CommandCenter.Agents.Models;
using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class RoadmapStateMachineCompletionTests
{
    [Fact]
    public void Completion_router_has_explicit_route_for_every_valid_recommendation()
    {
        var router = new CompletionCertificationRouter();
        PromptContract contract = new PromptContractRegistry(new ProjectionRegistry()).Get("EvaluateEpicCompletionAndDrift");

        Assert.Equal(
            CompletionCertificationRouter.AllowedRecommendations.Order(StringComparer.Ordinal),
            router.All.Select(route => route.ClosureRecommendation).Order(StringComparer.Ordinal));
        Assert.Equal(
            CompletionCertificationRouter.AllowedRecommendations.Order(StringComparer.Ordinal),
            contract.AllowedDecisions.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Completion_router_rejects_incomplete_route_tables()
    {
        var complete = new CompletionCertificationRouter();
        IEnumerable<CompletionCertificationRoute> incomplete = complete.All
            .Where(route => route.ClosureRecommendation != "Continue Epic");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new CompletionCertificationRouter(incomplete));

        Assert.Contains("Continue Epic", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Completion_router_can_be_extended_with_future_routes()
    {
        var complete = new CompletionCertificationRouter();
        var extended = new CompletionCertificationRouter(complete.All.Append(new CompletionCertificationRoute(
            "Suspend Epic",
            CompletionTransitionIntent.GatherAdditionalEvidence,
            RoadmapState.EvidenceGathering,
            TransitionStatus.Paused,
            RoadmapOutcome.Paused,
            RequiresRoadmapCompletionContextUpdate: false,
            ActiveEpicLifecycleState: ArtifactLifecycleState.Ready,
            NextTransitions: ["SuspendEpic"])));

        CompletionCertificationRoute route = extended.Route(new CompletionEvaluationDecision(
            "Inconclusive",
            "Unknown",
            "Suspend Epic"));

        Assert.Equal("Suspend Epic", route.ClosureRecommendation);
        Assert.Equal(RoadmapState.EvidenceGathering, route.TargetState);
    }

    [Theory]
    [InlineData("Close Epic", "Completed", "SelectNextStrategicInitiative", "Completed", "UpdateRoadmapCompletionContext", "Completed", true)]
    [InlineData("Close With Follow-Up", "Completed", "SelectNextStrategicInitiative", "Completed", "UpdateRoadmapCompletionContext", "Completed", true)]
    [InlineData("Continue Epic", "Paused", "ExecutionLoop", "Paused", "ContinueExecution", "Executing", false)]
    [InlineData("Reopen Epic", "Paused", "EpicPreparationAudit", "Paused", "ReturnToEpicPreparationAudit", "Ready", false)]
    [InlineData("Gather More Evidence", "Paused", "EvidenceGathering", "Paused", "GatherAdditionalEvidence", "Ready", false)]
    public async Task Completion_recommendations_route_as_domain_transitions(
        string recommendation,
        string expectedOutcome,
        string expectedState,
        string expectedStatus,
        string expectedIntent,
        string expectedActiveEpicLifecycle,
        bool updatesCompletionContext)
    {
        CompletionRunResult result = await RunCompletionAsync(recommendation);

        RoadmapOutcome outcome = Enum.Parse<RoadmapOutcome>(expectedOutcome);
        RoadmapState state = Enum.Parse<RoadmapState>(expectedState);
        TransitionStatus status = Enum.Parse<TransitionStatus>(expectedStatus);
        ArtifactLifecycleState activeEpicLifecycle = Enum.Parse<ArtifactLifecycleState>(expectedActiveEpicLifecycle);

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(state, result.State.CurrentState);
        Assert.Equal(state, result.State.LastTransition.To);
        Assert.Equal(status, result.State.LastTransition.Status);
        Assert.Equal(recommendation, result.State.LastTransition.Decision);
        Assert.Equal(expectedIntent, result.State.TransitionIntent.Intent);
        Assert.Equal(state, result.State.TransitionIntent.DispatchState);
        Assert.Equal(activeEpicLifecycle, result.ActiveEpicLifecycle);
        Assert.DoesNotContain("| Status | Failed |", result.StateMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadmapStateMachine", result.StateMarkdown, StringComparison.Ordinal);

        string evidencePath = Assert.Single(result.State.TransitionIntent.EvidencePaths);
        Assert.StartsWith(RoadmapArtifactPaths.EvaluationEvidenceDirectory, evidencePath, StringComparison.Ordinal);
        Assert.Contains(recommendation, result.EvaluationEvidence, StringComparison.Ordinal);
        Assert.Contains("CompletionCertificationRouting", result.TransitionJournal, StringComparison.Ordinal);
        Assert.Contains(recommendation, result.TransitionJournal, StringComparison.Ordinal);

        if (updatesCompletionContext)
        {
            Assert.Contains("# Updated Roadmap Completion Context", result.CompletionContext, StringComparison.Ordinal);
            Assert.Equal(10, result.AgentCalls);
        }
        else
        {
            Assert.Equal("existing completion context", result.CompletionContext);
            Assert.Equal(8, result.AgentCalls);
        }
    }

    [Fact]
    public async Task Runtime_transitions_record_causal_input_snapshots()
    {
        CompletionRunResult result = await RunCompletionAsync("Close Epic");

        TransitionJournalRecord[] records = JournalRecords(result.TransitionJournal);

        AssertPromptSnapshot(records, "SelectNextEpic", [
            RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
            RoadmapArtifactPaths.RoadmapCompletionContext,
            RoadmapArtifactPaths.RoadmapFile,
        ]);
        AssertPromptSnapshot(records, "GenerateMilestoneDeepDivesForEpic", [
            RoadmapArtifactPaths.ProjectionPaths["GenerateMilestoneDeepDivesForEpic"],
            RoadmapArtifactPaths.ActiveEpic,
        ]);
        AssertPromptSnapshot(records, "EvaluateEpicCompletionAndDrift", [
            RoadmapArtifactPaths.ProjectionPaths["EvaluateEpicCompletionAndDrift"],
            RoadmapArtifactPaths.ActiveEpic,
            ".agents/specs/routing-test.md",
        ]);
        AssertPromptSnapshot(records, "UpdateRoadmapCompletionContext", [
            RoadmapArtifactPaths.ProjectionPaths["UpdateRoadmapCompletionContext"],
            RoadmapArtifactPaths.RoadmapCompletionContext,
            RoadmapArtifactPaths.ActiveEpic,
        ]);

        TransitionJournalRecord updateStarted = records.Single(record =>
            record.Prompt == "UpdateRoadmapCompletionContext" &&
            record.Event == "TransitionStarted");
        Assert.Contains(updateStarted.InputArtifactHashes.Keys, path => path.StartsWith(RoadmapArtifactPaths.EvaluationEvidenceDirectory, StringComparison.Ordinal));

        TransitionJournalRecord routing = records.Single(record => record.Prompt == "CompletionCertificationRouting");
        Assert.NotNull(routing.InputSnapshot);
        Assert.Contains(routing.InputArtifactHashes.Keys, path => path.StartsWith(RoadmapArtifactPaths.EvaluationEvidenceDirectory, StringComparison.Ordinal));
    }

    private static async Task<CompletionRunResult> RunCompletionAsync(string recommendation)
    {
        using var repo = SeedRepo();
        var runtime = new ScriptedAgentRuntime(BuildTurns(recommendation).ToArray());

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string stateMarkdown = repo.Read(RoadmapArtifactPaths.State);
        Assert.True(state.TransitionIntent.EvidencePaths.Count == 1, stateMarkdown);
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        string evaluationEvidence = repo.Read(evidencePath);
        IReadOnlyList<ArtifactLifecycleEntry> lifecycle = await new ArtifactLifecycleStore(repo.Artifacts).LoadAsync();
        ArtifactLifecycleState activeEpicLifecycle = lifecycle.Single(entry => entry.Path == RoadmapArtifactPaths.ActiveEpic).State;
        string transitionJournal = repo.Read(RoadmapArtifactPaths.TransitionJournal);

        return new CompletionRunResult(
            outcome,
            state,
            stateMarkdown,
            repo.Read(RoadmapArtifactPaths.RoadmapCompletionContext),
            evaluationEvidence,
            activeEpicLifecycle,
            runtime.OneShotCalls,
            transitionJournal);
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap");
        return repo;
    }

    private static IEnumerable<AgentTurnResult> BuildTurns(string recommendation)
    {
        yield return ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic"));
        yield return ScriptedAgentRuntime.Completed(NewEpicSelection());
        yield return ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic"));
        yield return ScriptedAgentRuntime.Completed(ActiveEpic());
        yield return ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic"));
        yield return ScriptedAgentRuntime.Completed(MilestoneBundle());
        yield return ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift"));
        yield return ScriptedAgentRuntime.Completed(CompletionEvaluation(recommendation));

        if (recommendation is "Close Epic" or "Close With Follow-Up")
        {
            yield return ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("UpdateRoadmapCompletionContext"));
            yield return ScriptedAgentRuntime.Completed("# Updated Roadmap Completion Context");
        }
    }

    private static TransitionJournalRecord[] JournalRecords(string journal) =>
        journal
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .ToArray();

    private static void AssertPromptSnapshot(
        IReadOnlyList<TransitionJournalRecord> records,
        string prompt,
        IReadOnlyList<string> expectedInputPaths)
    {
        TransitionJournalRecord started = records.Single(record => record.Prompt == prompt && record.Event == "TransitionStarted");
        TransitionJournalRecord completed = records.Single(record =>
            record.Prompt == prompt &&
            (record.Event == "TransitionCompleted" || record.Event == "PromptCompleted"));

        Assert.NotNull(started.InputSnapshot);
        Assert.NotNull(completed.InputSnapshot);
        Assert.Equal(started.InputSnapshot.SnapshotHash, completed.InputSnapshot.SnapshotHash);
        Assert.Equal(started.InputArtifactHashes, completed.InputArtifactHashes);
        foreach (string path in expectedInputPaths)
        {
            Assert.Contains(path, started.InputArtifactHashes.Keys);
        }
    }

    private static string NewEpicSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select New Intermediary Epic |
        | Recommended Initiative | Build routing test epic |
        | Initiative Type | New Intermediary Epic |
        | Confidence | High |
        | Primary Reason | Exercise completion routing. |
        """;

    private static string ActiveEpic() => RoadmapSamples.ValidEpic("Routing Test Epic", "EPIC-ROUTING");

    private static string MilestoneBundle() => """
        # FILE: .agents/specs/routing-test.md
        # Routing Test Milestone

        | Field | Value |
        |---|---|
        | Epic Path | .agents/epic.md |

        ## Acceptance Criteria

        - [ ] Completion routing is explicit.
        """;

    private static string CompletionEvaluation(string recommendation) => $$"""
        # Epic Completion Evaluation

        ## Evaluation Summary

        | Field | Value |
        |---|---|
        | Overall Completion Status | Functionally Complete |
        | Overall Drift Classification | None |
        | Closure Recommendation | {{recommendation}} |
        """;

    private sealed record CompletionRunResult(
        RoadmapOutcome Outcome,
        RoadmapStateDocument State,
        string StateMarkdown,
        string CompletionContext,
        string EvaluationEvidence,
        ArtifactLifecycleState ActiveEpicLifecycle,
        int AgentCalls,
        string TransitionJournal);
}
