using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

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
        string state = repo.Read(RoadmapArtifactPaths.State);
        Assert.Contains("EvidenceBlocked", state, StringComparison.Ordinal);
        Assert.Contains("Missing evidence", state, StringComparison.Ordinal);
        Assert.DoesNotContain("| Prompt | Preflight |", state, StringComparison.Ordinal);
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
        Assert.Contains("StrategicInvestigationRequired", repo.Read(RoadmapArtifactPaths.State), StringComparison.Ordinal);
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
            ScriptedAgentRuntime.Completed(MilestoneBundle()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift")),
            ScriptedAgentRuntime.Completed(CompletionEvaluation("Continue Epic")));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(4, runtime.OneShotCalls);
        Assert.All(runtime.Prompts, prompt => Assert.DoesNotContain("SelectNextEpic", prompt, StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(repo.Root, RoadmapArtifactPaths.Selection.Replace('/', Path.DirectorySeparatorChar))) == false);
        Assert.Contains("ExecutionLoop", repo.Read(RoadmapArtifactPaths.State), StringComparison.Ordinal);
    }

    private static RoadmapStateDocument State(
        RoadmapState state,
        RoadmapState? from = null,
        string prompt = "None",
        string output = "None",
        IReadOnlyList<BlockerRow>? blockers = null) =>
        new(
            state,
            [],
            new RoadmapTransitionSummary(from ?? state, state, prompt, "None", output, "Completed", TransitionStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            blockers ?? [],
            "None",
            0,
            0,
            new ProjectionManifestCounts(0, 0, 0),
            RoadmapTransitionIntent.Empty(state),
            [],
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
