using System.Text.Json;
using LoopRelay.Roadmap.Cli;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapStateMachinePromotionTests
{
    [Fact]
    public async Task CreateNewEpic_blocked_output_becomes_evidence_and_never_active_epic()
    {
        using var repo = SeedRepo();
        string blocked = CreateBlocked();
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(NewEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic")),
            ScriptedAgentRuntime.Completed(blocked));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.False(await repo.Artifacts.ExistsAsync(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.Equal(4, runtime.OneShotCalls);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal("Artifact Promotion Blocked", state.LastTransition.Decision);
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal(blocked, repo.Read(evidencePath));
        Assert.DoesNotContain((await new Cli.ArtifactLifecycleStore(repo.Artifacts).LoadAsync()), entry => entry.Path == Cli.RoadmapArtifactPaths.ActiveEpic);
    }

    [Fact]
    public async Task CreateNewEpic_ambiguous_output_never_advances_to_milestone_generation()
    {
        using var repo = SeedRepo();
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(NewEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic")),
            ScriptedAgentRuntime.Completed("I cannot determine a safe epic from this proposal."));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.False(await repo.Artifacts.ExistsAsync(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.False(await repo.Artifacts.ExistsAsync($"{Cli.RoadmapArtifactPaths.SpecsDirectory}/promotion-test.md"));
        Assert.Equal(4, runtime.OneShotCalls);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("Artifact Promotion Ambiguous", state.LastTransition.Decision);
    }

    [Fact]
    public async Task CreateNewEpic_prompt_completion_is_not_artifact_completion()
    {
        using var repo = SeedRepo();
        string blocked = CreateBlocked();
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(NewEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic")),
            ScriptedAgentRuntime.Completed(blocked));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.False(await repo.Artifacts.ExistsAsync(Cli.RoadmapArtifactPaths.ActiveEpic));
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal("Artifact Promotion Blocked", state.LastTransition.Decision);
        Assert.Equal(evidencePath, state.LastTransition.Output);

        Cli.TransitionJournalRecord[] journal = ReadJournal(repo);
        int promptCompletedIndex = Array.FindIndex(journal, record =>
            record.Event == "PromptCompleted" &&
            record.Prompt == "CreateNewEpic");
        int promotionBlockedIndex = Array.FindIndex(journal, record =>
            record.Event == "ArtifactPromotionBlocked" &&
            record.Prompt == "CreateNewEpic");

        Assert.NotEqual(-1, promptCompletedIndex);
        Assert.True(promotionBlockedIndex > promptCompletedIndex);
        Assert.DoesNotContain(journal, record =>
            record.Event == "TransitionCompleted" &&
            record.Prompt == "CreateNewEpic");

        Cli.TransitionJournalRecord promptCompleted = journal[promptCompletedIndex];
        Cli.TransitionJournalRecord promotionBlocked = journal[promotionBlockedIndex];
        Assert.Equal(promptCompleted.CorrelationId, promotionBlocked.CorrelationId);
        Assert.Equal(Cli.RoadmapState.NewEpicProposed, promptCompleted.PreviousState);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, promptCompleted.AttemptedState);
        Assert.Equal("PromptCompleted", promptCompleted.Result);
        Assert.Equal("Output produced", promptCompleted.ParserDecision);
        Assert.Equal([Cli.RoadmapArtifactPaths.ActiveEpic], promptCompleted.OutputPaths);
        Assert.Equal("ArtifactPromotionService", promotionBlocked.PromptContractKey);
        Assert.Equal([evidencePath], promotionBlocked.OutputPaths);
        Assert.Equal(blocked, repo.Read(evidencePath));
    }

    [Fact]
    public async Task Realign_blocked_output_preserves_existing_active_epic()
    {
        using var repo = SeedRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(Cli.RoadmapArtifactPaths.ActiveEpic, Cli.ArtifactLifecycleState.Ready, "Existing active epic.");
        string blocked = RealignBlocked();
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
            ScriptedAgentRuntime.Completed(AuditDisposition("Realign")),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("RealignEpic")),
            ScriptedAgentRuntime.Completed(blocked));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(existing, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.Equal(6, runtime.OneShotCalls);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal(blocked, repo.Read(evidencePath));
    }

    [Fact]
    public async Task Reimagine_blocked_output_preserves_existing_active_epic()
    {
        using var repo = SeedRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
        string blocked = ReimagineBlocked();
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
            ScriptedAgentRuntime.Completed(AuditDisposition("Reimagine")),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("ReimagineEpic")),
            ScriptedAgentRuntime.Completed(blocked));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(existing, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.Equal(6, runtime.OneShotCalls);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal(blocked, repo.Read(evidencePath));
    }

    [Theory]
    [InlineData("RealignEpic", "Realign", "ACTIVE-EPIC-REALIGN-SENTINEL")]
    [InlineData("ReimagineEpic", "Reimagine", "ACTIVE-EPIC-REIMAGINE-SENTINEL")]
    public async Task Rewrite_prompts_prefer_active_epic_over_current_selection(
        string runtimePrompt,
        string disposition,
        string activeEpicSentinel)
    {
        using var repo = SeedRepo();
        string activeEpic = RoadmapSamples.ValidEpic($"Existing Epic {activeEpicSentinel}", "EPIC-OLD", "Ready");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, activeEpic);
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(ExistingEpicSelection("SELECTION-ONLY-SENTINEL")),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
            ScriptedAgentRuntime.Completed(AuditDisposition(disposition)),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid(runtimePrompt)),
            ScriptedAgentRuntime.Completed(RewriteBlocked(runtimePrompt)));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        string prompt = RewriteRuntimePrompt(runtime);
        Assert.Contains(activeEpicSentinel, prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECTION-ONLY-SENTINEL", prompt, StringComparison.Ordinal);

        Cli.TransitionJournalRecord started = ReadJournal(repo).Single(record =>
            record.Event == "TransitionStarted" &&
            record.Prompt == runtimePrompt);
        Assert.NotNull(started.InputSnapshot);
        Assert.Contains(started.InputSnapshot.ArtifactInputs, input =>
            input.Path == Cli.RoadmapArtifactPaths.ActiveEpic &&
            input.Roles == Cli.TransitionInputRole.ActiveEpic);
        Assert.DoesNotContain(started.InputSnapshot.ArtifactInputs, input =>
            input.Path == Cli.RoadmapArtifactPaths.Selection);
        Assert.Equal(Cli.RoadmapHash.Sha256(activeEpic), started.InputArtifactHashes[Cli.RoadmapArtifactPaths.ActiveEpic]);
        Assert.False(started.InputArtifactHashes.ContainsKey(Cli.RoadmapArtifactPaths.Selection));
    }

    [Theory]
    [InlineData("RealignEpic", "Realign")]
    [InlineData("ReimagineEpic", "Reimagine")]
    public async Task Rewrite_prompts_fallback_to_current_selection_when_active_epic_is_missing(
        string runtimePrompt,
        string disposition)
    {
        using var repo = SeedRepo();
        string selection = ExistingEpicSelection("SELECTION-FALLBACK-SENTINEL");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(selection),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
            ScriptedAgentRuntime.Completed(AuditDisposition(disposition)),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid(runtimePrompt)),
            ScriptedAgentRuntime.Completed(RewriteBlocked(runtimePrompt)));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.False(await repo.Artifacts.ExistsAsync(Cli.RoadmapArtifactPaths.ActiveEpic));
        string prompt = RewriteRuntimePrompt(runtime);
        Assert.Contains("SELECTION-FALLBACK-SENTINEL", prompt, StringComparison.Ordinal);

        Cli.TransitionJournalRecord started = ReadJournal(repo).Single(record =>
            record.Event == "TransitionStarted" &&
            record.Prompt == runtimePrompt);
        Assert.NotNull(started.InputSnapshot);
        Assert.Contains(started.InputSnapshot.ArtifactInputs, input =>
            input.Path == Cli.RoadmapArtifactPaths.Selection &&
            input.Roles == Cli.TransitionInputRole.Selection);
        Assert.DoesNotContain(started.InputSnapshot.ArtifactInputs, input =>
            input.Path == Cli.RoadmapArtifactPaths.ActiveEpic);
        Assert.Equal(Cli.RoadmapHash.Sha256(selection), started.InputArtifactHashes[Cli.RoadmapArtifactPaths.Selection]);
        Assert.False(started.InputArtifactHashes.ContainsKey(Cli.RoadmapArtifactPaths.ActiveEpic));
    }

    [Fact]
    public async Task Successful_authoring_promotes_active_epic_generates_milestone_specs_and_pauses()
    {
        using var repo = SeedRepo();
        string epic = RoadmapSamples.ValidEpic("Created Epic", "EPIC-NEW");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(NewEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic")),
            ScriptedAgentRuntime.Completed(epic),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic")),
            ScriptedAgentRuntime.Completed(MilestoneBundle()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(epic, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.Equal(6, runtime.OneShotCalls);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.MilestoneSpecsReady, state.CurrentState);
        Assert.Equal(Cli.RoadmapArtifactPaths.SpecsDirectory, state.LastTransition.Output);
        Assert.False(await repo.Artifacts.ExistsAsync(Cli.RoadmapArtifactPaths.OperationalContext));
        Assert.False(await repo.Artifacts.ExistsAsync(Cli.RoadmapArtifactPaths.ExecutionPrompt));

        Cli.TransitionJournalRecord[] journal = ReadJournal(repo);
        Assert.Contains(journal, record => record.Event == "ArtifactPromoted");
        int promptCompletedIndex = Array.FindIndex(journal, record =>
            record.Event == "PromptCompleted" &&
            record.Prompt == "GenerateMilestoneDeepDivesForEpic");
        int materializedIndex = Array.FindIndex(journal, record =>
            record.Event == "MilestoneSpecsMaterialized" &&
            record.Prompt == "GenerateMilestoneDeepDivesForEpic");
        Assert.NotEqual(-1, promptCompletedIndex);
        Assert.True(materializedIndex > promptCompletedIndex);
        Assert.DoesNotContain(journal, record =>
            record.Event == "TransitionCompleted" &&
            record.Prompt == "GenerateMilestoneDeepDivesForEpic");

        Cli.TransitionJournalRecord promptCompleted = journal[promptCompletedIndex];
        Cli.TransitionJournalRecord materialized = journal[materializedIndex];
        Assert.Equal(promptCompleted.CorrelationId, materialized.CorrelationId);
        Assert.Equal(promptCompleted.DurationMilliseconds, materialized.DurationMilliseconds);
        Assert.Equal(promptCompleted.InputArtifactHashes, materialized.InputArtifactHashes);
        Assert.NotNull(promptCompleted.InputSnapshot);
        Assert.NotNull(materialized.InputSnapshot);
        Assert.Equal(promptCompleted.InputSnapshot.SnapshotHash, materialized.InputSnapshot.SnapshotHash);
        Assert.Equal(promptCompleted.InputSnapshot.RuntimePromptName, materialized.InputSnapshot.RuntimePromptName);
        Assert.Equal(promptCompleted.InputSnapshot.Projection, materialized.InputSnapshot.Projection);
        Assert.Equal(promptCompleted.InputSnapshot.PromptContextHash, materialized.InputSnapshot.PromptContextHash);
        Assert.Equal(promptCompleted.InputSnapshot.SecondaryInputHash, materialized.InputSnapshot.SecondaryInputHash);
        Assert.Equal(promptCompleted.InputSnapshot.ArtifactInputs, materialized.InputSnapshot.ArtifactInputs);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, promptCompleted.PreviousState);
        Assert.Equal(Cli.RoadmapState.MilestoneSpecsReady, promptCompleted.AttemptedState);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, materialized.PreviousState);
        Assert.Equal(Cli.RoadmapState.MilestoneSpecsReady, materialized.AttemptedState);
        Assert.Equal([Cli.RoadmapArtifactPaths.SpecsDirectory], promptCompleted.OutputPaths);
        Assert.Equal([Cli.RoadmapArtifactPaths.SpecsDirectory], materialized.OutputPaths);
        Assert.Equal("PromptCompleted", promptCompleted.Result);
        Assert.Equal("Output produced", promptCompleted.ParserDecision);
        Assert.Equal("MilestoneSpecPostProcessing", materialized.PromptContractKey);
        Assert.Equal("Completed", materialized.Result);
        Assert.Equal("Milestone Specs Ready", materialized.ParserDecision);
    }

    [Fact]
    public async Task Successful_rewrite_promotes_replacement_active_epic()
    {
        using var repo = SeedRepo();
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready"));
        string replacement = RoadmapSamples.ValidEpic("Realigned Epic", "EPIC-OLD", "Realigned", "Realign");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
            ScriptedAgentRuntime.Completed(AuditDisposition("Realign")),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("RealignEpic")),
            ScriptedAgentRuntime.Completed(replacement),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic")),
            ScriptedAgentRuntime.Completed(MilestoneBundle()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(replacement, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.Equal(8, runtime.OneShotCalls);
        Assert.Contains("ArtifactPromoted", repo.Read(Cli.RoadmapArtifactPaths.TransitionJournal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Realign_rewrite_without_milestone_roadmap_is_blocked_and_preserves_existing_active_epic()
    {
        using var repo = SeedRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
        string replacement = StripMilestoneRoadmap(RoadmapSamples.ValidEpic("Realigned Epic", "EPIC-OLD", "Realigned", "Realign"));
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
            ScriptedAgentRuntime.Completed(AuditDisposition("Realign")),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("RealignEpic")),
            ScriptedAgentRuntime.Completed(replacement));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(existing, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.Equal(6, runtime.OneShotCalls);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal("Artifact Promotion Invalid", state.LastTransition.Decision);
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal(replacement, repo.Read(evidencePath));
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        return repo;
    }

    private static string NewEpicSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select New Intermediary Epic |
        | Recommended Initiative | Build promotion test epic |
        | Initiative Type | New Intermediary Epic |
        | Confidence | High |
        | Primary Reason | Exercise active epic promotion. |
        """;

    private static string ExistingEpicSelection(string primaryReason = "Existing epic needs audit.") => $$"""
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select Existing Epic |
        | Recommended Initiative | Existing Epic |
        | Initiative Type | Existing Roadmap Epic |
        | Confidence | High |
        | Primary Reason | {{primaryReason}} |

        ## If Existing Roadmap Epic Selected

        | Field | Value |
        |---|---|
        | Epic ID | EPIC-OLD |
        | Epic Name | Existing Epic |
        | Why This Epic Now | {{primaryReason}} |
        | Dependencies Satisfied? | Yes |
        | Required Pre-Implementation Follow-Up | None |
        """;

    private static string AuditDisposition(string disposition) => $$"""
        # Epic Preparation Audit

        ## Selected Epic

        | Field | Value |
        |---|---|
        | Epic ID | EPIC-OLD |
        | Epic Name | Existing Epic |
        | Claimed Strategic Purpose | Preserve roadmap promotion safety |
        | Apparent Projection Link | Promotion Boundary |

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

        The proposal requires roadmap revision before epic authoring.

        ## Blocking Evidence

        | Source | Evidence | Implication |
        |---|---|---|
        | Selection | Roadmap revision needed | Do not author an epic |

        ## Required Next Step

        Revise the roadmap.
        """;

    private static string RealignBlocked() => """
        # Epic Realignment Blocked

        ## Reason

        The audit does not support safe realignment.
        """;

    private static string ReimagineBlocked() => """
        # Epic Reimagination Blocked

        ## Reason

        The audit does not support safe reimagination.
        """;

    private static string RewriteBlocked(string runtimePrompt) =>
        runtimePrompt switch
        {
            "RealignEpic" => RealignBlocked(),
            "ReimagineEpic" => ReimagineBlocked(),
            _ => throw new ArgumentOutOfRangeException(nameof(runtimePrompt)),
        };

    private static string RewriteRuntimePrompt(ScriptedAgentRuntime runtime) =>
        Assert.Single(runtime.Prompts, prompt =>
            prompt.Contains("# Roadmap Runtime Prompt Context", StringComparison.Ordinal) &&
            prompt.Contains("## Current Epic", StringComparison.Ordinal));

    private static Cli.TransitionJournalRecord[] ReadJournal(TempRepo repo) =>
        repo.Read(Cli.RoadmapArtifactPaths.TransitionJournal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<Cli.TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .ToArray();

    private static string MilestoneBundle() => """
        # FILE: .agents/specs/promotion-test.md
        # Promotion Test Milestone

        | Field | Value |
        |---|---|
        | Epic Path | .agents/epic.md |

        ## Acceptance Criteria

        - [ ] Promotion boundary remains explicit.
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

    private static string StripMilestoneRoadmap(string epic)
    {
        int milestoneStart = epic.IndexOf("## Milestone Roadmap", StringComparison.Ordinal);
        return milestoneStart < 0
            ? epic
            : epic[..milestoneStart].TrimEnd() + Environment.NewLine;
    }
}
