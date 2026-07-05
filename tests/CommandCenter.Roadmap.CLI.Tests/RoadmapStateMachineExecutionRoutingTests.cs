using System.Text.Json;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class RoadmapStateMachineExecutionRoutingTests
{
    [Fact]
    public async Task Completion_claim_routes_to_certification_with_execution_evidence()
    {
        ExecutionRouteRun result = await RunExecutionRouteAsync(
            RoadmapExecutionTransportResult.Completed(ExecutionDisposition("Epic Complete", "EvaluateEpicCompletionAndDrift")),
            includeCompletionEvaluation: true);

        Assert.Equal(RoadmapOutcome.Paused, result.Outcome);
        Assert.Equal(RoadmapState.ExecutionLoop, result.State.CurrentState);
        Assert.Equal("Continue Epic", result.State.LastTransition.Decision);
        Assert.Single(result.EvaluationEvidencePaths);
        Assert.Single(result.ExecutionEvidencePaths);
        Assert.Equal(8, result.AgentCalls);
        Assert.Contains("Execution Evidence", result.EvaluationPromptContext, StringComparison.Ordinal);
        Assert.Contains(result.ExecutionEvidencePaths.Single(), result.EvaluationPromptContext, StringComparison.Ordinal);

        TransitionJournalRecord evaluation = result.Journal.Single(record =>
            record.Prompt == "EvaluateEpicCompletionAndDrift" &&
            record.Event == "TransitionStarted");
        Assert.Contains(result.ExecutionEvidencePaths.Single(), evaluation.InputArtifactHashes.Keys);
    }

    [Fact]
    public async Task Continue_required_preserves_execution_loop_and_bypasses_certification()
    {
        ExecutionRouteRun result = await RunExecutionRouteAsync(
            RoadmapExecutionTransportResult.Completed(ExecutionDisposition("Continue Required", "ContinueExecution")),
            includeCompletionEvaluation: false);

        Assert.Equal(RoadmapOutcome.Paused, result.Outcome);
        Assert.Equal(RoadmapState.ExecutionLoop, result.State.CurrentState);
        Assert.Equal(TransitionStatus.Paused, result.State.LastTransition.Status);
        Assert.Equal("Continue Required", result.State.LastTransition.Decision);
        Assert.Equal("ContinueExecution", result.State.TransitionIntent.Intent);
        Assert.Equal(RoadmapState.ExecutionLoop, result.State.TransitionIntent.DispatchState);
        Assert.Single(result.ExecutionEvidencePaths);
        Assert.Empty(result.EvaluationEvidencePaths);
        Assert.Equal(6, result.AgentCalls);
        Assert.Equal(ArtifactLifecycleState.Executing, result.ActiveEpicLifecycle);
    }

    [Fact]
    public async Task Execution_blocked_routes_to_execution_blocked_and_bypasses_certification()
    {
        ExecutionRouteRun result = await RunExecutionRouteAsync(
            RoadmapExecutionTransportResult.Completed(ExecutionDisposition("Execution Blocked", "ResolveExecutionBlocker")),
            includeCompletionEvaluation: false);

        Assert.Equal(RoadmapOutcome.Paused, result.Outcome);
        Assert.Equal(RoadmapState.ExecutionBlocked, result.State.CurrentState);
        Assert.Equal(TransitionStatus.Paused, result.State.LastTransition.Status);
        Assert.Equal("Execution Blocked", result.State.LastTransition.Decision);
        Assert.Equal("ResolveExecutionBlocker", result.State.TransitionIntent.Intent);
        Assert.Contains("Execution evidence for Execution Blocked.", Assert.Single(result.State.Blockers).Blocker, StringComparison.Ordinal);
        Assert.Single(result.ExecutionEvidencePaths);
        Assert.Empty(result.EvaluationEvidencePaths);
        Assert.Equal(6, result.AgentCalls);
        Assert.Equal(ArtifactLifecycleState.Executing, result.ActiveEpicLifecycle);
    }

    [Fact]
    public async Task Malformed_completed_output_blocks_safely_and_bypasses_certification()
    {
        ExecutionRouteRun result = await RunExecutionRouteAsync(
            RoadmapExecutionTransportResult.Completed("# Execution Report\n\nNo disposition."),
            includeCompletionEvaluation: false);

        Assert.Equal(RoadmapOutcome.Paused, result.Outcome);
        Assert.Equal(RoadmapState.EvidenceBlocked, result.State.CurrentState);
        Assert.Equal(TransitionStatus.Paused, result.State.LastTransition.Status);
        Assert.Equal("Malformed Execution Output", result.State.LastTransition.Decision);
        Assert.Equal("ResolveMalformedExecutionOutput", result.State.TransitionIntent.Intent);
        Assert.Single(result.ExecutionEvidencePaths);
        Assert.Empty(result.EvaluationEvidencePaths);
        Assert.Equal(6, result.AgentCalls);
        Assert.Equal(ArtifactLifecycleState.Executing, result.ActiveEpicLifecycle);
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
            RoadmapExecutionTransportResult.Completed(ExecutionDisposition(status, nextStep)),
            includeCompletionEvaluation: false);

        Assert.Equal(RoadmapOutcome.Paused, result.Outcome);
        Assert.Equal(RoadmapState.EvidenceBlocked, result.State.CurrentState);
        Assert.Equal(TransitionStatus.Paused, result.State.LastTransition.Status);
        Assert.Equal("Malformed Execution Output", result.State.LastTransition.Decision);
        Assert.Equal("ResolveMalformedExecutionOutput", result.State.TransitionIntent.Intent);
        Assert.Single(result.ExecutionEvidencePaths);
        Assert.Empty(result.EvaluationEvidencePaths);
        Assert.Equal(6, result.AgentCalls);
        Assert.Equal(ArtifactLifecycleState.Executing, result.ActiveEpicLifecycle);
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
            RoadmapExecutionTransportResult.Failed("Failed", "transport died"),
            includeCompletionEvaluation: false);

        Assert.Equal(RoadmapOutcome.Failed, result.Outcome);
        Assert.Equal(RoadmapState.Failed, result.State.CurrentState);
        Assert.Equal(TransitionStatus.Failed, result.State.LastTransition.Status);
        Assert.Equal("Runtime Failure", result.State.LastTransition.Decision);
        Assert.Equal("RepairExecutionRuntimeFailure", result.State.TransitionIntent.Intent);
        Assert.Single(result.ExecutionEvidencePaths);
        Assert.Empty(result.EvaluationEvidencePaths);
        Assert.Equal(6, result.AgentCalls);
        Assert.Contains("transport died", result.ExecutionEvidence, StringComparison.Ordinal);
    }

    private static async Task<ExecutionRouteRun> RunExecutionRouteAsync(
        RoadmapExecutionTransportResult bridgeResult,
        bool includeCompletionEvaluation)
    {
        using var repo = SeedRepo();
        var runtime = new ScriptedAgentRuntime(BuildTurns(includeCompletionEvaluation).ToArray());
        RoadmapOutcome outcome = await StateMachineFactory.Create(
            repo,
            runtime,
            new StaticExecutionBridge(bridgeResult)).RunAsync(CancellationToken.None);

        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        IReadOnlyList<string> executionEvidencePaths = await repo.Artifacts.ListAsync(RoadmapArtifactPaths.ExecutionEvidenceDirectory, "*.md");
        IReadOnlyList<string> evaluationEvidencePaths = await repo.Artifacts.ListAsync(RoadmapArtifactPaths.EvaluationEvidenceDirectory, "*.md");
        IReadOnlyList<ArtifactLifecycleEntry> lifecycle = await new ArtifactLifecycleStore(repo.Artifacts).LoadAsync();
        ArtifactLifecycleState activeEpicLifecycle = lifecycle.Single(entry => entry.Path == RoadmapArtifactPaths.ActiveEpic).State;
        string executionEvidence = executionEvidencePaths.Count == 0 ? string.Empty : repo.Read(executionEvidencePaths.Single());
        string evaluationPromptContext = runtime.Prompts.FirstOrDefault(prompt => prompt.Contains("## Execution Evidence", StringComparison.Ordinal)) ?? string.Empty;
        TransitionJournalRecord[] journal = File.Exists(Path.Combine(repo.Root, RoadmapArtifactPaths.TransitionJournal.Replace('/', Path.DirectorySeparatorChar)))
            ? repo.Read(RoadmapArtifactPaths.TransitionJournal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => JsonSerializer.Deserialize<TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
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
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap");
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

    private sealed class StaticExecutionBridge(RoadmapExecutionTransportResult result) : IRoadmapExecutionBridge
    {
        public Task<RoadmapExecutionTransportResult> RunAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed record ExecutionRouteRun(
        RoadmapOutcome Outcome,
        RoadmapStateDocument State,
        IReadOnlyList<string> ExecutionEvidencePaths,
        IReadOnlyList<string> EvaluationEvidencePaths,
        string ExecutionEvidence,
        ArtifactLifecycleState ActiveEpicLifecycle,
        int AgentCalls,
        string EvaluationPromptContext,
        IReadOnlyList<TransitionJournalRecord> Journal);
}
