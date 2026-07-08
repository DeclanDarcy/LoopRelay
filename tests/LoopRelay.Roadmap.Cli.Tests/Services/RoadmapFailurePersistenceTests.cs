using System.Text.Json;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Roadmap.Cli;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapFailurePersistenceTests
{
    [Theory]
    [InlineData("CreateRoadmapCompletionContext", "CoreReady", "RoadmapCompletionContextReady", Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "Failed")]
    [InlineData("SelectNextEpic", "RoadmapCompletionContextReady", "SelectNextStrategicInitiative", Cli.RoadmapArtifactPaths.Selection, "Failed")]
    [InlineData("EpicPreparationAudit", "ExistingEpicSelected", "EpicPreparationAudit", Cli.RoadmapArtifactPaths.AuditEvidenceDirectory, "Failed")]
    [InlineData("CreateNewEpic", "NewEpicProposed", "ActiveEpicReady", Cli.RoadmapArtifactPaths.ActiveEpic, "Runtime Failure")]
    [InlineData("RealignEpic", "RealignEpic", "ActiveEpicReady", Cli.RoadmapArtifactPaths.ActiveEpic, "Runtime Failure")]
    [InlineData("ReimagineEpic", "ReimagineEpic", "ActiveEpicReady", Cli.RoadmapArtifactPaths.ActiveEpic, "Runtime Failure")]
    [InlineData("SplitEpic", "SplitEpicProposed", "SplitChildSelection", Cli.RoadmapArtifactPaths.SplitFamiliesDirectory, "Failed")]
    [InlineData("GenerateMilestoneDeepDivesForEpic", "ActiveEpicReady", "MilestoneSpecsReady", Cli.RoadmapArtifactPaths.SpecsDirectory, "Runtime Failure")]
    public async Task Prompt_transition_failures_are_owned_by_the_transition_layer(
        string prompt,
        string expectedFrom,
        string expectedTo,
        string expectedOutput,
        string expectedDecision)
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
        Assert.Equal(expectedDecision, state.LastTransition.Decision);
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
    public async Task Milestone_post_prompt_bundle_write_failure_persists_blocker_without_rolling_back_written_specs()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        const string retainedSpecPath = ".agents/specs/retained-before-failure.md";
        const string collidingSpecPath = ".agents/specs/write-collision.md";
        Directory.CreateDirectory(Path.Combine(
            repo.Root,
            collidingSpecPath.Replace('/', Path.DirectorySeparatorChar)));
        string milestoneBundle = $$"""
            # FILE: {{retainedSpecPath}}
            # Retained Before Failure

            | Field | Value |
            |---|---|
            | Epic Path | .agents/epic.md |

            ## Acceptance Criteria

            - [ ] This spec should remain materialized after the later write fails.

            # FILE: {{collidingSpecPath}}
            # Write Collision

            | Field | Value |
            |---|---|
            | Epic Path | .agents/epic.md |

            ## Acceptance Criteria

            - [ ] This spec collides with an existing directory.
            """;
        var runtime = new ScriptedAgentRuntime(BuildMilestoneInvariantTurns(milestoneBundle).ToArray());

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal($"{Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory}/milestone-spec-generation-failed.0001.md", evidencePath);
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

        Assert.Equal(Cli.ArtifactStatus.Present, await repo.Artifacts.GetStatusAsync(retainedSpecPath));
        Assert.Contains("# Retained Before Failure", repo.Read(retainedSpecPath), StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(
            repo.Root,
            collidingSpecPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Equal(Cli.ArtifactStatus.Missing, await repo.Artifacts.GetStatusAsync($"{Cli.RoadmapArtifactPaths.SpecsDirectory}/bundle-manifest.md"));
        Assert.Equal(Cli.ArtifactStatus.Missing, await repo.Artifacts.GetStatusAsync(Cli.RoadmapArtifactPaths.ExecutionPreparationManifest));
        Assert.DoesNotContain(
            await new Cli.ArtifactLifecycleStore(repo.Artifacts).LoadAsync(),
            entry => entry.Path == retainedSpecPath);

        string evidence = repo.Read(evidencePath);
        Assert.Contains("Milestone Spec Generation Failed", evidence, StringComparison.Ordinal);
        Assert.Contains("Retained Before Failure", evidence, StringComparison.Ordinal);
        Assert.Contains(collidingSpecPath, evidence, StringComparison.Ordinal);

        Cli.TransitionJournalRecord[] journal = ReadJournal(repo);
        int promptCompletedIndex = Array.FindIndex(journal, record =>
            record.Event == "PromptCompleted" &&
            record.Prompt == "GenerateMilestoneDeepDivesForEpic");
        int failedIndex = Array.FindIndex(journal, record =>
            record.Event == "MilestoneSpecGenerationFailed" &&
            record.Prompt == "GenerateMilestoneDeepDivesForEpic");
        Assert.NotEqual(-1, promptCompletedIndex);
        Assert.True(failedIndex > promptCompletedIndex);

        Cli.TransitionJournalRecord promptCompleted = journal[promptCompletedIndex];
        Cli.TransitionJournalRecord failed = journal[failedIndex];
        Assert.Equal(promptCompleted.CorrelationId, failed.CorrelationId);
        Assert.Equal("MilestoneSpecPostProcessing", failed.PromptContractKey);
        Assert.Equal([evidencePath], failed.OutputPaths);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, failed.PreviousState);
        Assert.Equal(Cli.RoadmapState.MilestoneSpecsReady, failed.AttemptedState);
        Assert.DoesNotContain(journal, record =>
            record.Event == "MilestoneSpecsMaterialized" &&
            record.Prompt == "GenerateMilestoneDeepDivesForEpic");
        Assert.DoesNotContain(journal, record =>
            record.Event == "TransitionCompleted" &&
            record.Prompt == "GenerateMilestoneDeepDivesForEpic");
    }

    [Fact]
    public async Task Completion_evaluation_parse_failure_preserves_written_evidence_without_invalid_certification_blocker()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        const string specPath = ".agents/specs/completion-parse-failure.md";
        const string executionEvidencePath = ".agents/evidence/execution/execution-result.0001.md";
        const string invalidEvaluation = """
            # Epic Completion Evaluation

            ## Evaluation Summary

            This evaluation is malformed because it has no field/value table.
            """;
        await SeedCompletionClaimAsync(repo, specPath, executionEvidencePath);
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift")),
            ScriptedAgentRuntime.Completed(invalidEvaluation));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        const string evaluationPath = ".agents/evidence/evaluations/epic-completion-and-drift.0001.md";
        Assert.Equal(invalidEvaluation, repo.Read(evaluationPath));
        Assert.False(await repo.Artifacts.ExistsAsync(Cli.RoadmapArtifactPaths.DecisionLedgerJson));
        Assert.Equal("None", await new Cli.DecisionLedgerStore(repo.Artifacts).LastDecisionIdAsync());
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "invalid-completion-certification.*.md"));
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "roadmap-transition-blocked-*.md"));

        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.CompletionEvaluationAndContextUpdate, state.CurrentState);
        Assert.NotEqual(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Completed, state.LastTransition.Status);
        Assert.Equal(Cli.RoadmapState.EpicCompletionDetected, state.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.CompletionEvaluationAndContextUpdate, state.LastTransition.To);
        Assert.Equal("EvaluateEpicCompletionAndDrift", state.LastTransition.Prompt);
        Assert.Equal(Cli.RoadmapArtifactPaths.ProjectionPaths["EvaluateEpicCompletionAndDrift"], state.LastTransition.Projection);
        Assert.Equal(Cli.RoadmapArtifactPaths.EvaluationEvidenceDirectory, state.LastTransition.Output);
        Assert.Equal("Completed", state.LastTransition.Decision);
        Assert.Empty(state.Blockers);
        Assert.NotEqual("ResolveInvalidCompletionCertification", state.TransitionIntent.Intent);
        Assert.Equal([executionEvidencePath], state.TransitionIntent.EvidencePaths);

        Cli.TransitionJournalRecord[] journal = ReadJournal(repo);
        Cli.TransitionJournalRecord completed = journal.Single(record =>
            record.Event == "TransitionCompleted" &&
            record.Prompt == "EvaluateEpicCompletionAndDrift");
        Assert.Equal([Cli.RoadmapArtifactPaths.EvaluationEvidenceDirectory], completed.OutputPaths);
        Assert.DoesNotContain(journal, record =>
            record.Event == "TransitionFailed" &&
            record.Prompt == "EvaluateEpicCompletionAndDrift");
        Assert.DoesNotContain(journal, record => record.Event == "CompletionCertificationRejected");
        Assert.DoesNotContain(journal, record =>
            record.Prompt == "CompletionCertificationRouting" &&
            record.Event == "TransitionCompleted");
    }

    [Fact]
    public async Task Invalid_parsed_completion_certification_writes_rejection_journal_and_recovery_intent()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        const string specPath = ".agents/specs/invalid-completion-certification.md";
        const string executionEvidencePath = ".agents/evidence/execution/execution-result.0001.md";
        string evaluation = CompletionEvaluation("Not Complete", "None", "Close Epic");
        await SeedCompletionClaimAsync(repo, specPath, executionEvidencePath);
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift")),
            ScriptedAgentRuntime.Completed(evaluation));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        const string evaluationPath = ".agents/evidence/evaluations/epic-completion-and-drift.0001.md";
        const string blockerPath = ".agents/evidence/blockers/invalid-completion-certification.0001.md";
        string projectionPath = Cli.RoadmapArtifactPaths.ProjectionPaths["EvaluateEpicCompletionAndDrift"];
        Assert.Equal(evaluation, repo.Read(evaluationPath));
        string blocker = repo.Read(blockerPath);
        Assert.Contains("Completion certification was parsed successfully", blocker, StringComparison.Ordinal);
        Assert.Contains("does not allow completion status `Not Complete`", blocker, StringComparison.Ordinal);
        Assert.Contains(evaluationPath, blocker, StringComparison.Ordinal);
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "roadmap-transition-blocked-*.md"));

        var ledgerJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ledgerJsonOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        Cli.DecisionLedgerPersistenceDocument ledger = JsonSerializer.Deserialize<Cli.DecisionLedgerPersistenceDocument>(
            repo.Read(Cli.RoadmapArtifactPaths.DecisionLedgerJson),
            ledgerJsonOptions)!;
        Cli.DecisionLedgerEntry ledgerDecision = Assert.Single(ledger.ToDomain());
        Assert.Equal(Cli.RoadmapState.CompletionEvaluationAndContextUpdate, ledgerDecision.State);
        Assert.Equal("EvaluateEpicCompletionAndDrift", ledgerDecision.Transition);
        Assert.Equal(projectionPath, ledgerDecision.ProjectionPath);
        Assert.Equal([evaluationPath], ledgerDecision.OutputArtifactPaths);
        Assert.Equal("Close Epic", ledgerDecision.Decision);
        Assert.Equal("Unclear", ledgerDecision.Confidence);
        Assert.Equal("Not Complete", ledgerDecision.RationaleExcerpt);
        Assert.Equal("D0001", await new Cli.DecisionLedgerStore(repo.Artifacts).LastDecisionIdAsync());

        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("D0001", state.LastDecisionId);
        Assert.Equal(Cli.TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal(Cli.RoadmapState.CompletionEvaluationAndContextUpdate, state.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.LastTransition.To);
        Assert.Equal("CompletionCertificationRouting", state.LastTransition.Prompt);
        Assert.Equal(projectionPath, state.LastTransition.Projection);
        Assert.Equal($"{evaluationPath}, {blockerPath}", state.LastTransition.Output);
        Assert.Equal("Invalid Completion Certification", state.LastTransition.Decision);
        Cli.BlockerRow persistedBlocker = Assert.Single(state.Blockers);
        Assert.Contains("does not allow completion status `Not Complete`", persistedBlocker.Blocker, StringComparison.Ordinal);
        Assert.Equal("ResolveInvalidCompletionCertification", state.TransitionIntent.Intent);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.TransitionIntent.DispatchState);
        Assert.Equal([evaluationPath, blockerPath], state.TransitionIntent.EvidencePaths);
        Assert.Equal(["Resolve invalid completion certification and rerun"], state.NextValidTransitions);

        Cli.TransitionJournalRecord[] journal = ReadJournal(repo);
        Cli.TransitionJournalRecord completed = journal.Single(record =>
            record.Event == "TransitionCompleted" &&
            record.Prompt == "EvaluateEpicCompletionAndDrift");
        Assert.Equal([Cli.RoadmapArtifactPaths.EvaluationEvidenceDirectory], completed.OutputPaths);
        Cli.TransitionJournalRecord rejected = journal.Single(record =>
            record.Event == "CompletionCertificationRejected" &&
            record.Prompt == "CompletionCertificationRouting");
        Assert.Equal(Cli.RoadmapState.CompletionEvaluationAndContextUpdate, rejected.PreviousState);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, rejected.AttemptedState);
        Assert.Equal(projectionPath, rejected.Projection);
        Assert.Equal("CompletionCertificationPolicy", rejected.PromptContractKey);
        Assert.Equal([evaluationPath, blockerPath], rejected.OutputPaths);
        Assert.Equal("Paused", rejected.Result);
        Assert.Equal("Invalid Completion Certification", rejected.ParserDecision);
        Assert.Contains("does not allow completion status `Not Complete`", rejected.ErrorMessage, StringComparison.Ordinal);
        Assert.NotNull(rejected.InputSnapshot);
        Assert.DoesNotContain(journal, record =>
            record.Event == "TransitionFailed" &&
            record.Prompt == "EvaluateEpicCompletionAndDrift");
        Assert.DoesNotContain(journal, record =>
            record.Prompt == "CompletionCertificationRouting" &&
            record.Event == "TransitionCompleted");
    }

    [Fact]
    public async Task Close_route_updates_completion_context_supersedes_selection_and_excludes_update_evidence_from_route_outputs()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        const string specPath = ".agents/specs/close-route-certification.md";
        const string executionEvidencePath = ".agents/evidence/execution/execution-result.0001.md";
        const string updatedCompletionContext = """
            # Updated Roadmap Completion Context

            Closed epic synthesis has been folded into the roadmap context.
            """;
        string evaluation = CompletionEvaluation("Fully Complete", "None", "Close Epic");
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, ExistingEpicSelection());
        await SeedCompletionClaimAsync(repo, specPath, executionEvidencePath);
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift")),
            ScriptedAgentRuntime.Completed(evaluation),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("UpdateRoadmapCompletionContext")),
            ScriptedAgentRuntime.Completed(updatedCompletionContext));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Completed, outcome);
        const string evaluationPath = ".agents/evidence/evaluations/epic-completion-and-drift.0001.md";
        const string updateEvidencePath = ".agents/evidence/evaluations/roadmap-completion-update.0001.md";
        const string archiveSynthesisPath = ".agents/archive/epics/1.md";
        string projectionPath = Cli.RoadmapArtifactPaths.ProjectionPaths["EvaluateEpicCompletionAndDrift"];
        Assert.Equal(evaluation, repo.Read(evaluationPath));
        Assert.Equal(updatedCompletionContext, repo.Read(Cli.RoadmapArtifactPaths.RoadmapCompletionContext));
        Assert.Equal(updatedCompletionContext, repo.Read(updateEvidencePath));

        Cli.SelectionProvenanceManifest selectionManifest =
            await new Cli.SelectionProvenanceManifestStore(repo.Artifacts).LoadAsync();
        Cli.DerivedArtifactManifestEntry selection = Assert.Single(selectionManifest.Selections);
        Assert.Equal(Cli.DerivedArtifactProvenanceStatus.Superseded, selection.ProvenanceStatus);
        Assert.Equal(Cli.DerivedArtifactFreshnessStatus.Stale, selection.FreshnessStatus);
        Assert.Contains(Cli.DerivedArtifactStaleReason.RoadmapCompletionContextDrift, selection.FreshnessReasons);
        Cli.ArtifactLifecycleEntry selectionLifecycle = Assert.Single(
            await new Cli.ArtifactLifecycleStore(repo.Artifacts).LoadAsync(),
            entry => entry.Path == Cli.RoadmapArtifactPaths.Selection);
        Assert.Equal(Cli.ArtifactLifecycleState.Superseded, selectionLifecycle.State);
        Assert.Equal("Roadmap completion context changed after completion certification.", selectionLifecycle.Notes);

        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.SelectNextStrategicInitiative, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Completed, state.LastTransition.Status);
        Assert.Equal(Cli.RoadmapState.CompletionEvaluationAndContextUpdate, state.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.SelectNextStrategicInitiative, state.LastTransition.To);
        Assert.Equal("CompletionCertificationRouting", state.LastTransition.Prompt);
        Assert.Equal(projectionPath, state.LastTransition.Projection);
        Assert.Equal(
            $"{evaluationPath}, {Cli.RoadmapArtifactPaths.RoadmapCompletionContext}, {archiveSynthesisPath}",
            state.LastTransition.Output);
        Assert.Equal("Close Epic", state.LastTransition.Decision);
        Assert.Equal("UpdateRoadmapCompletionContext", state.TransitionIntent.Intent);
        Assert.Equal(
            [evaluationPath, Cli.RoadmapArtifactPaths.RoadmapCompletionContext, archiveSynthesisPath],
            state.TransitionIntent.EvidencePaths);
        Assert.DoesNotContain(updateEvidencePath, state.TransitionIntent.EvidencePaths);
        Assert.Equal(["SelectNextEpic"], state.NextValidTransitions);

        Cli.TransitionJournalRecord[] journal = ReadJournal(repo);
        Cli.TransitionJournalRecord updateCompleted = journal.Single(record =>
            record.Event == "TransitionCompleted" &&
            record.Prompt == "UpdateRoadmapCompletionContext");
        Assert.Equal([Cli.RoadmapArtifactPaths.RoadmapCompletionContext], updateCompleted.OutputPaths);

        Cli.TransitionJournalRecord routeCompleted = journal.Single(record =>
            record.Event == "TransitionCompleted" &&
            record.Prompt == "CompletionCertificationRouting");
        Assert.Equal(
            [evaluationPath, Cli.RoadmapArtifactPaths.RoadmapCompletionContext, archiveSynthesisPath],
            routeCompleted.OutputPaths);
        Assert.DoesNotContain(updateEvidencePath, routeCompleted.OutputPaths);
        Assert.True(Array.IndexOf(journal, routeCompleted) > Array.IndexOf(journal, updateCompleted));
        Assert.DoesNotContain(journal, record => record.Event == "CompletionCertificationRejected");
    }

    [Fact]
    public async Task Close_route_archive_synthesis_failure_preserves_completion_decision_without_invalid_certification_blocker()
    {
        using var repo = SeedRepo(includeCompletionContext: true);
        const string specPath = ".agents/specs/archive-synthesis-failure.md";
        const string executionEvidencePath = ".agents/evidence/execution/execution-result.0001.md";
        string evaluation = CompletionEvaluation("Fully Complete", "None", "Close Epic");
        await SeedCompletionClaimAsync(repo, specPath, executionEvidencePath);
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift")),
            ScriptedAgentRuntime.Completed(evaluation));
        var archive = new FakeCompletedEpicArchiveService
        {
            ExceptionToThrow = new InvalidOperationException("archive synthesis unavailable"),
        };

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(
            repo,
            runtime,
            completionArchive: archive).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        Assert.Equal(2, runtime.OneShotCalls);
        Assert.Single(archive.Requests);
        const string evaluationPath = ".agents/evidence/evaluations/epic-completion-and-drift.0001.md";
        const string updateEvidencePath = ".agents/evidence/evaluations/roadmap-completion-update.0001.md";
        string projectionPath = Cli.RoadmapArtifactPaths.ProjectionPaths["EvaluateEpicCompletionAndDrift"];
        Assert.Equal(evaluation, repo.Read(evaluationPath));
        Assert.Equal("existing completion context", repo.Read(Cli.RoadmapArtifactPaths.RoadmapCompletionContext));
        Assert.False(await repo.Artifacts.ExistsAsync(updateEvidencePath));
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "invalid-completion-certification.*.md"));
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "roadmap-transition-blocked-*.md"));

        var ledgerJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ledgerJsonOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        Cli.DecisionLedgerPersistenceDocument ledger = JsonSerializer.Deserialize<Cli.DecisionLedgerPersistenceDocument>(
            repo.Read(Cli.RoadmapArtifactPaths.DecisionLedgerJson),
            ledgerJsonOptions)!;
        Cli.DecisionLedgerEntry ledgerDecision = Assert.Single(ledger.ToDomain());
        Assert.Equal(Cli.RoadmapState.CompletionEvaluationAndContextUpdate, ledgerDecision.State);
        Assert.Equal("EvaluateEpicCompletionAndDrift", ledgerDecision.Transition);
        Assert.Equal(projectionPath, ledgerDecision.ProjectionPath);
        Assert.Equal([evaluationPath], ledgerDecision.OutputArtifactPaths);
        Assert.Equal("Close Epic", ledgerDecision.Decision);

        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.CompletionEvaluationAndContextUpdate, state.CurrentState);
        Assert.NotEqual(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("None", state.LastDecisionId);
        Assert.Equal(Cli.TransitionStatus.Completed, state.LastTransition.Status);
        Assert.Equal(Cli.RoadmapState.EpicCompletionDetected, state.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.CompletionEvaluationAndContextUpdate, state.LastTransition.To);
        Assert.Equal("EvaluateEpicCompletionAndDrift", state.LastTransition.Prompt);
        Assert.Equal(projectionPath, state.LastTransition.Projection);
        Assert.Equal(Cli.RoadmapArtifactPaths.EvaluationEvidenceDirectory, state.LastTransition.Output);
        Assert.Equal("Completed", state.LastTransition.Decision);
        Assert.Empty(state.Blockers);
        Assert.Equal("EvaluateEpicCompletionAndDrift", state.TransitionIntent.Intent);
        Assert.Equal(Cli.RoadmapState.EpicCompletionDetected, state.TransitionIntent.DispatchState);
        Assert.Equal([executionEvidencePath], state.TransitionIntent.EvidencePaths);
        Assert.NotEqual("ResolveInvalidCompletionCertification", state.TransitionIntent.Intent);

        Cli.TransitionJournalRecord[] journal = ReadJournal(repo);
        Cli.TransitionJournalRecord completed = journal.Single(record =>
            record.Event == "TransitionCompleted" &&
            record.Prompt == "EvaluateEpicCompletionAndDrift");
        Assert.Equal([Cli.RoadmapArtifactPaths.EvaluationEvidenceDirectory], completed.OutputPaths);
        Assert.DoesNotContain(journal, record => record.Event == "CompletionCertificationRejected");
        Assert.DoesNotContain(journal, record => record.Prompt == "CompletionCertificationRouting");
        Assert.DoesNotContain(journal, record => record.Prompt == "UpdateRoadmapCompletionContext");
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
        var persistence = new Cli.RoadmapTransitionPersistence(
            repo.Artifacts,
            new Cli.ProjectionManifestStore(repo.Artifacts),
            new RoadmapStateStore(repo.Artifacts),
            new Cli.DecisionLedgerStore(repo.Artifacts),
            new Cli.TransitionJournalStore(repo.Artifacts));
        Cli.InvariantValidationResult invariant = Cli.InvariantValidationResult.Invalid(
            Cli.RoadmapState.EvidenceBlocked,
            "Validator failed without evidence.",
            string.Empty,
            "MissingValidatorEvidence",
            "Restore validator diagnostics before continuing.");

        Cli.RoadmapStepException exception = await Assert.ThrowsAsync<Cli.RoadmapStepException>(() =>
            persistence.PersistInvariantFailureAndThrowAsync(
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

    private static async Task SeedCompletionClaimAsync(
        TempRepo repo,
        string specPath,
        string executionEvidencePath)
    {
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Completion Parse Failure Epic", "EPIC-COMPLETE"));
        repo.Write(specPath, """
            # Completion Parse Failure Milestone

            | Field | Value |
            |---|---|
            | Epic Path | .agents/epic.md |

            ## Acceptance Criteria

            - [x] Execution evidence is ready for completion evaluation.
            """);
        repo.Write(executionEvidencePath, """
            # Execution Report

            ## Execution Disposition

            | Field | Value |
            |---|---|
            | Status | Epic Complete |
            | Confidence | High |
            | Evidence Summary | Tests passed and implementation is complete. |
            | Next Step | EvaluateEpicCompletionAndDrift |
            """);
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(
            Cli.RoadmapArtifactPaths.ActiveEpic,
            Cli.ArtifactLifecycleState.Ready);
        await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, specPath);
        DateTimeOffset detectedAt = DateTimeOffset.UtcNow;
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(new Cli.RoadmapStateDocument(
            Cli.RoadmapState.EpicCompletionDetected,
            [],
            new Cli.RoadmapTransitionSummary(
                Cli.RoadmapState.ExecutionLoop,
                Cli.RoadmapState.EpicCompletionDetected,
                "ExecutionOutcomeInterpretation",
                "None",
                executionEvidencePath,
                "Epic Complete",
                Cli.TransitionStatus.Completed,
                detectedAt,
                detectedAt),
            [],
            "None",
            0,
            0,
            new Cli.ProjectionManifestCounts(0, 0, 0),
            new Cli.RoadmapTransitionIntent(
                "EvaluateEpicCompletionAndDrift",
                Cli.RoadmapState.EpicCompletionDetected,
                [executionEvidencePath]),
            ["EvaluateEpicCompletionAndDrift"],
            []));
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

    private static string CompletionEvaluation(string recommendation) =>
        CompletionEvaluation("Functionally Complete", "None", recommendation);

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
