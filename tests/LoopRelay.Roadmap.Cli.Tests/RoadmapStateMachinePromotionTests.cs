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

    [Fact]
    public async Task Successful_authoring_promotes_active_epic_and_continues_to_execution_routing()
    {
        using var repo = SeedRepo();
        string epic = RoadmapSamples.ValidEpic("Created Epic", "EPIC-NEW");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(NewEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic")),
            ScriptedAgentRuntime.Completed(epic),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic")),
            ScriptedAgentRuntime.Completed(MilestoneBundle()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift")),
            ScriptedAgentRuntime.Completed(CompletionEvaluation("Continue Epic")));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(epic, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.Equal(8, runtime.OneShotCalls);
        Assert.Contains("ArtifactPromoted", repo.Read(Cli.RoadmapArtifactPaths.TransitionJournal), StringComparison.Ordinal);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.ExecutionLoop, state.CurrentState);
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
            ScriptedAgentRuntime.Completed(MilestoneBundle()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EvaluateEpicCompletionAndDrift")),
            ScriptedAgentRuntime.Completed(CompletionEvaluation("Continue Epic")));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(replacement, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.Equal(10, runtime.OneShotCalls);
        Assert.Contains("ArtifactPromoted", repo.Read(Cli.RoadmapArtifactPaths.TransitionJournal), StringComparison.Ordinal);
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
}
