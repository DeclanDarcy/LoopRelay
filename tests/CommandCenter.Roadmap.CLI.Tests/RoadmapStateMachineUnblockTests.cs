using CommandCenter.Agents.Models;
using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class RoadmapStateMachineUnblockTests
{
    [Fact]
    public async Task Status_reports_blocked_state_without_mutation()
    {
        using var repo = new TempRepo();
        await SaveStateAsync(repo, BlockedState("ResolveBlocker", RoadmapState.EvidenceBlocked, ".agents/evidence/blockers/preflight.0001.md"));
        string before = repo.Read(RoadmapArtifactPaths.State);

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).ExecuteAsync(
            RoadmapCliCommand.Status,
            CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(before, repo.Read(RoadmapArtifactPaths.State));
        Assert.Empty(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "unblock-review.*.md"));
    }

    [Fact]
    public async Task Run_reports_blocked_state_without_mutation()
    {
        using var repo = new TempRepo();
        await SaveStateAsync(repo, BlockedState("ResolveBlocker", RoadmapState.EvidenceBlocked, ".agents/evidence/blockers/preflight.0001.md"));
        string before = repo.Read(RoadmapArtifactPaths.State);

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).ExecuteAsync(
            RoadmapCliCommand.Run,
            CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(before, repo.Read(RoadmapArtifactPaths.State));
    }

    [Fact]
    public async Task ResolveBlocker_missing_project_context_preserves_original_blocker_and_appends_review_once()
    {
        using var repo = new TempRepo();
        const string blockerPath = ".agents/evidence/blockers/preflight.0001.md";
        repo.Write(blockerPath, "original preflight blocker");
        await SaveStateAsync(repo, BlockedState(
            "ResolveBlocker",
            RoadmapState.EvidenceBlocked,
            blockerPath,
            prompt: "Preflight",
            blockers: [new BlockerRow("Original Project Context blocker", "Repair Project Context")]));

        RoadmapStateMachine machine = StateMachineFactory.Create(repo, new ScriptedAgentRuntime());
        RoadmapOutcome first = await machine.UnblockAsync(CancellationToken.None);
        RoadmapOutcome second = await machine.UnblockAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, first);
        Assert.Equal(RoadmapOutcome.Paused, second);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("Preflight", state.LastTransition.Prompt);
        Assert.Equal("ResolveBlocker", state.TransitionIntent.Intent);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker == "Original Project Context blocker");
        Assert.Single(state.Blockers, blocker => blocker.Blocker.StartsWith("Unblock Review Failed:", StringComparison.Ordinal));
        IReadOnlyList<string> reviews = await repo.Artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "unblock-review.*.md");
        Assert.Equal(2, reviews.Count);
        Assert.Contains("Original Project Context blocker", repo.Read(reviews.Order(StringComparer.Ordinal).First()), StringComparison.Ordinal);
        Assert.Equal("original preflight blocker", repo.Read(blockerPath));
    }

    [Fact]
    public async Task ResolveBlocker_repaired_project_context_recovers_to_core_ready()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap source");
        const string blockerPath = ".agents/evidence/blockers/preflight.0001.md";
        repo.Write(blockerPath, "original preflight blocker");
        await SaveStateAsync(repo, BlockedState(
            "ResolveBlocker",
            RoadmapState.EvidenceBlocked,
            blockerPath,
            prompt: "Preflight",
            blockers: [new BlockerRow("Original Project Context blocker", "Repair Project Context")]));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.CoreReady, state.CurrentState);
        Assert.Equal("UnblockReview", state.LastTransition.Prompt);
        Assert.Equal("Project Context blocker resolved.", state.LastTransition.Decision);
        Assert.Empty(state.Blockers);
        string review = repo.Read(Assert.Single(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "unblock-review.*.md")));
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
            RoadmapState.EvidenceBlocked,
            evidencePath,
            from: RoadmapState.ExecutionLoop,
            prompt: "ExecutionOutcomeInterpretation",
            decision: "Malformed Execution Output"));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.ExecutionLoop, state.CurrentState);
        Assert.Equal(TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal("Continue Required", state.LastTransition.Decision);
        Assert.Equal("ContinueExecution", state.TransitionIntent.Intent);
        Assert.Contains(evidencePath, state.TransitionIntent.EvidencePaths);
        Assert.Empty(state.Blockers);
        Assert.Equal(evidence, repo.Read(evidencePath));
        Assert.Contains("UnblockReviewCompleted", repo.Read(RoadmapArtifactPaths.TransitionJournal), StringComparison.Ordinal);
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
            RoadmapState.EvidenceBlocked,
            evidencePath,
            from: RoadmapState.ExecutionLoop,
            prompt: "ExecutionOutcomeInterpretation",
            decision: "Malformed Execution Output",
            blockers: [new BlockerRow("Original malformed output", "Repair execution evidence")]));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("ExecutionOutcomeInterpretation", state.LastTransition.Prompt);
        Assert.Equal("ResolveMalformedExecutionOutput", state.TransitionIntent.Intent);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker == "Original malformed output");
        Assert.Contains(state.Blockers, blocker => blocker.Blocker.StartsWith("Unblock Review Failed:", StringComparison.Ordinal));
        Assert.Equal(evidence, repo.Read(evidencePath));
        string review = repo.Read(Assert.Single(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "unblock-review.*.md")));
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
            RoadmapState.EvidenceBlocked,
            evidencePath,
            output: ".agents/evidence/execution/other.md",
            from: RoadmapState.ExecutionLoop,
            prompt: "ExecutionOutcomeInterpretation"));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("ResolveMalformedExecutionOutput", state.TransitionIntent.Intent);
        Assert.Contains("does not match", Assert.Single(state.Blockers, blocker => blocker.Blocker.StartsWith("Unblock Review Failed:", StringComparison.Ordinal)).Blocker, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveInvalidCompletionCertification_corrected_certification_recovers_through_router()
    {
        using var repo = SeedProject();
        const string evaluationPath = ".agents/evidence/evaluations/epic-completion-and-drift.0001.md";
        const string blockerPath = ".agents/evidence/blockers/invalid-completion-certification.0001.md";
        repo.Write(RoadmapArtifactPaths.ProjectionPaths["EvaluateEpicCompletionAndDrift"], ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift"));
        repo.Write(evaluationPath, CompletionEvaluation("Partially Complete", "None", "Continue Epic"));
        repo.Write(blockerPath, "original invalid certification blocker");
        await SaveStateAsync(repo, BlockedState(
            "ResolveInvalidCompletionCertification",
            RoadmapState.EvidenceBlocked,
            evaluationPath,
            output: $"{evaluationPath}, {blockerPath}",
            from: RoadmapState.CompletionEvaluationAndContextUpdate,
            prompt: "CompletionCertificationRouting",
            projection: RoadmapArtifactPaths.ProjectionPaths["EvaluateEpicCompletionAndDrift"],
            decision: "Invalid Completion Certification",
            extraEvidencePaths: [blockerPath]));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.ExecutionLoop, state.CurrentState);
        Assert.Equal("Continue Epic", state.LastTransition.Decision);
        Assert.Equal("ContinueExecution", state.TransitionIntent.Intent);
        Assert.Contains(evaluationPath, state.TransitionIntent.EvidencePaths);
        Assert.Contains(state.TransitionIntent.EvidencePaths, path => path.StartsWith(RoadmapArtifactPaths.BlockerEvidenceDirectory, StringComparison.Ordinal));
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
            RoadmapState.EvidenceBlocked,
            evaluationPath,
            output: $"{evaluationPath}, {blockerPath}",
            from: RoadmapState.CompletionEvaluationAndContextUpdate,
            prompt: "CompletionCertificationRouting",
            decision: "Invalid Completion Certification",
            blockers: [new BlockerRow("Original certification contradiction", "Repair certification")],
            extraEvidencePaths: [blockerPath]));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
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

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.ExecutionPromptReady, state.CurrentState);
        Assert.Equal("UnblockReview", state.LastTransition.Prompt);
        Assert.Equal("ContinueExecution", state.TransitionIntent.Intent);
        Assert.Contains(RoadmapArtifactPaths.ExecutionPrompt, state.TransitionIntent.EvidencePaths);
        Assert.Empty(state.Blockers);
    }

    [Fact]
    public async Task RepairExecutionRuntimeFailure_missing_artifacts_remains_failed()
    {
        using var repo = SeedProject();
        const string evidencePath = ".agents/evidence/execution/execution-turn.0001.md";
        repo.Write(evidencePath, "runtime failure evidence");
        await SaveStateAsync(repo, FailedRuntimeState(evidencePath));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.Failed, state.CurrentState);
        Assert.Equal("ExecutionLoop", state.LastTransition.Prompt);
        Assert.Equal("RepairExecutionRuntimeFailure", state.TransitionIntent.Intent);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker.StartsWith("Unblock Review Failed:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RepairExecutionRuntimeFailure_stale_preparation_artifacts_do_not_recover()
    {
        using var repo = SeedProject();
        await SeedExecutionReadyAsync(repo);
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Changed Epic", "EPIC-CHANGED"));
        const string evidencePath = ".agents/evidence/execution/execution-turn.0001.md";
        repo.Write(evidencePath, "runtime failure evidence");
        await SaveStateAsync(repo, FailedRuntimeState(evidencePath));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.Failed, state.CurrentState);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker.Contains("not fresh", StringComparison.Ordinal) || blocker.Blocker.Contains("not safe", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ResolveArtifactPromotionBlocker")]
    [InlineData("ResolveSplitEpicBlocker")]
    [InlineData("ResolveTransitionFailure")]
    public async Task Unsupported_blocker_intents_remain_report_only(string intent)
    {
        using var repo = SeedProject();
        const string evidencePath = ".agents/evidence/blockers/unsupported.0001.md";
        repo.Write(evidencePath, "unsupported blocker evidence");
        await SaveStateAsync(repo, BlockedState(
            intent,
            RoadmapState.EvidenceBlocked,
            evidencePath,
            blockers: [new BlockerRow("Original unsupported blocker", "Manual repair")]));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).UnblockAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(intent, state.TransitionIntent.Intent);
        Assert.Contains(state.Blockers, blocker => blocker.Blocker == "Original unsupported blocker");
        Assert.Contains(state.Blockers, blocker => blocker.Blocker.StartsWith("Unblock Unsupported:", StringComparison.Ordinal));
        string review = repo.Read(Assert.Single(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "unblock-review.*.md")));
        Assert.Contains("Unblock Unsupported", review, StringComparison.Ordinal);
        Assert.Contains("support", review, StringComparison.OrdinalIgnoreCase);
    }

    private static TempRepo SeedProject()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap source");
        return repo;
    }

    private static async Task SeedExecutionReadyAsync(TempRepo repo)
    {
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Runtime Recovery Epic", "EPIC-RUNTIME"));
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Ready);

        const string specPath = ".agents/specs/runtime-recovery.md";
        repo.Write(specPath, """
            # Runtime Recovery

            | Field | Value |
            |---|---|
            | Epic Path | .agents/epic.md |

            ## Acceptance Criteria

            - [ ] Runtime recovery is ready.
            """);
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(specPath, ArtifactLifecycleState.Ready);

        ExecutionPreparationProvenanceService provenance = await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, specPath);
        await ExecutionPreparationTestSupport.SeedOperationalContextAsync(provenance, repo, "# Operational Context");
        await ExecutionPreparationTestSupport.SeedExecutionPromptAsync(provenance, repo, "# Execution Prompt");
        await new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();
    }

    private static RoadmapStateDocument BlockedState(
        string intent,
        RoadmapState state,
        string evidencePath,
        string? output = null,
        RoadmapState? from = null,
        string prompt = "BlockedTransition",
        string projection = "None",
        string decision = "Blocked",
        IReadOnlyList<BlockerRow>? blockers = null,
        IReadOnlyList<string>? extraEvidencePaths = null)
    {
        IReadOnlyList<string> evidencePaths = extraEvidencePaths is null
            ? [evidencePath]
            : [evidencePath, ..extraEvidencePaths];
        return new RoadmapStateDocument(
            state,
            [],
            new RoadmapTransitionSummary(
                from ?? RoadmapState.CoreReady,
                state,
                prompt,
                projection,
                output ?? evidencePath,
                decision,
                state == RoadmapState.Failed ? TransitionStatus.Failed : TransitionStatus.Paused,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
            blockers ?? [new BlockerRow("Original blocker", "Repair original blocker")],
            "None",
            0,
            0,
            new ProjectionManifestCounts(0, 0, 0),
            new RoadmapTransitionIntent(intent, state, evidencePaths),
            ["unblock"],
            []);
    }

    private static RoadmapStateDocument FailedRuntimeState(string evidencePath) =>
        BlockedState(
            "RepairExecutionRuntimeFailure",
            RoadmapState.Failed,
            evidencePath,
            from: RoadmapState.ExecutionLoop,
            prompt: "ExecutionLoop",
            decision: "Runtime Failure",
            blockers: [new BlockerRow("Original runtime failure", "Repair runtime failure")]);

    private static Task SaveStateAsync(TempRepo repo, RoadmapStateDocument state) =>
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
