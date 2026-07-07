using LoopRelay.Roadmap.Cli;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapStateMachineResumeTests
{
    [Fact]
    public async Task Existing_blocked_state_is_loaded_before_startup_can_overwrite_it()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(
            Cli.RoadmapState.EvidenceBlocked,
            blockers: [new Cli.BlockerRow("Missing evidence", "Add the evidence")]));
        var runtime = new ScriptedAgentRuntime();

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        string state = repo.Read(Cli.RoadmapArtifactPaths.State);
        Assert.Contains("EvidenceBlocked", state, StringComparison.Ordinal);
        Assert.Contains("Missing evidence", state, StringComparison.Ordinal);
        Assert.DoesNotContain("| Prompt | Preflight |", state, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_blocked_state_survives_project_context_preflight_failure()
    {
        using var repo = new TempRepo();
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(
            Cli.RoadmapState.EvidenceBlocked,
            blockers: [new Cli.BlockerRow("Missing evidence", "Add the evidence")]));
        var runtime = new ScriptedAgentRuntime();

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        string state = repo.Read(Cli.RoadmapArtifactPaths.State);
        Assert.Contains("EvidenceBlocked", state, StringComparison.Ordinal);
        Assert.Contains("Missing evidence", state, StringComparison.Ordinal);
        Assert.DoesNotContain("Project Context source contract violation", state, StringComparison.Ordinal);
        Assert.DoesNotContain("| Prompt | Preflight |", state, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((int)Cli.RoadmapState.StrategicInvestigationRequired, (int)Cli.RoadmapOutcome.Paused)]
    [InlineData((int)Cli.RoadmapState.RoadmapRevisionRequired, (int)Cli.RoadmapOutcome.Paused)]
    [InlineData((int)Cli.RoadmapState.NoSuitableInitiative, (int)Cli.RoadmapOutcome.Paused)]
    [InlineData((int)Cli.RoadmapState.EvidenceGathering, (int)Cli.RoadmapOutcome.Paused)]
    [InlineData((int)Cli.RoadmapState.ExecutionBlocked, (int)Cli.RoadmapOutcome.Paused)]
    [InlineData((int)Cli.RoadmapState.Completed, (int)Cli.RoadmapOutcome.Completed)]
    [InlineData((int)Cli.RoadmapState.Failed, (int)Cli.RoadmapOutcome.Failed)]
    public async Task Report_only_state_skips_project_context_preflight_failure(
        int persistedStateValue,
        int expectedOutcomeValue)
    {
        Cli.RoadmapState persistedState = (Cli.RoadmapState)persistedStateValue;
        Cli.RoadmapOutcome expectedOutcome = (Cli.RoadmapOutcome)expectedOutcomeValue;
        using var repo = new TempRepo();
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(persistedState));
        var runtime = new ScriptedAgentRuntime();

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(expectedOutcome, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        string state = repo.Read(Cli.RoadmapArtifactPaths.State);
        Assert.Contains(persistedState.ToString(), state, StringComparison.Ordinal);
        Assert.DoesNotContain("Project Context source contract violation", state, StringComparison.Ordinal);
        Assert.DoesNotContain("| Prompt | Preflight |", state, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fresh_repository_missing_project_context_does_not_write_preflight_blocker()
    {
        using var repo = new TempRepo();
        var runtime = new ScriptedAgentRuntime();

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.PreflightBlocked, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        Cli.RoadmapStateDocument? state = await new RoadmapStateStore(repo.Artifacts).LoadAsync();
        Assert.Null(state);
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "*.md"));
    }

    [Fact]
    public async Task Active_resume_project_context_failure_preserves_interrupted_context()
    {
        using var repo = new TempRepo();
        Cli.RoadmapTransitionIntent intent = new(
            "ResumeMilestoneGeneration",
            Cli.RoadmapState.ActiveEpicReady,
            [".agents/evidence/original.md"]);
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(
            Cli.RoadmapState.ActiveEpicReady,
            from: Cli.RoadmapState.CreateNewEpic,
            prompt: "CreateNewEpic",
            output: Cli.RoadmapArtifactPaths.ActiveEpic,
            blockers: [new Cli.BlockerRow("Original blocker", "Resolve original blocker")],
            intent: intent,
            nextTransitions: ["GenerateMilestoneDeepDives"]));
        string stateBefore = repo.Read(Cli.RoadmapArtifactPaths.State);
        var runtime = new ScriptedAgentRuntime();

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.PreflightBlocked, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        Cli.RoadmapStateDocument? state = await new RoadmapStateStore(repo.Artifacts).LoadAsync();
        Assert.NotNull(state);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, state.CurrentState);
        Assert.Equal(Cli.RoadmapState.CreateNewEpic, state.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, state.LastTransition.To);
        Assert.Equal("CreateNewEpic", state.LastTransition.Prompt);
        Assert.Equal(Cli.RoadmapArtifactPaths.ActiveEpic, state.LastTransition.Output);
        Assert.Equal("ResumeMilestoneGeneration", state.TransitionIntent.Intent);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, state.TransitionIntent.DispatchState);
        Assert.Contains(".agents/evidence/original.md", state.TransitionIntent.EvidencePaths);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker == "Original blocker");
        Assert.DoesNotContain(state.Blockers, blocker => blocker.Blocker.Contains("Project Context source contract violation", StringComparison.Ordinal));
        Assert.Contains("GenerateMilestoneDeepDives", state.NextValidTransitions);
        Assert.DoesNotContain("Repair Project Context and rerun", state.NextValidTransitions);
        Assert.Equal(stateBefore, repo.Read(Cli.RoadmapArtifactPaths.State));
        Assert.DoesNotContain("| Prompt | Preflight |", repo.Read(Cli.RoadmapArtifactPaths.State), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resume_planning_block_preserves_persisted_state()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(
            Cli.RoadmapState.ActiveEpicReady,
            from: Cli.RoadmapState.CreateNewEpic,
            prompt: "CreateNewEpic",
            output: Cli.RoadmapArtifactPaths.ActiveEpic,
            nextTransitions: ["GenerateMilestoneDeepDives"]));
        string stateBefore = repo.Read(Cli.RoadmapArtifactPaths.State);
        var runtime = new ScriptedAgentRuntime();

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, state.CurrentState);
        Assert.Empty(state.Blockers);
        Assert.Equal(stateBefore, repo.Read(Cli.RoadmapArtifactPaths.State));
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "*.md"));
    }

    [Fact]
    public async Task Existing_terminal_paused_state_is_not_restarted()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(Cli.RoadmapState.StrategicInvestigationRequired));
        var runtime = new ScriptedAgentRuntime();

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        Assert.Contains("StrategicInvestigationRequired", repo.Read(Cli.RoadmapArtifactPaths.State), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_active_epic_resumes_without_rerunning_selection()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(Cli.RoadmapArtifactPaths.ActiveEpic, Cli.ArtifactLifecycleState.Ready);
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(
            Cli.RoadmapState.ActiveEpicReady,
            from: Cli.RoadmapState.CreateNewEpic,
            prompt: "CreateNewEpic",
            output: Cli.RoadmapArtifactPaths.ActiveEpic));
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic")),
            ScriptedAgentRuntime.Completed(MilestoneBundle()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift")),
            ScriptedAgentRuntime.Completed(CompletionEvaluation("Continue Epic")));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(4, runtime.OneShotCalls);
        Assert.All(runtime.Prompts, prompt => Assert.DoesNotContain("SelectNextEpic", prompt, StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(repo.Root, Cli.RoadmapArtifactPaths.Selection.Replace('/', Path.DirectorySeparatorChar))) == false);
        Assert.Contains("ExecutionLoop", repo.Read(Cli.RoadmapArtifactPaths.State), StringComparison.Ordinal);
    }

    private static Cli.RoadmapStateDocument State(
        Cli.RoadmapState state,
        Cli.RoadmapState? from = null,
        Cli.RoadmapState? to = null,
        string prompt = "None",
        string output = "None",
        IReadOnlyList<Cli.BlockerRow>? blockers = null,
        Cli.RoadmapTransitionIntent? intent = null,
        IReadOnlyList<string>? nextTransitions = null) =>
        new(
            state,
            [],
            new Cli.RoadmapTransitionSummary(from ?? state, to ?? state, prompt, "None", output, "Completed", Cli.TransitionStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            blockers ?? [],
            "None",
            0,
            0,
            new Cli.ProjectionManifestCounts(0, 0, 0),
            intent ?? Cli.RoadmapTransitionIntent.Empty(state),
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
