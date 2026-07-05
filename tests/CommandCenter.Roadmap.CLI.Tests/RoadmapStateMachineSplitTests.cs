using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class RoadmapStateMachineSplitTests
{
    [Fact]
    public async Task Spec_only_split_bundle_blocks_before_any_write_and_preserves_active_epic()
    {
        using var repo = SeedRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, existing);
        string splitOutput = """
            # FILE: .agents/specs/not-a-child.md
            # Not A Child Epic
            """;
        var runtime = SplitRuntime(splitOutput);

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(4, runtime.OneShotCalls);
        Assert.Equal(existing, repo.Read(RoadmapArtifactPaths.ActiveEpic));
        Assert.False(await repo.Artifacts.ExistsAsync(".agents/specs/not-a-child.md"));
        Assert.False(await repo.Artifacts.ExistsAsync(".agents/specs/split-test.md"));
        Assert.Empty(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.md"));

        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal("Split Bundle Rejected", state.LastTransition.Decision);
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Contains(".agents/specs/not-a-child.md", repo.Read(evidencePath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Direct_active_epic_target_is_rejected_before_overwrite()
    {
        using var repo = SeedRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, existing);
        string splitOutput = $"""
            # FILE: .agents/epic.md
            {RoadmapSamples.ValidEpic("Illegal Direct Active Epic", "EPIC-DIRECT")}
            """;

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, SplitRuntime(splitOutput)).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(existing, repo.Read(RoadmapArtifactPaths.ActiveEpic));
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Contains(".agents/epic.md", repo.Read(evidencePath), StringComparison.Ordinal);
        Assert.Empty(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.md"));
    }

    [Fact]
    public async Task Malformed_child_plus_direct_active_target_preserves_active_epic_and_writes_no_children()
    {
        using var repo = SeedRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, existing);
        string splitOutput = $"""
            # FILE: .agents/epic-1.md
            # Epic

            ## Epic Metadata

            | Field | Value |
            |---|---|
            | Epic ID | EPIC-BAD |
            | Status | Authored |

            # FILE: .agents/epic.md
            {RoadmapSamples.ValidEpic("Illegal Direct Active Epic", "EPIC-DIRECT")}
            """;

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, SplitRuntime(splitOutput)).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(existing, repo.Read(RoadmapArtifactPaths.ActiveEpic));
        Assert.False(await repo.Artifacts.ExistsAsync(".agents/epic-1.md"));
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string evidence = repo.Read(Assert.Single(state.TransitionIntent.EvidencePaths));
        Assert.Contains(".agents/epic-1.md", evidence, StringComparison.Ordinal);
        Assert.Contains(".agents/epic.md", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Blocked_split_output_routes_to_blocked_state_without_milestones()
    {
        using var repo = SeedRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, existing);
        string splitOutput = """
            # Split Epic Blocked

            ## Reason

            The selected initiative is not safely decomposable.
            """;
        var runtime = SplitRuntime(splitOutput);

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(4, runtime.OneShotCalls);
        Assert.Equal(existing, repo.Read(RoadmapArtifactPaths.ActiveEpic));
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("Split Epic Blocked", state.LastTransition.Decision);
    }

    [Fact]
    public async Task Valid_split_writes_validated_children_and_promotes_selected_child_through_promotion_service()
    {
        using var repo = SeedRepo();
        string childTwo = RoadmapSamples.ValidEpic("Second Child", "EPIC-2");
        string childOne = RoadmapSamples.ValidEpic("First Child", "EPIC-1");
        string splitOutput = $"""
            # FILE: .agents/epic-2.md
            {childTwo}

            # FILE: .agents/epic-1.md
            {childOne}
            """;
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(SplitSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SplitEpic")),
            ScriptedAgentRuntime.Completed(splitOutput),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic")),
            ScriptedAgentRuntime.Completed(MilestoneBundle()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift")),
            ScriptedAgentRuntime.Completed(CompletionEvaluation("Continue Epic")));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(8, runtime.OneShotCalls);
        Assert.Equal(childOne, repo.Read(RoadmapArtifactPaths.ActiveEpic));
        Assert.Equal(childOne, repo.Read(".agents/epic-1.md"));
        Assert.Equal(childTwo, repo.Read(".agents/epic-2.md"));
        Assert.Contains("ArtifactPromotionService", repo.Read(RoadmapArtifactPaths.TransitionJournal), StringComparison.Ordinal);
        Assert.Contains("ArtifactPromoted", repo.Read(RoadmapArtifactPaths.TransitionJournal), StringComparison.Ordinal);

        string familyPath = Assert.Single(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.md"));
        string family = repo.Read(familyPath);
        Assert.Contains("- .agents/epic-1.md", family, StringComparison.Ordinal);
        Assert.Contains("- .agents/epic-2.md", family, StringComparison.Ordinal);
        Assert.DoesNotContain(".agents/specs", family, StringComparison.Ordinal);
        Assert.Contains("| Selected Child | .agents/epic-1.md |", family, StringComparison.Ordinal);
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap");
        return repo;
    }

    private static ScriptedAgentRuntime SplitRuntime(string splitOutput) =>
        new(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(SplitSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SplitEpic")),
            ScriptedAgentRuntime.Completed(splitOutput));

    private static string SplitSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select Split Epic |
        | Recommended Initiative | Split promotion safety epic |
        | Initiative Type | Split Epic |
        | Confidence | High |
        | Primary Reason | Exercise split-domain promotion boundaries. |
        """;

    private static string MilestoneBundle() => """
        # FILE: .agents/specs/split-test.md
        # Split Test Milestone

        | Field | Value |
        |---|---|
        | Epic Path | .agents/epic.md |

        ## Acceptance Criteria

        - [ ] Split child promotion remains explicit.
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
