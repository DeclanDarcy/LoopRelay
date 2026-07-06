using LoopRelay.Roadmap.Cli;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapStateMachineEpicPreparationTests
{
    [Fact]
    public async Task Retire_disposition_is_successful_transition_and_persists_selected_identity()
    {
        using var repo = SeedRepo();
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
            ScriptedAgentRuntime.Completed(RetireAudit()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.RetireEpic, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Completed, state.LastTransition.Status);
        Assert.Equal(Cli.RoadmapState.RetireEpic, state.LastTransition.To);
        Cli.RetiredEpic retired = Assert.Single(state.RetiredEpics);
        Assert.Equal("EPIC-001", retired.EpicId);
        Assert.Equal("Epic A", retired.EpicName);
        Assert.DoesNotContain("- Retire Epic", repo.Read(Cli.RoadmapArtifactPaths.State), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restart_after_retire_derives_selection_context_from_retired_epic_record()
    {
        using var repo = SeedRepo();
        await RetireEpicAsync(repo);
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        string prompt = Assert.Single(runtime.Prompts);
        Assert.Contains("## Retired Epics", prompt, StringComparison.Ordinal);
        Assert.Contains("EPIC-001", prompt, StringComparison.Ordinal);
        Assert.Contains("Epic A", prompt, StringComparison.Ordinal);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Single(state.RetiredEpics);
    }

    [Fact]
    public async Task Duplicate_retirement_is_deduplicated_by_stable_identity()
    {
        using var repo = SeedRepo();
        await RetireEpicAsync(repo);
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
            ScriptedAgentRuntime.Completed(RetireAudit()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Cli.RetiredEpic retired = Assert.Single(state.RetiredEpics);
        Assert.Equal("EPIC-001", retired.EpicId);
    }

    [Fact]
    public async Task Later_blocked_persistence_preserves_retired_epic_records()
    {
        using var repo = SeedRepo();
        await RetireEpicAsync(repo);
        var runtime = new ScriptedAgentRuntime(ScriptedAgentRuntime.Failed("selection failed"));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Cli.RetiredEpic retired = Assert.Single(state.RetiredEpics);
        Assert.Equal("EPIC-001", retired.EpicId);
    }

    [Fact]
    public async Task Runtime_prompt_failure_remains_failed_transition()
    {
        using var repo = SeedRepo();
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Failed("selection failed"));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        string state = repo.Read(Cli.RoadmapArtifactPaths.State);
        Assert.Contains("EvidenceBlocked", state, StringComparison.Ordinal);
        Assert.Contains("| Status | Failed |", state, StringComparison.Ordinal);
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapFile, "roadmap");
        return repo;
    }

    private static async Task RetireEpicAsync(TempRepo repo)
    {
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
            ScriptedAgentRuntime.Completed(RetireAudit()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);
        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
    }

    private static string ExistingEpicSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select Existing Epic |
        | Recommended Initiative | Epic A |
        | Initiative Type | Existing Roadmap Epic |
        | Confidence | High |
        | Primary Reason | Best leverage |

        ## If Existing Roadmap Epic Selected

        | Field | Value |
        |---|---|
        | Epic ID | EPIC-001 |
        | Epic Name | Epic A |
        | Why This Epic Now | It is the next obsolete item to verify. |
        | Dependencies Satisfied? | Yes |
        | Required Pre-Implementation Follow-Up | None |
        """;

    private static string RetireAudit() => """
        # Epic Preparation Audit

        ## Selected Epic

        | Field | Value |
        |---|---|
        | Epic ID | EPIC-001 |
        | Epic Name | Epic A |
        | Claimed Strategic Purpose | Old capability |
        | Apparent Projection Link | Capability A |

        ## Audit Disposition

        | Field | Value |
        |---|---|
        | Disposition | Retire |
        | Confidence | High |
        | Primary Reason | Repository evidence shows the capability is already satisfied. |
        | Evidence Strength | Strong |
        | Recommended Next Step | Retire Epic |
        """;

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
