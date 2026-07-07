using System.Text.Json;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Roadmap.Cli;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapStateMachineExecutionRoutingTests
{
    [Fact]
    public async Task Completion_claim_routes_to_certification_with_execution_evidence()
    {
        ExecutionRouteRun result = await RunExecutionRouteAsync(
            Cli.RoadmapExecutionTransportResult.Completed(ExecutionDisposition("Epic Complete", "EvaluateEpicCompletionAndDrift")),
            includeCompletionEvaluation: true);

        Assert.Equal(Cli.RoadmapOutcome.Paused, result.Outcome);
        Assert.Equal(Cli.RoadmapState.ExecutionLoop, result.State.CurrentState);
        Assert.Equal("Continue Epic", result.State.LastTransition.Decision);
        Assert.Single(result.EvaluationEvidencePaths);
        Assert.Single(result.ExecutionEvidencePaths);
        Assert.Equal(8, result.AgentCalls);
        Assert.Contains("Execution Evidence", result.EvaluationPromptContext, StringComparison.Ordinal);
        Assert.Contains(result.ExecutionEvidencePaths.Single(), result.EvaluationPromptContext, StringComparison.Ordinal);

        Cli.TransitionJournalRecord evaluation = result.Journal.Single(record =>
            record.Prompt == "EvaluateEpicCompletionAndDrift" &&
            record.Event == "TransitionStarted");
        Assert.Contains(result.ExecutionEvidencePaths.Single(), evaluation.InputArtifactHashes.Keys);
    }

    [Fact]
    public async Task Continue_required_preserves_execution_loop_and_bypasses_certification()
    {
        ExecutionRouteRun result = await RunExecutionRouteAsync(
            Cli.RoadmapExecutionTransportResult.Completed(ExecutionDisposition("Continue Required", "ContinueExecution")),
            includeCompletionEvaluation: false);

        Assert.Equal(Cli.RoadmapOutcome.Paused, result.Outcome);
        Assert.Equal(Cli.RoadmapState.ExecutionLoop, result.State.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, result.State.LastTransition.Status);
        Assert.Equal("Continue Required", result.State.LastTransition.Decision);
        Assert.Equal("ContinueExecution", result.State.TransitionIntent.Intent);
        Assert.Equal(Cli.RoadmapState.ExecutionLoop, result.State.TransitionIntent.DispatchState);
        Assert.Single(result.ExecutionEvidencePaths);
        Assert.Empty(result.EvaluationEvidencePaths);
        Assert.Equal(6, result.AgentCalls);
        Assert.Equal(Cli.ArtifactLifecycleState.Executing, result.ActiveEpicLifecycle);
    }

    [Fact]
    public async Task Execution_blocked_routes_to_execution_blocked_and_bypasses_certification()
    {
        ExecutionRouteRun result = await RunExecutionRouteAsync(
            Cli.RoadmapExecutionTransportResult.Completed(ExecutionDisposition("Execution Blocked", "ResolveExecutionBlocker")),
            includeCompletionEvaluation: false);

        Assert.Equal(Cli.RoadmapOutcome.Paused, result.Outcome);
        Assert.Equal(Cli.RoadmapState.ExecutionBlocked, result.State.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, result.State.LastTransition.Status);
        Assert.Equal("Execution Blocked", result.State.LastTransition.Decision);
        Assert.Equal("ResolveExecutionBlocker", result.State.TransitionIntent.Intent);
        Assert.Contains("Execution evidence for Execution Blocked.", Assert.Single(result.State.Blockers).Blocker, StringComparison.Ordinal);
        Assert.Single(result.ExecutionEvidencePaths);
        Assert.Empty(result.EvaluationEvidencePaths);
        Assert.Equal(6, result.AgentCalls);
        Assert.Equal(Cli.ArtifactLifecycleState.Executing, result.ActiveEpicLifecycle);
    }

    [Fact]
    public async Task Malformed_completed_output_blocks_safely_and_bypasses_certification()
    {
        ExecutionRouteRun result = await RunExecutionRouteAsync(
            Cli.RoadmapExecutionTransportResult.Completed("# Execution Report\n\nNo disposition."),
            includeCompletionEvaluation: false);

        Assert.Equal(Cli.RoadmapOutcome.Paused, result.Outcome);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, result.State.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, result.State.LastTransition.Status);
        Assert.Equal("Malformed Execution Output", result.State.LastTransition.Decision);
        Assert.Equal("ResolveMalformedExecutionOutput", result.State.TransitionIntent.Intent);
        Assert.Single(result.ExecutionEvidencePaths);
        Assert.Empty(result.EvaluationEvidencePaths);
        Assert.Equal(6, result.AgentCalls);
        Assert.Equal(Cli.ArtifactLifecycleState.Executing, result.ActiveEpicLifecycle);
        Assert.Contains("Malformed Execution Output", result.ExecutionEvidence, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Epic Complete", "ContinueExecution")]
    [InlineData("Continue Required", "EvaluateEpicCompletionAndDrift")]
    [InlineData("Execution Blocked", "ContinueExecution")]
    public async Task Contradictory_execution_protocol_blocks_safely_and_bypasses_certification(
        string status,
        string nextStep)
    {
        ExecutionRouteRun result = await RunExecutionRouteAsync(
            Cli.RoadmapExecutionTransportResult.Completed(ExecutionDisposition(status, nextStep)),
            includeCompletionEvaluation: false);

        Assert.Equal(Cli.RoadmapOutcome.Paused, result.Outcome);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, result.State.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, result.State.LastTransition.Status);
        Assert.Equal("Malformed Execution Output", result.State.LastTransition.Decision);
        Assert.Equal("ResolveMalformedExecutionOutput", result.State.TransitionIntent.Intent);
        Assert.Single(result.ExecutionEvidencePaths);
        Assert.Empty(result.EvaluationEvidencePaths);
        Assert.Equal(6, result.AgentCalls);
        Assert.Equal(Cli.ArtifactLifecycleState.Executing, result.ActiveEpicLifecycle);
        Assert.Contains("## Execution Disposition", result.ExecutionEvidence, StringComparison.Ordinal);
        Assert.Contains($"| Status | {status} |", result.ExecutionEvidence, StringComparison.Ordinal);
        Assert.Contains($"| Next Step | {nextStep} |", result.ExecutionEvidence, StringComparison.Ordinal);
        Assert.Contains("## Execution Protocol Validation", result.ExecutionEvidence, StringComparison.Ordinal);
        Assert.Contains("| Result | Invalid |", result.ExecutionEvidence, StringComparison.Ordinal);
        Assert.Contains("Protocol Violation Reason", result.ExecutionEvidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runtime_failure_remains_infrastructure_failure_and_bypasses_certification()
    {
        ExecutionRouteRun result = await RunExecutionRouteAsync(
            Cli.RoadmapExecutionTransportResult.Failed("Failed", "transport died"),
            includeCompletionEvaluation: false);

        Assert.Equal(Cli.RoadmapOutcome.Failed, result.Outcome);
        Assert.Equal(Cli.RoadmapState.Failed, result.State.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Failed, result.State.LastTransition.Status);
        Assert.Equal("Runtime Failure", result.State.LastTransition.Decision);
        Assert.Equal("RepairExecutionRuntimeFailure", result.State.TransitionIntent.Intent);
        Assert.Single(result.ExecutionEvidencePaths);
        Assert.Empty(result.EvaluationEvidencePaths);
        Assert.Equal(6, result.AgentCalls);
        Assert.Contains("transport died", result.ExecutionEvidence, StringComparison.Ordinal);
    }

    private static async Task<ExecutionRouteRun> RunExecutionRouteAsync(
        Cli.RoadmapExecutionTransportResult bridgeResult,
        bool includeCompletionEvaluation)
    {
        using var repo = SeedRepo();
        var runtime = new ScriptedAgentRuntime(BuildTurns(includeCompletionEvaluation).ToArray());
        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(
            repo,
            runtime,
            new StaticExecutionBridge(bridgeResult)).RunAsync(CancellationToken.None);

        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        IReadOnlyList<string> executionEvidencePaths = await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.ExecutionEvidenceDirectory, "*.md");
        IReadOnlyList<string> evaluationEvidencePaths = await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.EvaluationEvidenceDirectory, "*.md");
        IReadOnlyList<Cli.ArtifactLifecycleEntry> lifecycle = await new Cli.ArtifactLifecycleStore(repo.Artifacts).LoadAsync();
        Cli.ArtifactLifecycleState activeEpicLifecycle = lifecycle.Single(entry => entry.Path == Cli.RoadmapArtifactPaths.ActiveEpic).State;
        string executionEvidence = executionEvidencePaths.Count == 0 ? string.Empty : repo.Read(executionEvidencePaths.Single());
        string evaluationPromptContext = runtime.Prompts.FirstOrDefault(prompt => prompt.Contains("## Execution Evidence", StringComparison.Ordinal)) ?? string.Empty;
        Cli.TransitionJournalRecord[] journal = File.Exists(Path.Combine(repo.Root, Cli.RoadmapArtifactPaths.TransitionJournal.Replace('/', Path.DirectorySeparatorChar)))
            ? repo.Read(Cli.RoadmapArtifactPaths.TransitionJournal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => JsonSerializer.Deserialize<Cli.TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
                .ToArray()
            : [];

        return new ExecutionRouteRun(
            outcome,
            state,
            executionEvidencePaths,
            evaluationEvidencePaths,
            executionEvidence,
            activeEpicLifecycle,
            runtime.OneShotCalls,
            evaluationPromptContext,
            journal);
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        return repo;
    }

    private static IEnumerable<AgentTurnResult> BuildTurns(bool includeCompletionEvaluation)
    {
        yield return ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic"));
        yield return ScriptedAgentRuntime.Completed(NewEpicSelection());
        yield return ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic"));
        yield return ScriptedAgentRuntime.Completed(RoadmapSamples.ValidEpic("Execution Routing Epic", "EPIC-EXECUTION-ROUTING"));
        yield return ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic"));
        yield return ScriptedAgentRuntime.Completed(MilestoneBundle());

        if (includeCompletionEvaluation)
        {
            yield return ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift"));
            yield return ScriptedAgentRuntime.Completed(CompletionEvaluation("Continue Epic"));
        }
    }

    private static string ExecutionDisposition(string status, string nextStep) => $$"""
        # Execution Report

        ## Execution Disposition

        | Field | Value |
        |---|---|
        | Status | {{status}} |
        | Confidence | High |
        | Evidence Summary | Execution evidence for {{status}}. |
        | Next Step | {{nextStep}} |
        """;

    private static string NewEpicSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select New Intermediary Epic |
        | Recommended Initiative | Build execution routing test epic |
        | Initiative Type | New Intermediary Epic |
        | Confidence | High |
        | Primary Reason | Exercise execution routing. |
        """;

    private static string MilestoneBundle() => """
        # FILE: .agents/specs/execution-routing-test.md
        # Execution Routing Test Milestone

        | Field | Value |
        |---|---|
        | Epic Path | .agents/epic.md |

        ## Acceptance Criteria

        - [ ] Execution routing is explicit.
        """;

    private static string CompletionEvaluation(string recommendation) => $$"""
        # Epic Completion Evaluation

        ## Evaluation Summary

        | Field | Value |
        |---|---|
        | Overall Completion Status | Partially Complete |
        | Overall Drift Classification | None |
        | Closure Recommendation | {{recommendation}} |
        """;

    private sealed class StaticExecutionBridge(Cli.RoadmapExecutionTransportResult result) : Cli.IRoadmapExecutionBridge
    {
        public Task<Cli.RoadmapExecutionTransportResult> RunAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed record ExecutionRouteRun(
        Cli.RoadmapOutcome Outcome,
        Cli.RoadmapStateDocument State,
        IReadOnlyList<string> ExecutionEvidencePaths,
        IReadOnlyList<string> EvaluationEvidencePaths,
        string ExecutionEvidence,
        Cli.ArtifactLifecycleState ActiveEpicLifecycle,
        int AgentCalls,
        string EvaluationPromptContext,
        IReadOnlyList<Cli.TransitionJournalRecord> Journal);
}
