using System.Text.Json;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Roadmap.Cli;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapFailurePersistenceTests
{
    [Theory]
    [InlineData("CreateRoadmapCompletionContext", "CoreReady", "RoadmapCompletionContextReady", Cli.RoadmapArtifactPaths.RoadmapCompletionContext)]
    [InlineData("SelectNextEpic", "RoadmapCompletionContextReady", "SelectNextStrategicInitiative", Cli.RoadmapArtifactPaths.Selection)]
    [InlineData("EpicPreparationAudit", "ExistingEpicSelected", "EpicPreparationAudit", Cli.RoadmapArtifactPaths.AuditEvidenceDirectory)]
    [InlineData("CreateNewEpic", "NewEpicProposed", "ActiveEpicReady", Cli.RoadmapArtifactPaths.ActiveEpic)]
    [InlineData("RealignEpic", "RealignEpic", "ActiveEpicReady", Cli.RoadmapArtifactPaths.ActiveEpic)]
    [InlineData("ReimagineEpic", "ReimagineEpic", "ActiveEpicReady", Cli.RoadmapArtifactPaths.ActiveEpic)]
    [InlineData("SplitEpic", "SplitEpicProposed", "SplitChildSelection", Cli.RoadmapArtifactPaths.SplitFamiliesDirectory)]
    [InlineData("GenerateMilestoneDeepDivesForEpic", "ActiveEpicReady", "MilestoneSpecsReady", Cli.RoadmapArtifactPaths.SpecsDirectory)]
    [InlineData("EvaluateEpicCompletionAndDrift", "EpicCompletionDetected", "CompletionEvaluationAndContextUpdate", Cli.RoadmapArtifactPaths.EvaluationEvidenceDirectory)]
    [InlineData("UpdateRoadmapCompletionContext", "CompletionEvaluationAndContextUpdate", "SelectNextStrategicInitiative", Cli.RoadmapArtifactPaths.RoadmapCompletionContext)]
    public async Task Prompt_transition_failures_are_owned_by_the_transition_layer(
        string prompt,
        string expectedFrom,
        string expectedTo,
        string expectedOutput)
    {
        Cli.RoadmapState expectedFromState = Enum.Parse<Cli.RoadmapState>(expectedFrom);
        Cli.RoadmapState expectedToState = Enum.Parse<Cli.RoadmapState>(expectedTo);
        using var repo = SeedRepo(includeCompletionContext: prompt != "CreateRoadmapCompletionContext");
        var runtime = new ScriptedAgentRuntime(BuildPromptFailureTurns(prompt).ToArray());

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Failed, state.LastTransition.Status);
        Assert.Equal(expectedFromState, state.LastTransition.From);
        Assert.Equal(expectedToState, state.LastTransition.To);
        Assert.Equal(prompt, state.LastTransition.Prompt);
        Assert.Equal(Cli.RoadmapArtifactPaths.ProjectionPaths[prompt], state.LastTransition.Projection);
        Assert.Equal(expectedOutput, state.LastTransition.Output);
        Assert.Equal("ResolveTransitionFailure", state.TransitionIntent.Intent);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.TransitionIntent.DispatchState);
        Assert.Equal([expectedOutput], state.TransitionIntent.EvidencePaths);

        string stateJson = repo.Read(Cli.RoadmapArtifactPaths.StateJson);
        Assert.DoesNotContain("RoadmapStateMachine", stateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveBlocker", stateJson, StringComparison.Ordinal);
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "roadmap-transition-blocked-*.md"));

        Cli.TransitionJournalRecord failed = ReadJournal(repo).Single(record =>
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

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal(Cli.RoadmapState.NewEpicProposed, state.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, state.LastTransition.To);
        Assert.Equal("CreateNewEpic", state.LastTransition.Prompt);
        Assert.Equal(Cli.RoadmapArtifactPaths.ProjectionPaths["CreateNewEpic"], state.LastTransition.Projection);
        Assert.Equal(evidencePath, state.LastTransition.Output);
        Assert.Equal("ResolveArtifactPromotionBlocker", state.TransitionIntent.Intent);
        Assert.Equal(blocked, repo.Read(evidencePath));
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "roadmap-transition-blocked-*.md"));

        Cli.TransitionJournalRecord promotionBlocked = ReadJournal(repo).Single(record => record.Event == "ArtifactPromotionBlocked");
        Assert.Equal(state.LastTransition.From, promotionBlocked.PreviousState);
        Assert.Equal(state.LastTransition.To, promotionBlocked.AttemptedState);
        Assert.Equal(state.LastTransition.Prompt, promotionBlocked.Prompt);
        Assert.Equal(state.LastTransition.Projection, promotionBlocked.Projection);
        Assert.Equal([evidencePath], promotionBlocked.OutputPaths);
    }

    [Fact]
    public async Task Milestone_output_without_specs_persists_blocker_instead_of_ready_state()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        string invalidOutput = """
            # Milestone Deep Dive Notes

            This output contains analysis, but it does not contain any FILE markers.
            """;
        var runtime = new ScriptedAgentRuntime(BuildMilestoneInvariantTurns(invalidOutput).ToArray());

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.NotEqual(Cli.RoadmapState.MilestoneSpecsReady, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, state.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.MilestoneSpecsReady, state.LastTransition.To);
        Assert.Equal("GenerateMilestoneDeepDivesForEpic", state.LastTransition.Prompt);
        Assert.Equal(Cli.RoadmapArtifactPaths.ProjectionPaths["GenerateMilestoneDeepDivesForEpic"], state.LastTransition.Projection);
        Assert.Equal(evidencePath, state.LastTransition.Output);
        Assert.Equal("Milestone Spec Generation Failed", state.LastTransition.Decision);
        Assert.Equal("ResolveMilestoneSpecGenerationFailure", state.TransitionIntent.Intent);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.TransitionIntent.DispatchState);
        Assert.Contains("No FILE markers", Assert.Single(state.Blockers).Blocker, StringComparison.Ordinal);
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.SpecsDirectory, "*.md"));
        Assert.Equal(Cli.ArtifactStatus.Missing, await repo.Artifacts.GetStatusAsync($"{Cli.RoadmapArtifactPaths.SpecsDirectory}/bundle-manifest.md"));
        Assert.Equal(Cli.ArtifactStatus.Missing, await repo.Artifacts.GetStatusAsync(Cli.RoadmapArtifactPaths.ExecutionPreparationManifest));

        string evidence = repo.Read(evidencePath);
        Assert.Contains("Milestone Spec Generation Failed", evidence, StringComparison.Ordinal);
        Assert.Contains(invalidOutput.Trim(), evidence, StringComparison.Ordinal);

        Cli.TransitionJournalRecord[] journal = ReadJournal(repo);
        Assert.Contains(journal, record =>
            record.Event == "PromptCompleted" &&
            record.Prompt == "GenerateMilestoneDeepDivesForEpic");
        Assert.Contains(journal, record =>
            record.Event == "MilestoneSpecGenerationFailed" &&
            record.OutputPaths.SequenceEqual([evidencePath]));
        Assert.DoesNotContain(journal, record =>
            record.Event == "TransitionCompleted" &&
            record.Prompt == "GenerateMilestoneDeepDivesForEpic");
    }

    [Fact]
    public async Task Invariant_failure_preserves_validator_evidence_state_and_journal()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        var runtime = new ScriptedAgentRuntime(BuildMilestoneInvariantTurns(MismatchedMilestoneBundle()).ToArray());

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, state.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.MilestoneSpecsReady, state.LastTransition.To);
        Assert.Equal("PostMilestoneInvariantValidation", state.LastTransition.Prompt);
        Assert.Equal(Cli.RoadmapArtifactPaths.ProjectionPaths["GenerateMilestoneDeepDivesForEpic"], state.LastTransition.Projection);
        Assert.Equal(evidencePath, state.LastTransition.Output);
        Assert.Equal("Invariant Failed: SpecEpicMismatch", state.LastTransition.Decision);
        Assert.Equal("ResolveInvariantViolation", state.TransitionIntent.Intent);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.TransitionIntent.DispatchState);
        Assert.StartsWith(Cli.RoadmapArtifactPaths.OrchestrationEvidenceDirectory, evidencePath, StringComparison.Ordinal);
        Assert.Contains("SpecEpicMismatch", repo.Read(evidencePath), StringComparison.Ordinal);
        Assert.Contains(evidencePath, Assert.Single(state.Blockers).RequiredNextStep, StringComparison.Ordinal);

        string stateJson = repo.Read(Cli.RoadmapArtifactPaths.StateJson);
        Assert.Contains(evidencePath, stateJson, StringComparison.Ordinal);
        Assert.Contains("ResolveInvariantViolation", stateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveBlocker", stateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadmapStateMachine", stateJson, StringComparison.Ordinal);
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "roadmap-transition-blocked-*.md"));

        Cli.TransitionJournalRecord[] journal = ReadJournal(repo);
        Assert.Contains(journal, record =>
            record.Event == "PromptCompleted" &&
            record.Prompt == "GenerateMilestoneDeepDivesForEpic");
        Assert.DoesNotContain(journal, record =>
            record.Event == "TransitionCompleted" &&
            record.Prompt == "GenerateMilestoneDeepDivesForEpic");

        Cli.TransitionJournalRecord invariantFailed = journal.Single(record => record.Event == "InvariantFailed");
        Assert.Equal(state.LastTransition.From, invariantFailed.PreviousState);
        Assert.Equal(state.LastTransition.To, invariantFailed.AttemptedState);
        Assert.Equal(state.LastTransition.Prompt, invariantFailed.Prompt);
        Assert.Equal(state.LastTransition.Projection, invariantFailed.Projection);
        Assert.Equal([evidencePath], invariantFailed.OutputPaths);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked.ToString(), invariantFailed.Result);
        Assert.Equal("SpecEpicMismatch", invariantFailed.ParserDecision);
        Assert.Contains(".agents/other-epic.md", invariantFailed.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failed_invariant_persistence_does_not_downgrade_to_evidence_blocked()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(".agents/epic-1.md", Cli.ArtifactLifecycleState.Ready);
        var runtime = new ScriptedAgentRuntime(BuildMilestoneInvariantTurns(MilestoneBundle()).ToArray());

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal(Cli.RoadmapState.Failed, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Failed, state.LastTransition.Status);
        Assert.Equal("Invariant Failed: DuplicateActiveEpic", state.LastTransition.Decision);
        Assert.Equal("ResolveInvariantViolation", state.TransitionIntent.Intent);
        Assert.Equal(Cli.RoadmapState.Failed, state.TransitionIntent.DispatchState);
        Assert.StartsWith(Cli.RoadmapArtifactPaths.OrchestrationEvidenceDirectory, evidencePath, StringComparison.Ordinal);
        Assert.Contains("DuplicateActiveEpic", repo.Read(evidencePath), StringComparison.Ordinal);
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "roadmap-transition-blocked-*.md"));

        Cli.TransitionJournalRecord invariantFailed = ReadJournal(repo).Single(record => record.Event == "InvariantFailed");
        Assert.Equal(Cli.RoadmapState.Failed.ToString(), invariantFailed.Result);
        Assert.Equal("DuplicateActiveEpic", invariantFailed.ParserDecision);
        Assert.Equal([evidencePath], invariantFailed.OutputPaths);
    }

    [Fact]
    public async Task Invariant_failure_without_validator_evidence_uses_specific_fallback_not_generic_blocker()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        Cli.RoadmapStateMachine machine = StateMachineFactory.Create(repo, new ScriptedAgentRuntime());
        Cli.InvariantValidationResult invariant = Cli.InvariantValidationResult.Invalid(
            Cli.RoadmapState.EvidenceBlocked,
            "Validator failed without evidence.",
            string.Empty,
            "MissingValidatorEvidence",
            "Restore validator diagnostics before continuing.");

        Cli.RoadmapStepException exception = await Assert.ThrowsAsync<Cli.RoadmapStepException>(() =>
            machine.PersistInvariantFailureAndThrowAsync(
                invariant,
                Cli.RoadmapState.ExecutionPromptReady,
                Cli.RoadmapState.ExecutionLoop,
                "PreExecutionInvariantValidation",
                "None"));

        Assert.Equal(Cli.RoadmapFailurePersistence.AlreadyPersisted, exception.Persistence);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string fallbackPath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("ResolveInvariantViolation", state.TransitionIntent.Intent);
        Assert.Equal(fallbackPath, state.LastTransition.Output);
        Assert.StartsWith(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, fallbackPath, StringComparison.Ordinal);
        Assert.Contains("without returning an evidence path", repo.Read(fallbackPath), StringComparison.Ordinal);
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "roadmap-transition-blocked-*.md"));

        Cli.TransitionJournalRecord invariantFailed = ReadJournal(repo).Single(record => record.Event == "InvariantFailed");
        Assert.Equal([fallbackPath], invariantFailed.OutputPaths);
        Assert.Equal("MissingValidatorEvidence", invariantFailed.ParserDecision);
    }

    [Fact]
    public async Task Preflight_failure_does_not_persist_ephemeral_blocker()
    {
        using var repo = new TempRepo();
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, new ScriptedAgentRuntime()).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.PreflightBlocked, outcome);
        Assert.Null(await new RoadmapStateStore(repo.Artifacts).LoadAsync());
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "*.md"));
    }

    [Fact]
    public async Task Unexpected_runtime_exception_preserves_last_durable_state()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        var runtime = new ThrowingAgentRuntime(new InvalidOperationException("agent transport unavailable"));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.CoreReady, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Completed, state.LastTransition.Status);
        Assert.Equal("Preflight", state.LastTransition.Prompt);
        Assert.Equal("CoreReady", state.LastTransition.Decision);
        Assert.Empty(state.Blockers);
        Assert.Equal("None", state.TransitionIntent.Intent);
        Assert.Equal(Cli.RoadmapState.CoreReady, state.TransitionIntent.DispatchState);
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "*.md"));
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

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(
            repo,
            runtime,
            new FailingExecutionBridge("execution bridge unavailable")).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.Failed, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Failed, state.LastTransition.Status);
        Assert.Equal(Cli.RoadmapState.ExecutionLoop, state.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.Failed, state.LastTransition.To);
        Assert.Equal("ExecutionLoop", state.LastTransition.Prompt);
        Assert.Equal("None", state.LastTransition.Projection);
        Assert.Equal("Runtime Failure", state.LastTransition.Decision);
        Assert.StartsWith(Cli.RoadmapArtifactPaths.ExecutionEvidenceDirectory, state.LastTransition.Output, StringComparison.Ordinal);
        Assert.Equal("RepairExecutionRuntimeFailure", state.TransitionIntent.Intent);
        Assert.Equal(Cli.RoadmapState.Failed, state.TransitionIntent.DispatchState);
        Assert.Equal([state.LastTransition.Output], state.TransitionIntent.EvidencePaths);
        Assert.Contains("execution bridge unavailable", Assert.Single(state.Blockers).Blocker, StringComparison.Ordinal);
        Assert.Contains("Runtime Failure", repo.Read(state.LastTransition.Output), StringComparison.Ordinal);
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "roadmap-transition-blocked-*.md"));
    }

    private static TempRepo SeedRepo(bool includeCompletionContext)
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        if (includeCompletionContext)
        {
            repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
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

    private static IEnumerable<AgentTurnResult> BuildMilestoneInvariantTurns(string milestoneBundle)
    {
        yield return ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic"));
        yield return ScriptedAgentRuntime.Completed(NewEpicSelection());
        yield return ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic"));
        yield return ScriptedAgentRuntime.Completed(RoadmapSamples.ValidEpic("Invariant Persistence Epic", "EPIC-INVARIANT"));
        yield return ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic"));
        yield return ScriptedAgentRuntime.Completed(milestoneBundle);
    }

    private static Cli.TransitionJournalRecord[] ReadJournal(TempRepo repo) =>
        repo.Read(Cli.RoadmapArtifactPaths.TransitionJournal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<Cli.TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .ToArray();

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

    private static string MismatchedMilestoneBundle() => """
        # FILE: .agents/specs/invariant-mismatch-test.md
        # Invariant Mismatch Test Milestone

        | Field | Value |
        |---|---|
        | Epic Path | .agents/other-epic.md |

        ## Acceptance Criteria

        - [ ] Validator evidence remains authoritative.
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

    private sealed class FailingExecutionBridge(string message) : Cli.IRoadmapExecutionBridge
    {
        public Task<Cli.RoadmapExecutionTransportResult> RunAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Cli.RoadmapExecutionTransportResult.Failed("Failed", message));
    }
}
