using System.Collections;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Tests.Services.Execution;
using LoopRelay.Roadmap.Cli.Tests.Services.Projections;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.Services.State.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests.Services.State;

public sealed class RoadmapStateMachineResumeTests
{
    [Fact]
    public async Task Existing_blocked_state_is_loaded_before_startup_can_overwrite_it()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(
            RoadmapState.EvidenceBlocked,
            blockers: [new BlockerRow("Missing evidence", "Add the evidence")]));
        var runtime = new ScriptedAgentRuntime();

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        string state = repo.Read(RoadmapArtifactPaths.StateJson);
        Assert.Contains("EvidenceBlocked", state, StringComparison.Ordinal);
        Assert.Contains("Missing evidence", state, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Prompt\": \"Preflight\"", state, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_blocked_state_survives_project_context_preflight_failure()
    {
        using var repo = new TempRepo();
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(
            RoadmapState.EvidenceBlocked,
            blockers: [new BlockerRow("Missing evidence", "Add the evidence")]));
        var runtime = new ScriptedAgentRuntime();

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        string state = repo.Read(RoadmapArtifactPaths.StateJson);
        Assert.Contains("EvidenceBlocked", state, StringComparison.Ordinal);
        Assert.Contains("Missing evidence", state, StringComparison.Ordinal);
        Assert.DoesNotContain("Project Context source contract violation", state, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Prompt\": \"Preflight\"", state, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((int)RoadmapState.StrategicInvestigationRequired, (int)RoadmapOutcome.Paused)]
    [InlineData((int)RoadmapState.RoadmapRevisionRequired, (int)RoadmapOutcome.Paused)]
    [InlineData((int)RoadmapState.NoSuitableInitiative, (int)RoadmapOutcome.Paused)]
    [InlineData((int)RoadmapState.EvidenceGathering, (int)RoadmapOutcome.Paused)]
    [InlineData((int)RoadmapState.ExecutionBlocked, (int)RoadmapOutcome.Paused)]
    [InlineData((int)RoadmapState.Completed, (int)RoadmapOutcome.Completed)]
    [InlineData((int)RoadmapState.Failed, (int)RoadmapOutcome.Failed)]
    public async Task Report_only_state_skips_project_context_preflight_failure(
        int persistedStateValue,
        int expectedOutcomeValue)
    {
        RoadmapState persistedState = (RoadmapState)persistedStateValue;
        RoadmapOutcome expectedOutcome = (RoadmapOutcome)expectedOutcomeValue;
        using var repo = new TempRepo();
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(persistedState));
        var runtime = new ScriptedAgentRuntime();

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(expectedOutcome, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        string state = repo.Read(RoadmapArtifactPaths.StateJson);
        Assert.Contains(persistedState.ToString(), state, StringComparison.Ordinal);
        Assert.DoesNotContain("Project Context source contract violation", state, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Prompt\": \"Preflight\"", state, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fresh_repository_missing_project_context_does_not_write_preflight_blocker()
    {
        using var repo = new TempRepo();
        var runtime = new ScriptedAgentRuntime();

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.PreflightBlocked, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        RoadmapStateDocument? state = await new RoadmapStateStore(repo.Artifacts).LoadAsync();
        Assert.Null(state);
        Assert.Empty((IEnumerable)await repo.Artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "*.md"));
    }

    [Fact]
    public async Task Active_resume_project_context_failure_preserves_interrupted_context()
    {
        using var repo = new TempRepo();
        RoadmapTransitionIntent intent = new(
            "ResumeMilestoneGeneration",
            RoadmapState.ActiveEpicReady,
            [".agents/evidence/original.md"]);
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(
            RoadmapState.ActiveEpicReady,
            from: RoadmapState.CreateNewEpic,
            prompt: "CreateNewEpic",
            output: RoadmapArtifactPaths.ActiveEpic,
            blockers: [new BlockerRow("Original blocker", "Resolve original blocker")],
            intent: intent,
            nextTransitions: ["GenerateMilestoneDeepDives"]));
        string stateBefore = repo.Read(RoadmapArtifactPaths.StateJson);
        var runtime = new ScriptedAgentRuntime();

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.PreflightBlocked, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        RoadmapStateDocument? state = await new RoadmapStateStore(repo.Artifacts).LoadAsync();
        Assert.NotNull(state);
        Assert.Equal(RoadmapState.ActiveEpicReady, state.CurrentState);
        Assert.Equal(RoadmapState.CreateNewEpic, state.LastTransition.From);
        Assert.Equal(RoadmapState.ActiveEpicReady, state.LastTransition.To);
        Assert.Equal("CreateNewEpic", state.LastTransition.Prompt);
        Assert.Equal((string?)RoadmapArtifactPaths.ActiveEpic, state.LastTransition.Output);
        Assert.Equal("ResumeMilestoneGeneration", state.TransitionIntent.Intent);
        Assert.Equal(RoadmapState.ActiveEpicReady, state.TransitionIntent.DispatchState);
        Assert.Contains(".agents/evidence/original.md", state.TransitionIntent.EvidencePaths);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker == "Original blocker");
        Assert.DoesNotContain(state.Blockers, blocker => blocker.Blocker.Contains("Project Context source contract violation", StringComparison.Ordinal));
        Assert.Contains("GenerateMilestoneDeepDives", state.NextValidTransitions);
        Assert.DoesNotContain("Repair Project Context and rerun", state.NextValidTransitions);
        Assert.Equal(stateBefore, repo.Read(RoadmapArtifactPaths.StateJson));
        Assert.DoesNotContain("\"Prompt\": \"Preflight\"", repo.Read(RoadmapArtifactPaths.StateJson), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resume_planning_block_preserves_persisted_state()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(
            RoadmapState.ActiveEpicReady,
            from: RoadmapState.CreateNewEpic,
            prompt: "CreateNewEpic",
            output: RoadmapArtifactPaths.ActiveEpic,
            nextTransitions: ["GenerateMilestoneDeepDives"]));
        string stateBefore = repo.Read(RoadmapArtifactPaths.StateJson);
        var runtime = new ScriptedAgentRuntime();

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.ActiveEpicReady, state.CurrentState);
        Assert.Empty(state.Blockers);
        Assert.Equal(stateBefore, repo.Read(RoadmapArtifactPaths.StateJson));
        Assert.Empty((IEnumerable)await repo.Artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "*.md"));
    }

    [Fact]
    public async Task Existing_terminal_paused_state_is_not_restarted()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(RoadmapState.StrategicInvestigationRequired));
        var runtime = new ScriptedAgentRuntime();

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        Assert.Contains("StrategicInvestigationRequired", repo.Read(RoadmapArtifactPaths.StateJson), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_active_epic_resumes_without_rerunning_selection()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Ready);
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(
            RoadmapState.ActiveEpicReady,
            from: RoadmapState.CreateNewEpic,
            prompt: "CreateNewEpic",
            output: RoadmapArtifactPaths.ActiveEpic));
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic")),
            ScriptedAgentRuntime.Completed(MilestoneBundle()));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(2, runtime.OneShotCalls);
        Assert.All(runtime.Prompts, prompt => Assert.DoesNotContain("SelectNextEpic", prompt, StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(repo.Root, RoadmapArtifactPaths.Selection.Replace('/', Path.DirectorySeparatorChar))) == false);
        Assert.Contains("MilestoneSpecsReady", repo.Read(RoadmapArtifactPaths.StateJson), StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionLoop", repo.Read(RoadmapArtifactPaths.StateJson), StringComparison.Ordinal);
    }

    private static RoadmapStateDocument State(
        RoadmapState state,
        RoadmapState? from = null,
        RoadmapState? to = null,
        string prompt = "None",
        string output = "None",
        IReadOnlyList<BlockerRow>? blockers = null,
        RoadmapTransitionIntent? intent = null,
        IReadOnlyList<string>? nextTransitions = null) =>
        new(
            state,
            [],
            new RoadmapTransitionSummary(from ?? state, to ?? state, prompt, "None", output, "Completed", TransitionStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            blockers ?? [],
            "None",
            0,
            0,
            new ProjectionManifestCounts(0, 0, 0),
            intent ?? RoadmapTransitionIntent.Empty(state),
            nextTransitions ?? [],
            []);

    private static string MilestoneBundle() => """
        # FILE: .agents/specs/resume-test.md
        # Resume Test Milestone

        | Field | Value |
        |---|---|
        | Epic Path | .agents/epic.md |

        ## Acceptance Criteria

        - [ ] Resume without selection.
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
}
