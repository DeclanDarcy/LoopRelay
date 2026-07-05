using System.Text.Json;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class RoadmapFailurePersistenceTests
{
    [Theory]
    [InlineData("CreateRoadmapCompletionContext", "CoreReady", "RoadmapCompletionContextReady", RoadmapArtifactPaths.RoadmapCompletionContext)]
    [InlineData("SelectNextEpic", "RoadmapCompletionContextReady", "SelectNextStrategicInitiative", RoadmapArtifactPaths.Selection)]
    [InlineData("EpicPreparationAudit", "ExistingEpicSelected", "EpicPreparationAudit", RoadmapArtifactPaths.AuditEvidenceDirectory)]
    [InlineData("CreateNewEpic", "NewEpicProposed", "ActiveEpicReady", RoadmapArtifactPaths.ActiveEpic)]
    [InlineData("RealignEpic", "RealignEpic", "ActiveEpicReady", RoadmapArtifactPaths.ActiveEpic)]
    [InlineData("ReimagineEpic", "ReimagineEpic", "ActiveEpicReady", RoadmapArtifactPaths.ActiveEpic)]
    [InlineData("SplitEpic", "SplitEpicProposed", "SplitChildSelection", RoadmapArtifactPaths.SplitFamiliesDirectory)]
    [InlineData("GenerateMilestoneDeepDivesForEpic", "ActiveEpicReady", "MilestoneSpecsReady", RoadmapArtifactPaths.SpecsDirectory)]
    [InlineData("EvaluateEpicCompletionAndDrift", "EpicCompletionDetected", "CompletionEvaluationAndContextUpdate", RoadmapArtifactPaths.EvaluationEvidenceDirectory)]
    [InlineData("UpdateRoadmapCompletionContext", "CompletionEvaluationAndContextUpdate", "SelectNextStrategicInitiative", RoadmapArtifactPaths.RoadmapCompletionContext)]
    public async Task Prompt_transition_failures_are_owned_by_the_transition_layer(
        string prompt,
        string expectedFrom,
        string expectedTo,
        string expectedOutput)
    {
        RoadmapState expectedFromState = Enum.Parse<RoadmapState>(expectedFrom);
        RoadmapState expectedToState = Enum.Parse<RoadmapState>(expectedTo);
        using var repo = SeedRepo(includeCompletionContext: prompt != "CreateRoadmapCompletionContext");
        var runtime = new ScriptedAgentRuntime(BuildPromptFailureTurns(prompt).ToArray());

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(TransitionStatus.Failed, state.LastTransition.Status);
        Assert.Equal(expectedFromState, state.LastTransition.From);
        Assert.Equal(expectedToState, state.LastTransition.To);
        Assert.Equal(prompt, state.LastTransition.Prompt);
        Assert.Equal(RoadmapArtifactPaths.ProjectionPaths[prompt], state.LastTransition.Projection);
        Assert.Equal(expectedOutput, state.LastTransition.Output);
        Assert.Equal("ResolveTransitionFailure", state.TransitionIntent.Intent);
        Assert.Equal(RoadmapState.EvidenceBlocked, state.TransitionIntent.DispatchState);
        Assert.Equal([expectedOutput], state.TransitionIntent.EvidencePaths);

        string stateMarkdown = repo.Read(RoadmapArtifactPaths.State);
        Assert.DoesNotContain("RoadmapStateMachine", stateMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveBlocker", stateMarkdown, StringComparison.Ordinal);
        Assert.Empty(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "roadmap-transition-blocked-*.md"));

        TransitionJournalRecord failed = ReadJournal(repo).Single(record =>
            record.Event == "TransitionFailed" &&
            record.Prompt == prompt);
        Assert.Equal(state.LastTransition.From, failed.PreviousState);
        Assert.Equal(state.LastTransition.To, failed.AttemptedState);
        Assert.Equal(state.LastTransition.Prompt, failed.Prompt);
        Assert.Equal(state.LastTransition.Projection, failed.Projection);
        Assert.Equal([expectedOutput], failed.OutputPaths);
        Assert.Contains($"{prompt} failed", failed.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Artifact_promotion_failure_preserves_transition_context_without_generic_overwrite()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        string blocked = CreateBlocked();
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(NewEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic")),
            ScriptedAgentRuntime.Completed(blocked));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal(RoadmapState.NewEpicProposed, state.LastTransition.From);
        Assert.Equal(RoadmapState.ActiveEpicReady, state.LastTransition.To);
        Assert.Equal("CreateNewEpic", state.LastTransition.Prompt);
        Assert.Equal(RoadmapArtifactPaths.ProjectionPaths["CreateNewEpic"], state.LastTransition.Projection);
        Assert.Equal(evidencePath, state.LastTransition.Output);
        Assert.Equal("ResolveArtifactPromotionBlocker", state.TransitionIntent.Intent);
        Assert.Equal(blocked, repo.Read(evidencePath));
        Assert.Empty(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "roadmap-transition-blocked-*.md"));

        TransitionJournalRecord promotionBlocked = ReadJournal(repo).Single(record => record.Event == "ArtifactPromotionBlocked");
        Assert.Equal(state.LastTransition.From, promotionBlocked.PreviousState);
        Assert.Equal(state.LastTransition.To, promotionBlocked.AttemptedState);
        Assert.Equal(state.LastTransition.Prompt, promotionBlocked.Prompt);
        Assert.Equal(state.LastTransition.Projection, promotionBlocked.Projection);
        Assert.Equal([evidencePath], promotionBlocked.OutputPaths);
    }

    [Fact]
    public async Task Preflight_failure_uses_generic_safety_net()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap");

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.PreflightBlocked, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        AssertGenericBlockedState(repo, state, "Preflight");
    }

    [Fact]
    public async Task Unexpected_runtime_exception_uses_generic_safety_net()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        var runtime = new ThrowingAgentRuntime(new InvalidOperationException("agent transport unavailable"));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        AssertGenericBlockedState(repo, state, "RoadmapStateMachine");
        Assert.Contains("agent transport unavailable", Assert.Single(state.Blockers).Blocker, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execution_bridge_failure_is_persisted_as_runtime_failure()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(NewEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic")),
            ScriptedAgentRuntime.Completed(RoadmapSamples.ValidEpic("Execution Bridge Epic", "EPIC-BRIDGE")),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic")),
            ScriptedAgentRuntime.Completed(MilestoneBundle()));

        RoadmapOutcome outcome = await StateMachineFactory.Create(
            repo,
            runtime,
            new FailingExecutionBridge("execution bridge unavailable")).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.Failed, state.CurrentState);
        Assert.Equal(TransitionStatus.Failed, state.LastTransition.Status);
        Assert.Equal(RoadmapState.ExecutionLoop, state.LastTransition.From);
        Assert.Equal(RoadmapState.Failed, state.LastTransition.To);
        Assert.Equal("ExecutionLoop", state.LastTransition.Prompt);
        Assert.Equal("None", state.LastTransition.Projection);
        Assert.Equal("Runtime Failure", state.LastTransition.Decision);
        Assert.StartsWith(RoadmapArtifactPaths.ExecutionEvidenceDirectory, state.LastTransition.Output, StringComparison.Ordinal);
        Assert.Equal("RepairExecutionRuntimeFailure", state.TransitionIntent.Intent);
        Assert.Equal(RoadmapState.Failed, state.TransitionIntent.DispatchState);
        Assert.Equal([state.LastTransition.Output], state.TransitionIntent.EvidencePaths);
        Assert.Contains("execution bridge unavailable", Assert.Single(state.Blockers).Blocker, StringComparison.Ordinal);
        Assert.Contains("Runtime Failure", repo.Read(state.LastTransition.Output), StringComparison.Ordinal);
        Assert.Empty(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "roadmap-transition-blocked-*.md"));
    }

    private static TempRepo SeedRepo(bool includeCompletionContext)
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap");
        if (includeCompletionContext)
        {
            repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
        }

        return repo;
    }

    private static IEnumerable<AgentTurnResult> BuildPromptFailureTurns(string prompt)
    {
        AgentTurnResult Failure() => ScriptedAgentRuntime.Failed($"{prompt} failed");

        return prompt switch
        {
            "CreateRoadmapCompletionContext" =>
            [
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateRoadmapCompletionContext")),
                Failure(),
            ],
            "SelectNextEpic" =>
            [
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
                Failure(),
            ],
            "EpicPreparationAudit" =>
            [
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
                ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
                Failure(),
            ],
            "CreateNewEpic" =>
            [
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
                ScriptedAgentRuntime.Completed(NewEpicSelection()),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic")),
                Failure(),
            ],
            "RealignEpic" =>
            [
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
                ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
                ScriptedAgentRuntime.Completed(AuditDisposition("Realign")),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("RealignEpic")),
                Failure(),
            ],
            "ReimagineEpic" =>
            [
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
                ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
                ScriptedAgentRuntime.Completed(AuditDisposition("Reimagine")),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("ReimagineEpic")),
                Failure(),
            ],
            "SplitEpic" =>
            [
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
                ScriptedAgentRuntime.Completed(SplitSelection()),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SplitEpic")),
                Failure(),
            ],
            "GenerateMilestoneDeepDivesForEpic" =>
            [
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
                ScriptedAgentRuntime.Completed(NewEpicSelection()),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic")),
                ScriptedAgentRuntime.Completed(RoadmapSamples.ValidEpic("Milestone Failure Epic", "EPIC-MILESTONE")),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic")),
                Failure(),
            ],
            "EvaluateEpicCompletionAndDrift" =>
            [
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
                ScriptedAgentRuntime.Completed(NewEpicSelection()),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic")),
                ScriptedAgentRuntime.Completed(RoadmapSamples.ValidEpic("Evaluation Failure Epic", "EPIC-EVALUATION")),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic")),
                ScriptedAgentRuntime.Completed(MilestoneBundle()),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift")),
                Failure(),
            ],
            "UpdateRoadmapCompletionContext" =>
            [
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
                ScriptedAgentRuntime.Completed(NewEpicSelection()),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic")),
                ScriptedAgentRuntime.Completed(RoadmapSamples.ValidEpic("Completion Update Failure Epic", "EPIC-UPDATE")),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic")),
                ScriptedAgentRuntime.Completed(MilestoneBundle()),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift")),
                ScriptedAgentRuntime.Completed(CompletionEvaluation("Close Epic")),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("UpdateRoadmapCompletionContext")),
                Failure(),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(prompt), prompt, "No prompt failure scenario registered."),
        };
    }

    private static TransitionJournalRecord[] ReadJournal(TempRepo repo) =>
        repo.Read(RoadmapArtifactPaths.TransitionJournal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .ToArray();

    private static void AssertGenericBlockedState(TempRepo repo, RoadmapStateDocument state, string prompt)
    {
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(TransitionStatus.Failed, state.LastTransition.Status);
        Assert.Equal(RoadmapState.CoreReady, state.LastTransition.From);
        Assert.Equal(RoadmapState.EvidenceBlocked, state.LastTransition.To);
        Assert.Equal(prompt, state.LastTransition.Prompt);
        Assert.Equal("None", state.LastTransition.Projection);
        Assert.Equal("Blocked", state.LastTransition.Decision);
        Assert.StartsWith(RoadmapArtifactPaths.BlockerEvidenceDirectory, state.LastTransition.Output, StringComparison.Ordinal);
        Assert.Equal("ResolveBlocker", state.TransitionIntent.Intent);
        Assert.Equal([state.LastTransition.Output], state.TransitionIntent.EvidencePaths);
        Assert.Contains("# Roadmap Transition Blocked", repo.Read(state.LastTransition.Output), StringComparison.Ordinal);
    }

    private static string ExistingEpicSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select Existing Epic |
        | Recommended Initiative | Existing Epic |
        | Initiative Type | Existing Roadmap Epic |
        | Confidence | High |
        | Primary Reason | Existing epic needs audit. |

        ## If Existing Roadmap Epic Selected

        | Field | Value |
        |---|---|
        | Epic ID | EPIC-OLD |
        | Epic Name | Existing Epic |
        | Why This Epic Now | It is the next candidate. |
        | Dependencies Satisfied? | Yes |
        | Required Pre-Implementation Follow-Up | None |
        """;

    private static string NewEpicSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select New Intermediary Epic |
        | Recommended Initiative | Build failure persistence test epic |
        | Initiative Type | New Intermediary Epic |
        | Confidence | High |
        | Primary Reason | Exercise failure persistence ownership. |
        """;

    private static string SplitSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select Split Epic |
        | Recommended Initiative | Split failure persistence test epic |
        | Initiative Type | Split Epic |
        | Confidence | High |
        | Primary Reason | Exercise split prompt failure persistence. |
        """;

    private static string AuditDisposition(string disposition) => $$"""
        # Epic Preparation Audit

        ## Selected Epic

        | Field | Value |
        |---|---|
        | Epic ID | EPIC-OLD |
        | Epic Name | Existing Epic |
        | Claimed Strategic Purpose | Preserve failure persistence ownership |
        | Apparent Projection Link | Failure Persistence |

        ## Audit Disposition

        | Field | Value |
        |---|---|
        | Disposition | {{disposition}} |
        | Confidence | High |
        | Primary Reason | Audit supports {{disposition}}. |
        | Evidence Strength | Strong |
        | Recommended Next Step | {{disposition}} Epic |
        """;

    private static string CreateBlocked() => """
        # Create New Epic Blocked

        ## Reason

        Promotion should preserve this exact evidence.

        ## Required Next Step

        Resolve the authored blocker.
        """;

    private static string MilestoneBundle() => """
        # FILE: .agents/specs/failure-persistence-test.md
        # Failure Persistence Test Milestone

        | Field | Value |
        |---|---|
        | Epic Path | .agents/epic.md |

        ## Acceptance Criteria

        - [ ] Failure ownership remains explicit.
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

    private sealed class ThrowingAgentRuntime(Exception exception) : IAgentRuntime
    {
        public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken cancellationToken = default) =>
            throw exception;

        public Task<AgentTurnResult> RunOneShotAsync(
            AgentSessionSpec spec,
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default) =>
            throw exception;

        public ValueTask CloseSessionAsync(IAgentSession session) => ValueTask.CompletedTask;
    }

    private sealed class FailingExecutionBridge(string message) : IRoadmapExecutionBridge
    {
        public Task<RoadmapExecutionTransportResult> RunAsync(CancellationToken cancellationToken) =>
            Task.FromResult(RoadmapExecutionTransportResult.Failed("Failed", message));
    }
}
