using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapResumePlannerTests
{
    [Fact]
    public async Task No_state_file_initializes_core_ready()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(null, context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.ContinueFromCoreReady, plan.Action);
        Assert.True(plan.ShouldPersistCoreReady);
    }

    [Fact]
    public async Task Existing_core_ready_state_continues_without_reinitializing()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.CoreReady), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.ContinueFromCoreReady, plan.Action);
        Assert.False(plan.ShouldPersistCoreReady);
    }

    [Fact]
    public async Task Roadmap_completion_context_ready_resumes_selection()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        SeedCompletionContext(repo);

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.RoadmapCompletionContextReady), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.SelectNextStrategicInitiative, plan.Action);
    }

    [Fact]
    public async Task Select_next_strategic_initiative_with_ready_selection_continues_existing_decision()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        SeedCompletionContext(repo);
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, StrategicInvestigationSelection());

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.SelectNextStrategicInitiative, prompt: "SelectNextEpic", output: Cli.RoadmapArtifactPaths.Selection), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.ContinueSelectionDecision, plan.Action);
    }

    [Fact]
    public async Task Select_next_strategic_initiative_with_missing_selection_provenance_regenerates_selection()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        SeedCompletionContext(repo);
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        repo.Write(Cli.RoadmapArtifactPaths.Selection, StrategicInvestigationSelection());

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.SelectNextStrategicInitiative, prompt: "SelectNextEpic", output: Cli.RoadmapArtifactPaths.Selection), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.SelectNextStrategicInitiative, plan.Action);
        Assert.Contains("current selection cycle", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Select_next_strategic_initiative_with_stale_selection_provenance_regenerates_selection()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        SeedCompletionContext(repo);
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, StrategicInvestigationSelection());
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "# Changed Completion Context");

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.SelectNextStrategicInitiative, prompt: "SelectNextEpic", output: Cli.RoadmapArtifactPaths.Selection), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.SelectNextStrategicInitiative, plan.Action);
        Assert.Contains(nameof(Cli.DerivedArtifactStaleReason.RoadmapCompletionContextDrift), plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Select_next_strategic_initiative_with_archived_selection_regenerates_selection()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        SeedCompletionContext(repo);
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, StrategicInvestigationSelection());
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(Cli.RoadmapArtifactPaths.Selection, Cli.ArtifactLifecycleState.Archived);

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.SelectNextStrategicInitiative, prompt: "SelectNextEpic", output: Cli.RoadmapArtifactPaths.Selection), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.SelectNextStrategicInitiative, plan.Action);
    }

    [Theory]
    [InlineData("Select New Intermediary Epic")]
    [InlineData("Select Split Epic")]
    [InlineData("Select Existing Epic")]
    public async Task Stale_completed_selection_never_resumes_downstream_planning(string staleOutcome)
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        SeedCompletionContext(repo);
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, SelectionForOutcome(staleOutcome));
        repo.Write(".agents/roadmap/001-roadmap.md", "changed roadmap");

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.SelectNextStrategicInitiative, prompt: "SelectNextEpic", output: Cli.RoadmapArtifactPaths.Selection), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.SelectNextStrategicInitiative, plan.Action);
        Assert.Contains(nameof(Cli.DerivedArtifactStaleReason.RoadmapSourceDrift), plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Select_next_strategic_initiative_without_selection_runs_selection()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        SeedCompletionContext(repo);

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.SelectNextStrategicInitiative), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.SelectNextStrategicInitiative, plan.Action);
    }

    [Fact]
    public async Task Active_epic_ready_resumes_at_milestone_generation()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        await SeedActiveEpicAsync(repo);

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.ActiveEpicReady, prompt: "CreateNewEpic", output: Cli.RoadmapArtifactPaths.ActiveEpic), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.GenerateMilestoneSpecs, plan.Action);
    }

    [Fact]
    public async Task Milestone_specs_ready_pauses_before_execution_context_generation()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        await SeedActiveEpicAsync(repo);
        await SeedSpecAsync(repo);

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.MilestoneSpecsReady, prompt: "GenerateMilestoneDeepDivesForEpic", output: Cli.RoadmapArtifactPaths.SpecsDirectory), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.Terminal, plan.Action);
        Assert.Equal(Cli.RoadmapOutcome.Paused, plan.TerminalOutcome);
        Assert.Contains("stops before execution context generation", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Legacy_execution_prompt_ready_is_not_advanced_by_roadmap_cli()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        await SeedExecutionReadyAsync(repo);

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.ExecutionPromptReady, output: Cli.RoadmapArtifactPaths.ExecutionPrompt), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.Terminal, plan.Action);
        Assert.Equal(Cli.RoadmapOutcome.Paused, plan.TerminalOutcome);
        Assert.Contains("no longer advanced by Roadmap CLI", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Evidence_blocked_state_remains_paused()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(
            State(Cli.RoadmapState.EvidenceBlocked, blockers: [new Cli.BlockerRow("Need evidence", "Collect it")]),
            context,
            CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.Terminal, plan.Action);
        Assert.Equal(Cli.RoadmapOutcome.Paused, plan.TerminalOutcome);
    }

    [Fact]
    public async Task Terminal_paused_selection_states_do_not_auto_resume()
    {
        Cli.RoadmapState[] states =
        [
            Cli.RoadmapState.StrategicInvestigationRequired,
            Cli.RoadmapState.RoadmapRevisionRequired,
            Cli.RoadmapState.NoSuitableInitiative,
        ];

        foreach (Cli.RoadmapState state in states)
        {
            using var repo = new TempRepo();
            Cli.ProjectContext context = await SeedProjectAsync(repo);

            Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(state), context, CancellationToken.None);

            Assert.Equal(Cli.RoadmapResumeAction.Terminal, plan.Action);
            Assert.Equal(Cli.RoadmapOutcome.Paused, plan.TerminalOutcome);
        }
    }

    [Fact]
    public async Task Cancelled_state_recovers_from_transition_intent_when_artifacts_are_ready()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        await SeedExecutionReadyAsync(repo);

        Cli.RoadmapStateDocument cancelled = State(
            Cli.RoadmapState.Cancelled,
            status: Cli.TransitionStatus.Cancelled,
            from: Cli.RoadmapState.ExecutionPromptReady,
            to: Cli.RoadmapState.Cancelled,
            output: Cli.RoadmapArtifactPaths.ExecutionPrompt,
            intent: new Cli.RoadmapTransitionIntent("ResumeCancelledTransition", Cli.RoadmapState.ExecutionLoop, [Cli.RoadmapArtifactPaths.ExecutionPrompt]));

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(cancelled, context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.Terminal, plan.Action);
        Assert.Equal(Cli.RoadmapOutcome.Paused, plan.TerminalOutcome);
    }

    [Fact]
    public async Task Valid_state_with_missing_required_artifact_is_blocked()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.ActiveEpicReady), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.Block, plan.Action);
        Assert.Contains("Active epic", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stale_projection_manifest_blocks_resume()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        await SeedActiveEpicAsync(repo);
        await new Cli.ProjectionManifestStore(repo.Artifacts).UpsertAsync(new Cli.ProjectionManifestEntry(
            "CreateNewEpic",
            "CreateNewEpic",
            Cli.RoadmapArtifactPaths.ProjectionPaths["CreateNewEpic"],
            "projection-prompt-hash",
            Cli.RoadmapArtifactPaths.ProjectContextSourceFiles,
            "old-context-hash",
            "projection-hash",
            DateTimeOffset.UtcNow,
            Cli.ProjectionValidationStatus.Valid,
            Cli.ProjectionStaleStatus.Stale,
            null));

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.ActiveEpicReady, prompt: "CreateNewEpic", output: Cli.RoadmapArtifactPaths.ActiveEpic), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.Block, plan.Action);
        Assert.Contains("stale", plan.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Artifact_mismatch_blocks_resume()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
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

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(State(Cli.RoadmapState.MilestoneSpecsReady, output: Cli.RoadmapArtifactPaths.SpecsDirectory), context, CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.Block, plan.Action);
        Assert.Contains("not the active epic", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_transition_without_outputs_is_blocked()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        await SeedActiveEpicAsync(repo);

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo).PlanAsync(
            State(Cli.RoadmapState.MilestoneSpecsReady, status: Cli.TransitionStatus.Started, from: Cli.RoadmapState.ActiveEpicReady, to: Cli.RoadmapState.MilestoneSpecsReady, prompt: "GenerateMilestoneDeepDivesForEpic", output: Cli.RoadmapArtifactPaths.SpecsDirectory),
            context,
            CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.Block, plan.Action);
        Assert.Contains("output artifacts are not ready", plan.Reason, StringComparison.Ordinal);
    }

    private static Cli.RoadmapResumePlanner CreatePlanner(TempRepo repo)
    {
        var projections = new Cli.ProjectionRegistry();
        var contracts = new Cli.PromptContractRegistry(projections);
        var manifest = new Cli.ProjectionManifestStore(repo.Artifacts);
        var lifecycle = new Cli.ArtifactLifecycleStore(repo.Artifacts);
        Cli.ExecutionPreparationProvenanceService executionPreparation = ExecutionPreparationTestSupport.CreateProvenance(repo);
        var contextBuilder = new Cli.RoadmapPromptContextBuilder(repo.Artifacts, executionPreparation);
        var inputResolver = new Cli.TransitionInputResolver(repo.Artifacts, executionPreparation);
        var selectionProvenance = new Cli.SelectionProvenanceService(
            repo.Artifacts,
            new Cli.SelectionProvenanceManifestStore(repo.Artifacts),
            contextBuilder,
            inputResolver);
        return new Cli.RoadmapResumePlanner(repo.Artifacts, contracts, manifest, lifecycle, new Cli.ProjectionProvenanceFactory(projections), selectionProvenance, executionPreparation);
    }

    private static async Task<Cli.ProjectContext> SeedProjectAsync(TempRepo repo)
    {
        repo.SeedProjectContext();
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        return await new ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);
    }

    private static void SeedCompletionContext(TempRepo repo) =>
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "# Roadmap Completion Context");

    private static async Task SeedActiveEpicAsync(TempRepo repo)
    {
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(Cli.RoadmapArtifactPaths.ActiveEpic, Cli.ArtifactLifecycleState.Ready);
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
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(".agents/specs/test.md", Cli.ArtifactLifecycleState.Ready);
        await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/test.md");
    }

    private static async Task SeedExecutionReadyAsync(TempRepo repo)
    {
        await SeedActiveEpicAsync(repo);
        await SeedSpecAsync(repo);
        Cli.ExecutionPreparationProvenanceService provenance = ExecutionPreparationTestSupport.CreateProvenance(repo);
        await ExecutionPreparationTestSupport.SeedOperationalContextAsync(provenance, repo, "# Operational Context");
        await ExecutionPreparationTestSupport.SeedExecutionPromptAsync(provenance, repo, "# Execution Prompt");
        var lifecycle = new Cli.ArtifactLifecycleStore(repo.Artifacts);
        await lifecycle.UpsertAsync(Cli.RoadmapArtifactPaths.OperationalContext, Cli.ArtifactLifecycleState.Ready);
        await lifecycle.UpsertAsync(Cli.RoadmapArtifactPaths.ExecutionPrompt, Cli.ArtifactLifecycleState.Ready);
    }

    private static Cli.RoadmapStateDocument State(
        Cli.RoadmapState state,
        Cli.TransitionStatus status = Cli.TransitionStatus.Completed,
        Cli.RoadmapState? from = null,
        Cli.RoadmapState? to = null,
        string prompt = "None",
        string output = "None",
        IReadOnlyList<Cli.BlockerRow>? blockers = null,
        Cli.RoadmapTransitionIntent? intent = null) =>
        new(
            state,
            [],
            new Cli.RoadmapTransitionSummary(from ?? state, to ?? state, prompt, "None", output, "Completed", status, DateTimeOffset.UtcNow, status == Cli.TransitionStatus.Started ? null : DateTimeOffset.UtcNow),
            blockers ?? [],
            "None",
            0,
            0,
            new Cli.ProjectionManifestCounts(0, 0, 0),
            intent ?? Cli.RoadmapTransitionIntent.Empty(state),
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

    private static string SelectionForOutcome(string outcome)
    {
        string initiativeType = outcome switch
        {
            "Select Existing Epic" => "Existing Roadmap Epic",
            "Select Split Epic" => "Split Epic Proposal",
            _ => "New Intermediary Epic",
        };
        return $$"""
            # Next Strategic Initiative Selection

            ## Recommendation Summary

            | Field | Value |
            |---|---|
            | Recommended Outcome | {{outcome}} |
            | Recommended Initiative | Stale Initiative |
            | Initiative Type | {{initiativeType}} |
            | Confidence | High |
            | Primary Reason | This stale selection must not drive downstream planning. |
            """;
    }
}
