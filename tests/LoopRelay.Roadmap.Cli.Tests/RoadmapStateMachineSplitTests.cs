using System.Text.Json;
using LoopRelay.Roadmap.Cli;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapStateMachineSplitTests
{
    [Fact]
    public async Task Spec_only_split_bundle_blocks_before_any_write_and_preserves_active_epic()
    {
        using var repo = SeedRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
        string splitOutput = """
            # FILE: .agents/specs/not-a-child.md
            # Not A Child Epic
            """;
        var runtime = SplitRuntime(splitOutput);

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(4, runtime.OneShotCalls);
        Assert.Equal(existing, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.False(await repo.Artifacts.ExistsAsync(".agents/specs/not-a-child.md"));
        Assert.False(await repo.Artifacts.ExistsAsync(".agents/specs/split-test.md"));
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.json"));

        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal("Split Bundle Rejected", state.LastTransition.Decision);
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Contains(".agents/specs/not-a-child.md", repo.Read(evidencePath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partially_valid_split_bundle_writes_no_child_files_or_split_family()
    {
        using var repo = SeedRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
        string validChild = RoadmapSamples.ValidEpic("Valid Child", "EPIC-CHILD");
        string splitOutput = $"""
            # FILE: .agents/epic-1.md
            {validChild}

            # FILE: .agents/specs/not-a-child.md
            # Not A Child Epic
            """;
        var runtime = SplitRuntime(splitOutput);

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(4, runtime.OneShotCalls);
        Assert.Equal(existing, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.False(await repo.Artifacts.ExistsAsync(".agents/epic-1.md"));
        Assert.False(await repo.Artifacts.ExistsAsync(".agents/specs/not-a-child.md"));
        Assert.False(await repo.Artifacts.ExistsAsync(".agents/bundle-manifest.md"));
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.json"));
        Assert.DoesNotContain(
            await new Cli.ArtifactLifecycleStore(repo.Artifacts).LoadAsync(),
            entry => entry.Path is ".agents/epic-1.md" or ".agents/specs/not-a-child.md");

        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal("Split Bundle Rejected", state.LastTransition.Decision);
        Assert.Equal("ResolveSplitEpicBlocker", state.TransitionIntent.Intent);
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        string evidence = repo.Read(evidencePath);
        Assert.Contains(".agents/epic-1.md", evidence, StringComparison.Ordinal);
        Assert.Contains(".agents/specs/not-a-child.md", evidence, StringComparison.Ordinal);
        Assert.Contains("## Raw Output", evidence, StringComparison.Ordinal);
        Assert.Contains(validChild, evidence, StringComparison.Ordinal);
        Assert.Contains("SplitBundleRejected", repo.Read(Cli.RoadmapArtifactPaths.TransitionJournal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Direct_active_epic_target_is_rejected_before_overwrite()
    {
        using var repo = SeedRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
        string splitOutput = $"""
            # FILE: .agents/epic.md
            {RoadmapSamples.ValidEpic("Illegal Direct Active Epic", "EPIC-DIRECT")}
            """;

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, SplitRuntime(splitOutput)).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(existing, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Contains(".agents/epic.md", repo.Read(evidencePath), StringComparison.Ordinal);
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.json"));
    }

    [Fact]
    public async Task Malformed_child_plus_direct_active_target_preserves_active_epic_and_writes_no_children()
    {
        using var repo = SeedRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
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

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, SplitRuntime(splitOutput)).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(existing, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.False(await repo.Artifacts.ExistsAsync(".agents/epic-1.md"));
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string evidence = repo.Read(Assert.Single(state.TransitionIntent.EvidencePaths));
        Assert.Contains(".agents/epic-1.md", evidence, StringComparison.Ordinal);
        Assert.Contains(".agents/epic.md", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Blocked_split_output_routes_to_blocked_state_without_milestones()
    {
        using var repo = SeedRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
        string splitOutput = """
            # Split Epic Blocked

            ## Reason

            The selected initiative is not safely decomposable.
            """;
        var runtime = SplitRuntime(splitOutput);

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(4, runtime.OneShotCalls);
        Assert.Equal(existing, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
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
            ScriptedAgentRuntime.Completed(MilestoneBundle()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(6, runtime.OneShotCalls);
        Assert.Equal(childOne, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.NotEqual(splitOutput, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.Equal(childOne, repo.Read(".agents/epic-1.md"));
        Assert.Equal(childTwo, repo.Read(".agents/epic-2.md"));

        Cli.TransitionJournalRecord[] journal = ReadJournal(repo);
        Cli.TransitionJournalRecord splitCompleted = Assert.Single(journal, record =>
            record.Event == "TransitionCompleted" &&
            record.Prompt == "SplitEpic");
        Cli.TransitionJournalRecord artifactPromoted = Assert.Single(journal, record =>
            record.Event == "ArtifactPromoted" &&
            record.Prompt == "SplitEpic");
        Assert.Equal(splitCompleted.CorrelationId, artifactPromoted.CorrelationId);
        Assert.Equal(splitCompleted.DurationMilliseconds, artifactPromoted.DurationMilliseconds);
        Assert.Equal(splitCompleted.InputArtifactHashes.OrderBy(pair => pair.Key), artifactPromoted.InputArtifactHashes.OrderBy(pair => pair.Key));
        Assert.NotNull(splitCompleted.InputSnapshot);
        Assert.NotNull(artifactPromoted.InputSnapshot);
        Assert.Equal(splitCompleted.InputSnapshot.SnapshotHash, artifactPromoted.InputSnapshot.SnapshotHash);
        Assert.Equal(splitCompleted.InputSnapshot.RuntimePromptName, artifactPromoted.InputSnapshot.RuntimePromptName);
        Assert.Equal(splitCompleted.InputSnapshot.Projection, artifactPromoted.InputSnapshot.Projection);
        Assert.Equal(splitCompleted.InputSnapshot.PromptContextHash, artifactPromoted.InputSnapshot.PromptContextHash);
        Assert.Equal(splitCompleted.InputSnapshot.SecondaryInputHash, artifactPromoted.InputSnapshot.SecondaryInputHash);
        Assert.Equal(splitCompleted.InputSnapshot.ArtifactInputs, artifactPromoted.InputSnapshot.ArtifactInputs);
        Assert.Equal(Cli.RoadmapState.SplitEpicProposed, splitCompleted.PreviousState);
        Assert.Equal(Cli.RoadmapState.SplitChildSelection, splitCompleted.AttemptedState);
        Assert.Equal(Cli.RoadmapState.SplitChildSelection, artifactPromoted.PreviousState);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, artifactPromoted.AttemptedState);
        Assert.Equal([Cli.RoadmapArtifactPaths.SplitFamiliesDirectory], splitCompleted.OutputPaths);
        Assert.Equal([Cli.RoadmapArtifactPaths.ActiveEpic], artifactPromoted.OutputPaths);
        Assert.Equal("ArtifactPromotionService", artifactPromoted.PromptContractKey);

        string familyPath = Assert.Single(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.json"));
        string family = repo.Read(familyPath);
        Assert.Contains(".agents/epic-1.md", family, StringComparison.Ordinal);
        Assert.Contains(".agents/epic-2.md", family, StringComparison.Ordinal);
        Assert.DoesNotContain(".agents/specs", family, StringComparison.Ordinal);
        Assert.Contains("\"SelectedChildPath\": \".agents/epic-1.md\"", family, StringComparison.Ordinal);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.MilestoneSpecsReady, state.CurrentState);
        Assert.False(await repo.Artifacts.ExistsAsync(Cli.RoadmapArtifactPaths.OperationalContext));
        Assert.False(await repo.Artifacts.ExistsAsync(Cli.RoadmapArtifactPaths.ExecutionPrompt));
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
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

    private static Cli.TransitionJournalRecord[] ReadJournal(TempRepo repo) =>
        repo.Read(Cli.RoadmapArtifactPaths.TransitionJournal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<Cli.TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .ToArray();

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
