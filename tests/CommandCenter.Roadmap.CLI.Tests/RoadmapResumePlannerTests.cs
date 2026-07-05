using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class RoadmapResumePlannerTests
{
    [Fact]
    public async Task No_state_file_initializes_core_ready()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(null, context, CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.ContinueFromCoreReady, plan.Action);
        Assert.True(plan.ShouldPersistCoreReady);
    }

    [Fact]
    public async Task Existing_core_ready_state_continues_without_reinitializing()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(RoadmapState.CoreReady), context, CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.ContinueFromCoreReady, plan.Action);
        Assert.False(plan.ShouldPersistCoreReady);
    }

    [Fact]
    public async Task Roadmap_completion_context_ready_resumes_selection()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);
        SeedCompletionContext(repo);

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(RoadmapState.RoadmapCompletionContextReady), context, CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.SelectNextStrategicInitiative, plan.Action);
    }

    [Fact]
    public async Task Select_next_strategic_initiative_with_ready_selection_continues_existing_decision()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);
        SeedCompletionContext(repo);
        repo.Write(RoadmapArtifactPaths.Selection, StrategicInvestigationSelection());

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(RoadmapState.SelectNextStrategicInitiative, prompt: "SelectNextEpic", output: RoadmapArtifactPaths.Selection), context, CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.ContinueSelectionDecision, plan.Action);
    }

    [Fact]
    public async Task Select_next_strategic_initiative_without_selection_runs_selection()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);
        SeedCompletionContext(repo);

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(RoadmapState.SelectNextStrategicInitiative), context, CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.SelectNextStrategicInitiative, plan.Action);
    }

    [Fact]
    public async Task Active_epic_ready_resumes_at_milestone_generation()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);
        await SeedActiveEpicAsync(repo);

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(RoadmapState.ActiveEpicReady, prompt: "CreateNewEpic", output: RoadmapArtifactPaths.ActiveEpic), context, CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.PrepareExecutionFromActiveEpic, plan.Action);
    }

    [Fact]
    public async Task Milestone_specs_ready_resumes_at_execution_readiness()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);
        await SeedActiveEpicAsync(repo);
        await SeedSpecAsync(repo);

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(RoadmapState.MilestoneSpecsReady, prompt: "GenerateMilestoneDeepDivesForEpic", output: RoadmapArtifactPaths.SpecsDirectory), context, CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.PrepareExecutionFromMilestoneSpecs, plan.Action);
    }

    [Fact]
    public async Task Execution_prompt_ready_resumes_execution()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);
        await SeedExecutionReadyAsync(repo);

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(RoadmapState.ExecutionPromptReady, output: RoadmapArtifactPaths.ExecutionPrompt), context, CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.RunExecution, plan.Action);
    }

    [Fact]
    public async Task Evidence_blocked_state_remains_paused()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(
            State(RoadmapState.EvidenceBlocked, blockers: [new BlockerRow("Need evidence", "Collect it")]),
            context,
            CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.Terminal, plan.Action);
        Assert.Equal(RoadmapOutcome.Paused, plan.TerminalOutcome);
    }

    [Fact]
    public async Task Terminal_paused_selection_states_do_not_auto_resume()
    {
        RoadmapState[] states =
        [
            RoadmapState.StrategicInvestigationRequired,
            RoadmapState.RoadmapRevisionRequired,
            RoadmapState.NoSuitableInitiative,
        ];

        foreach (RoadmapState state in states)
        {
            using var repo = new TempRepo();
            ProjectContext context = await SeedProjectAsync(repo);

            RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(state), context, CancellationToken.None);

            Assert.Equal(RoadmapResumeAction.Terminal, plan.Action);
            Assert.Equal(RoadmapOutcome.Paused, plan.TerminalOutcome);
        }
    }

    [Fact]
    public async Task Cancelled_state_recovers_from_transition_intent_when_artifacts_are_ready()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);
        await SeedExecutionReadyAsync(repo);

        RoadmapStateDocument cancelled = State(
            RoadmapState.Cancelled,
            status: TransitionStatus.Cancelled,
            from: RoadmapState.ExecutionPromptReady,
            to: RoadmapState.Cancelled,
            output: RoadmapArtifactPaths.ExecutionPrompt,
            intent: new RoadmapTransitionIntent("ResumeCancelledTransition", RoadmapState.ExecutionLoop, [RoadmapArtifactPaths.ExecutionPrompt]));

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(cancelled, context, CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.RunExecution, plan.Action);
    }

    [Fact]
    public async Task Valid_state_with_missing_required_artifact_is_blocked()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(RoadmapState.ActiveEpicReady), context, CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.Block, plan.Action);
        Assert.Contains("Active epic", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stale_projection_manifest_blocks_resume()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);
        await SeedActiveEpicAsync(repo);
        await new ProjectionManifestStore(repo.Artifacts).UpsertAsync(new ProjectionManifestEntry(
            "CreateNewEpic",
            "CreateNewEpic",
            RoadmapArtifactPaths.ProjectionPaths["CreateNewEpic"],
            "projection-prompt-hash",
            RoadmapArtifactPaths.ProjectContextSourceFiles,
            "old-context-hash",
            "projection-hash",
            DateTimeOffset.UtcNow,
            ProjectionValidationStatus.Valid,
            ProjectionStaleStatus.Stale,
            null));

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(RoadmapState.ActiveEpicReady, prompt: "CreateNewEpic", output: RoadmapArtifactPaths.ActiveEpic), context, CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.Block, plan.Action);
        Assert.Contains("stale", plan.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Artifact_mismatch_blocks_resume()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);
        await SeedActiveEpicAsync(repo);
        repo.Write(".agents/specs/mismatch.md", """
            # Mismatch

            | Field | Value |
            |---|---|
            | Epic Path | .agents/other-epic.md |

            ## Acceptance Criteria

            - [ ] Do the work.
            """);
        await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/mismatch.md");

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(RoadmapState.MilestoneSpecsReady, output: RoadmapArtifactPaths.SpecsDirectory), context, CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.Block, plan.Action);
        Assert.Contains("not the active epic", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_transition_without_outputs_is_blocked()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);
        await SeedActiveEpicAsync(repo);

        RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(
            State(RoadmapState.MilestoneSpecsReady, status: TransitionStatus.Started, from: RoadmapState.ActiveEpicReady, to: RoadmapState.MilestoneSpecsReady, prompt: "GenerateMilestoneDeepDivesForEpic", output: RoadmapArtifactPaths.SpecsDirectory),
            context,
            CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.Block, plan.Action);
        Assert.Contains("output artifacts are not ready", plan.Reason, StringComparison.Ordinal);
    }

    private static RoadmapResumePlanner CreatePlanner(TempRepo repo)
    {
        var projections = new ProjectionRegistry();
        var contracts = new PromptContractRegistry(projections);
        var manifest = new ProjectionManifestStore(repo.Artifacts);
        var lifecycle = new ArtifactLifecycleStore(repo.Artifacts);
        ExecutionPreparationProvenanceService executionPreparation = ExecutionPreparationTestSupport.CreateProvenance(repo);
        return new RoadmapResumePlanner(repo.Artifacts, contracts, manifest, lifecycle, new ProjectionProvenanceFactory(projections), executionPreparation);
    }

    private static async Task<ProjectContext> SeedProjectAsync(TempRepo repo)
    {
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap");
        return await new ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);
    }

    private static void SeedCompletionContext(TempRepo repo) =>
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "# Roadmap Completion Context");

    private static async Task SeedActiveEpicAsync(TempRepo repo)
    {
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Ready);
    }

    private static async Task SeedSpecAsync(TempRepo repo)
    {
        repo.Write(".agents/specs/test.md", """
            # Test Spec

            | Field | Value |
            |---|---|
            | Epic Path | .agents/epic.md |

            ## Acceptance Criteria

            - [ ] Do the work.
            """);
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(".agents/specs/test.md", ArtifactLifecycleState.Ready);
        await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/test.md");
    }

    private static async Task SeedExecutionReadyAsync(TempRepo repo)
    {
        await SeedActiveEpicAsync(repo);
        await SeedSpecAsync(repo);
        ExecutionPreparationProvenanceService provenance = ExecutionPreparationTestSupport.CreateProvenance(repo);
        await ExecutionPreparationTestSupport.SeedOperationalContextAsync(provenance, repo, "# Operational Context");
        await ExecutionPreparationTestSupport.SeedExecutionPromptAsync(provenance, repo, "# Execution Prompt");
        var lifecycle = new ArtifactLifecycleStore(repo.Artifacts);
        await lifecycle.UpsertAsync(RoadmapArtifactPaths.OperationalContext, ArtifactLifecycleState.Ready);
        await lifecycle.UpsertAsync(RoadmapArtifactPaths.ExecutionPrompt, ArtifactLifecycleState.Ready);
    }

    private static RoadmapStateDocument State(
        RoadmapState state,
        TransitionStatus status = TransitionStatus.Completed,
        RoadmapState? from = null,
        RoadmapState? to = null,
        string prompt = "None",
        string output = "None",
        IReadOnlyList<BlockerRow>? blockers = null,
        RoadmapTransitionIntent? intent = null) =>
        new(
            state,
            [],
            new RoadmapTransitionSummary(from ?? state, to ?? state, prompt, "None", output, "Completed", status, DateTimeOffset.UtcNow, status == TransitionStatus.Started ? null : DateTimeOffset.UtcNow),
            blockers ?? [],
            "None",
            0,
            0,
            new ProjectionManifestCounts(0, 0, 0),
            intent ?? RoadmapTransitionIntent.Empty(state),
            [],
            []);

    private static string StrategicInvestigationSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Strategic Investigation Required |
        | Recommended Initiative | Investigate A |
        | Initiative Type | Strategic Investigation |
        | Confidence | Medium |
        | Primary Reason | Evidence is insufficient |
        """;
}
