using LoopRelay.Agents.Models;
using LoopRelay.Roadmap.Cli;
using ExecutionCompatibilityMaterializer = LoopRelay.Roadmap.Cli.ExecutionCompatibilityMaterializer;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapStateMachineUnblockTests
{
    [Fact]
    public async Task Status_reports_blocked_state_without_mutation()
    {
        using var repo = new TempRepo();
        await SaveStateAsync(repo, BlockedState("ResolveBlocker", Cli.RoadmapState.EvidenceBlocked, ".agents/evidence/blockers/preflight.0001.md"));
        string before = repo.Read(Cli.RoadmapArtifactPaths.StateJson);

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).ExecuteAsync(
            Cli.RoadmapCliCommand.Status,
            CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(before, repo.Read(Cli.RoadmapArtifactPaths.StateJson));
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "unblock-review.*.md"));
    }

    [Fact]
    public async Task Run_reports_blocked_state_without_mutation()
    {
        using var repo = new TempRepo();
        await SaveStateAsync(repo, BlockedState("ResolveBlocker", Cli.RoadmapState.EvidenceBlocked, ".agents/evidence/blockers/preflight.0001.md"));
        string before = repo.Read(Cli.RoadmapArtifactPaths.StateJson);

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).ExecuteAsync(
            Cli.RoadmapCliCommand.Run,
            CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(before, repo.Read(Cli.RoadmapArtifactPaths.StateJson));
    }

    [Fact]
    public async Task ResolveBlocker_missing_project_context_preserves_original_blocker_and_appends_review_once()
    {
        using var repo = new TempRepo();
        const string blockerPath = ".agents/evidence/blockers/preflight.0001.md";
        repo.Write(blockerPath, "original preflight blocker");
        await SaveStateAsync(repo, BlockedState(
            "ResolveBlocker",
            Cli.RoadmapState.EvidenceBlocked,
            blockerPath,
            prompt: "Preflight",
            blockers: [new Cli.BlockerRow("Original Project Context blocker", "Repair Project Context")]));

        Cli.RoadmapStateMachine machine = StateMachineFactory.Create(repo, new ScriptedAgentRuntime());
        Cli.RoadmapOutcome first = await machine.UnblockAsync(CancellationToken.None);
        Cli.RoadmapOutcome second = await machine.UnblockAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, first);
        Assert.Equal(Cli.RoadmapOutcome.Paused, second);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("Preflight", state.LastTransition.Prompt);
        Assert.Equal("ResolveBlocker", state.TransitionIntent.Intent);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker == "Original Project Context blocker");
        Assert.Single(state.Blockers, blocker => blocker.Blocker.StartsWith("Unblock Review Failed:", StringComparison.Ordinal));
        IReadOnlyList<string> reviews = await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "unblock-review.*.md");
        Assert.Equal(2, reviews.Count);
        Assert.Contains("Original Project Context blocker", repo.Read(reviews.Order(StringComparer.Ordinal).First()), StringComparison.Ordinal);
        Assert.Equal("original preflight blocker", repo.Read(blockerPath));
    }

    [Fact]
    public async Task ResolveBlocker_repaired_project_context_recovers_to_core_ready()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap source");
        const string blockerPath = ".agents/evidence/blockers/preflight.0001.md";
        repo.Write(blockerPath, "original preflight blocker");
        await SaveStateAsync(repo, BlockedState(
            "ResolveBlocker",
            Cli.RoadmapState.EvidenceBlocked,
            blockerPath,
            prompt: "Preflight",
            blockers: [new Cli.BlockerRow("Original Project Context blocker", "Repair Project Context")]));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.CoreReady, state.CurrentState);
        Assert.Equal("UnblockReview", state.LastTransition.Prompt);
        Assert.Equal("Project Context blocker resolved.", state.LastTransition.Decision);
        Assert.Empty(state.Blockers);
        string review = repo.Read(Assert.Single(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "unblock-review.*.md")));
        Assert.Contains("| Result | Recovered |", review, StringComparison.Ordinal);
        Assert.Contains("ProjectContext", review, StringComparison.Ordinal);
        Assert.Contains("SHA-256", review, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveMalformedExecutionOutput_corrected_disposition_recovers_to_execution_route()
    {
        using var repo = SeedProject();
        const string evidencePath = ".agents/evidence/execution/execution-turn.0001.md";
        string evidence = ExecutionDisposition("Continue Required", "ContinueExecution");
        repo.Write(evidencePath, evidence);
        await SaveStateAsync(repo, BlockedState(
            "ResolveMalformedExecutionOutput",
            Cli.RoadmapState.EvidenceBlocked,
            evidencePath,
            from: Cli.RoadmapState.ExecutionLoop,
            prompt: "ExecutionOutcomeInterpretation",
            decision: "Malformed Execution Output"));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.ExecutionLoop, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal("Continue Required", state.LastTransition.Decision);
        Assert.Equal("ContinueExecution", state.TransitionIntent.Intent);
        Assert.Contains(evidencePath, state.TransitionIntent.EvidencePaths);
        Assert.Empty(state.Blockers);
        Assert.Equal(evidence, repo.Read(evidencePath));
        Assert.Contains("UnblockReviewCompleted", repo.Read(Cli.RoadmapArtifactPaths.TransitionJournal), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("# Execution Report\n\nNo disposition.")]
    [InlineData("""
        # Execution Report

        ## Execution Disposition

        | Field | Value |
        |---|---|
        | Status | Epic Complete |
        | Confidence | High |
        | Evidence Summary | Contradictory output. |
        | Next Step | ContinueExecution |
        """)]
    public async Task ResolveMalformedExecutionOutput_invalid_repair_remains_blocked(string evidence)
    {
        using var repo = SeedProject();
        const string evidencePath = ".agents/evidence/execution/execution-turn.0001.md";
        repo.Write(evidencePath, evidence);
        await SaveStateAsync(repo, BlockedState(
            "ResolveMalformedExecutionOutput",
            Cli.RoadmapState.EvidenceBlocked,
            evidencePath,
            from: Cli.RoadmapState.ExecutionLoop,
            prompt: "ExecutionOutcomeInterpretation",
            decision: "Malformed Execution Output",
            blockers: [new Cli.BlockerRow("Original malformed output", "Repair execution evidence")]));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("ExecutionOutcomeInterpretation", state.LastTransition.Prompt);
        Assert.Equal("ResolveMalformedExecutionOutput", state.TransitionIntent.Intent);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker == "Original malformed output");
        Assert.Contains(state.Blockers, blocker => blocker.Blocker.StartsWith("Unblock Review Failed:", StringComparison.Ordinal));
        Assert.Equal(evidence, repo.Read(evidencePath));
        string review = repo.Read(Assert.Single(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "unblock-review.*.md")));
        Assert.Contains("Original malformed output", review, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveMalformedExecutionOutput_mismatched_evidence_path_remains_blocked()
    {
        using var repo = SeedProject();
        const string evidencePath = ".agents/evidence/execution/execution-turn.0001.md";
        repo.Write(evidencePath, ExecutionDisposition("Continue Required", "ContinueExecution"));
        await SaveStateAsync(repo, BlockedState(
            "ResolveMalformedExecutionOutput",
            Cli.RoadmapState.EvidenceBlocked,
            evidencePath,
            output: ".agents/evidence/execution/other.md",
            from: Cli.RoadmapState.ExecutionLoop,
            prompt: "ExecutionOutcomeInterpretation"));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("ResolveMalformedExecutionOutput", state.TransitionIntent.Intent);
        Assert.Contains("does not match", Assert.Single(state.Blockers, blocker => blocker.Blocker.StartsWith("Unblock Review Failed:", StringComparison.Ordinal)).Blocker, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveInvalidCompletionCertification_corrected_certification_recovers_through_router()
    {
        using var repo = SeedProject();
        const string evaluationPath = ".agents/evidence/evaluations/epic-completion-and-drift.0001.md";
        const string blockerPath = ".agents/evidence/blockers/invalid-completion-certification.0001.md";
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionPaths["EvaluateEpicCompletionAndDrift"], ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift"));
        repo.Write(evaluationPath, CompletionEvaluation("Partially Complete", "None", "Continue Epic"));
        repo.Write(blockerPath, "original invalid certification blocker");
        await SaveStateAsync(repo, BlockedState(
            "ResolveInvalidCompletionCertification",
            Cli.RoadmapState.EvidenceBlocked,
            evaluationPath,
            output: $"{evaluationPath}, {blockerPath}",
            from: Cli.RoadmapState.CompletionEvaluationAndContextUpdate,
            prompt: "CompletionCertificationRouting",
            projection: Cli.RoadmapArtifactPaths.ProjectionPaths["EvaluateEpicCompletionAndDrift"],
            decision: "Invalid Completion Certification",
            extraEvidencePaths: [blockerPath]));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.ExecutionLoop, state.CurrentState);
        Assert.Equal("Continue Epic", state.LastTransition.Decision);
        Assert.Equal("ContinueExecution", state.TransitionIntent.Intent);
        Assert.Contains(evaluationPath, state.TransitionIntent.EvidencePaths);
        Assert.Contains(state.TransitionIntent.EvidencePaths, path => path.StartsWith(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, StringComparison.Ordinal));
        Assert.Empty(state.Blockers);
    }

    [Fact]
    public async Task ResolveInvalidCompletionCertification_contradiction_remains_blocked()
    {
        using var repo = SeedProject();
        const string evaluationPath = ".agents/evidence/evaluations/epic-completion-and-drift.0001.md";
        const string blockerPath = ".agents/evidence/blockers/invalid-completion-certification.0001.md";
        repo.Write(evaluationPath, CompletionEvaluation("Not Complete", "None", "Close Epic"));
        repo.Write(blockerPath, "original invalid certification blocker");
        await SaveStateAsync(repo, BlockedState(
            "ResolveInvalidCompletionCertification",
            Cli.RoadmapState.EvidenceBlocked,
            evaluationPath,
            output: $"{evaluationPath}, {blockerPath}",
            from: Cli.RoadmapState.CompletionEvaluationAndContextUpdate,
            prompt: "CompletionCertificationRouting",
            decision: "Invalid Completion Certification",
            blockers: [new Cli.BlockerRow("Original certification contradiction", "Repair certification")],
            extraEvidencePaths: [blockerPath]));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("ResolveInvalidCompletionCertification", state.TransitionIntent.Intent);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker == "Original certification contradiction");
        Assert.Contains(state.Blockers, blocker => blocker.Blocker.Contains("does not allow completion status `Not Complete`", StringComparison.Ordinal));
        Assert.Contains("Not Complete", repo.Read(evaluationPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepairExecutionRuntimeFailure_valid_readiness_recovers_to_execution_prompt_ready()
    {
        using var repo = SeedProject();
        await SeedExecutionReadyAsync(repo);
        const string evidencePath = ".agents/evidence/execution/execution-turn.0001.md";
        repo.Write(evidencePath, "runtime failure evidence");
        await SaveStateAsync(repo, FailedRuntimeState(evidencePath));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.ExecutionPromptReady, state.CurrentState);
        Assert.Equal("UnblockReview", state.LastTransition.Prompt);
        Assert.Equal("ContinueExecution", state.TransitionIntent.Intent);
        Assert.Contains(Cli.RoadmapArtifactPaths.ExecutionPrompt, state.TransitionIntent.EvidencePaths);
        Assert.Empty(state.Blockers);
    }

    [Fact]
    public async Task RepairExecutionRuntimeFailure_missing_artifacts_remains_failed()
    {
        using var repo = SeedProject();
        const string evidencePath = ".agents/evidence/execution/execution-turn.0001.md";
        repo.Write(evidencePath, "runtime failure evidence");
        await SaveStateAsync(repo, FailedRuntimeState(evidencePath));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.Failed, state.CurrentState);
        Assert.Equal("ExecutionLoop", state.LastTransition.Prompt);
        Assert.Equal("RepairExecutionRuntimeFailure", state.TransitionIntent.Intent);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker.StartsWith("Unblock Review Failed:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RepairExecutionRuntimeFailure_stale_preparation_artifacts_do_not_recover()
    {
        using var repo = SeedProject();
        await SeedExecutionReadyAsync(repo);
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Changed Epic", "EPIC-CHANGED"));
        const string evidencePath = ".agents/evidence/execution/execution-turn.0001.md";
        repo.Write(evidencePath, "runtime failure evidence");
        await SaveStateAsync(repo, FailedRuntimeState(evidencePath));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.Failed, state.CurrentState);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker.Contains("not fresh", StringComparison.Ordinal) || blocker.Blocker.Contains("not safe", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ResolveArtifactPromotionBlocker")]
    [InlineData("ResolveSplitEpicBlocker")]
    [InlineData("ResolveTransitionFailure")]
    [InlineData("ResolveInvariantViolation")]
    public async Task Unsupported_blocker_intents_remain_report_only(string intent)
    {
        using var repo = SeedProject();
        const string evidencePath = ".agents/evidence/blockers/unsupported.0001.md";
        repo.Write(evidencePath, "unsupported blocker evidence");
        await SaveStateAsync(repo, BlockedState(
            intent,
            Cli.RoadmapState.EvidenceBlocked,
            evidencePath,
            blockers: [new Cli.BlockerRow("Original unsupported blocker", "Manual repair")]));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(intent, state.TransitionIntent.Intent);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker == "Original unsupported blocker");
        Assert.Contains(state.Blockers, blocker => blocker.Blocker.StartsWith("Unblock Unsupported:", StringComparison.Ordinal));
        string review = repo.Read(Assert.Single(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "unblock-review.*.md")));
        Assert.Contains("Unblock Unsupported", review, StringComparison.Ordinal);
        Assert.Contains("support", review, StringComparison.OrdinalIgnoreCase);
    }

    private static TempRepo SeedProject()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap source");
        return repo;
    }

    private static async Task SeedExecutionReadyAsync(TempRepo repo)
    {
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Runtime Recovery Epic", "EPIC-RUNTIME"));
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(Cli.RoadmapArtifactPaths.ActiveEpic, Cli.ArtifactLifecycleState.Ready);

        const string specPath = ".agents/specs/runtime-recovery.md";
        repo.Write(specPath, """
            # Runtime Recovery

            | Field | Value |
            |---|---|
            | Epic Path | .agents/epic.md |

            ## Acceptance Criteria

            - [ ] Runtime recovery is ready.
            """);
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(specPath, Cli.ArtifactLifecycleState.Ready);

        Cli.ExecutionPreparationProvenanceService provenance = await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, specPath);
        await ExecutionPreparationTestSupport.SeedOperationalContextAsync(provenance, repo, "# Operational Context");
        await ExecutionPreparationTestSupport.SeedExecutionPromptAsync(provenance, repo, "# Execution Prompt");
        await new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();
    }

    private static Cli.RoadmapStateDocument BlockedState(
        string intent,
        Cli.RoadmapState state,
        string evidencePath,
        string? output = null,
        Cli.RoadmapState? from = null,
        string prompt = "BlockedTransition",
        string projection = "None",
        string decision = "Blocked",
        IReadOnlyList<Cli.BlockerRow>? blockers = null,
        IReadOnlyList<string>? extraEvidencePaths = null)
    {
        IReadOnlyList<string> evidencePaths = extraEvidencePaths is null
            ? [evidencePath]
            : [evidencePath, ..extraEvidencePaths];
        return new Cli.RoadmapStateDocument(
            state,
            [],
            new Cli.RoadmapTransitionSummary(
                from ?? Cli.RoadmapState.CoreReady,
                state,
                prompt,
                projection,
                output ?? evidencePath,
                decision,
                state == Cli.RoadmapState.Failed ? Cli.TransitionStatus.Failed : Cli.TransitionStatus.Paused,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
            blockers ?? [new Cli.BlockerRow("Original blocker", "Repair original blocker")],
            "None",
            0,
            0,
            new Cli.ProjectionManifestCounts(0, 0, 0),
            new Cli.RoadmapTransitionIntent(intent, state, evidencePaths),
            ["unblock"],
            []);
    }

    private static Cli.RoadmapStateDocument FailedRuntimeState(string evidencePath) =>
        BlockedState(
            "RepairExecutionRuntimeFailure",
            Cli.RoadmapState.Failed,
            evidencePath,
            from: Cli.RoadmapState.ExecutionLoop,
            prompt: "ExecutionLoop",
            decision: "Runtime Failure",
            blockers: [new Cli.BlockerRow("Original runtime failure", "Repair runtime failure")]);

    private static Task SaveStateAsync(TempRepo repo, Cli.RoadmapStateDocument state) =>
        new RoadmapStateStore(repo.Artifacts).SaveAsync(state);

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

    private static string CompletionEvaluation(
        string completionStatus,
        string driftClassification,
        string recommendation) => $$"""
        # Epic Completion Evaluation

        ## Evaluation Summary

        | Field | Value |
        |---|---|
        | Overall Completion Status | {{completionStatus}} |
        | Overall Drift Classification | {{driftClassification}} |
        | Closure Recommendation | {{recommendation}} |
        """;
}
