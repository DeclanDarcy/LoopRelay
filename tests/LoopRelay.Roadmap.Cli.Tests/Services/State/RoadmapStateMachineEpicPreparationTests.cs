using System.Collections;
using System.Text.Json;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Tests.Services.Cli;
using LoopRelay.Roadmap.Cli.Tests.Services.Execution;
using LoopRelay.Roadmap.Cli.Tests.Services.Projections;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.Services.State.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests.Services.State;

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

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.RetireEpic, state.CurrentState);
        Assert.Equal(TransitionStatus.Completed, state.LastTransition.Status);
        Assert.Equal(RoadmapState.RetireEpic, state.LastTransition.To);
        RetiredEpic retired = Assert.Single(state.RetiredEpics);
        Assert.Equal("EPIC-001", retired.EpicId);
        Assert.Equal("Epic A", retired.EpicName);
        Assert.DoesNotContain("Retire Epic", state.NextValidTransitions);
    }

    [Fact]
    public async Task Restart_after_retire_derives_selection_context_from_retired_epic_record()
    {
        using var repo = SeedRepo();
        await RetireEpicAsync(repo);
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        string prompt = Assert.Single(runtime.Prompts);
        Assert.Contains("## Retired Epics", prompt, StringComparison.Ordinal);
        Assert.Contains("EPIC-001", prompt, StringComparison.Ordinal);
        Assert.Contains("Epic A", prompt, StringComparison.Ordinal);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
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

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        RetiredEpic retired = Assert.Single(state.RetiredEpics);
        Assert.Equal("EPIC-001", retired.EpicId);
    }

    [Fact]
    public async Task Later_blocked_persistence_preserves_retired_epic_records()
    {
        using var repo = SeedRepo();
        await RetireEpicAsync(repo);
        var runtime = new ScriptedAgentRuntime(ScriptedAgentRuntime.Failed("selection failed"));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
        RetiredEpic retired = Assert.Single(state.RetiredEpics);
        Assert.Equal("EPIC-001", retired.EpicId);
    }

    [Fact]
    public async Task Runtime_prompt_failure_remains_failed_transition()
    {
        using var repo = SeedRepo();
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Failed("selection failed"));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);
        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(TransitionStatus.Failed, state.LastTransition.Status);
    }

    [Fact]
    public async Task Insufficient_evidence_audit_persists_audit_evidence_and_decision_without_durable_blocker()
    {
        using var repo = SeedRepo();
        string audit = InsufficientEvidenceAudit();
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
            ScriptedAgentRuntime.Completed(audit));
        var console = new TestConsole();

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime, console).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);
        Assert.Equal(4, runtime.OneShotCalls);

        string auditPath = Assert.Single(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.AuditEvidenceDirectory, "epic-preparation-audit.*.md"));
        Assert.Equal(audit, repo.Read(auditPath));

        DecisionLedgerPersistenceDocument ledger = JsonSerializer.Deserialize<DecisionLedgerPersistenceDocument>(
            repo.Read(RoadmapArtifactPaths.DecisionLedgerJson),
            RoadmapJson.Options)!;
        DecisionLedgerEntry auditDecision = Assert.Single(
            ledger.ToDomain(),
            entry => entry.State == RoadmapState.EpicPreparationAudit);
        Assert.Equal("EpicPreparationAudit", auditDecision.Transition);
        Assert.Equal((string?)RoadmapArtifactPaths.ProjectionPaths["EpicPreparationAudit"], auditDecision.ProjectionPath);
        Assert.Equal([auditPath], auditDecision.OutputArtifactPaths);
        Assert.Equal("Insufficient Evidence", auditDecision.Decision);
        Assert.Equal("High", auditDecision.Confidence);
        Assert.Equal("Gather More Evidence", auditDecision.RationaleExcerpt);

        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.EpicPreparationAudit, state.CurrentState);
        Assert.Equal(TransitionStatus.Completed, state.LastTransition.Status);
        Assert.Equal(RoadmapState.ExistingEpicSelected, state.LastTransition.From);
        Assert.Equal(RoadmapState.EpicPreparationAudit, state.LastTransition.To);
        Assert.Equal("EpicPreparationAudit", state.LastTransition.Prompt);
        Assert.Equal((string?)RoadmapArtifactPaths.AuditEvidenceDirectory, state.LastTransition.Output);
        Assert.Equal("Completed", state.LastTransition.Decision);
        Assert.Empty(state.Blockers);
        Assert.Equal("None", state.TransitionIntent.Intent);
        Assert.Empty((IEnumerable)await repo.Artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "*.md"));

        Assert.Contains(console.Errors, error => error.Contains("Epic preparation audit requires more evidence.", StringComparison.Ordinal));
        Assert.Contains(console.Warnings, warning =>
            warning.Contains("Roadmap state machine blocked: Epic preparation audit requires more evidence.", StringComparison.Ordinal) &&
            warning.Contains("Persisted roadmap state remains EpicPreparationAudit.", StringComparison.Ordinal));

        TransitionJournalRecord[] auditRecords = ReadJournal(repo)
            .Where(record => record.Prompt == "EpicPreparationAudit")
            .ToArray();
        Assert.Contains(auditRecords, record => record.Event == "TransitionCompleted");
        Assert.DoesNotContain(auditRecords, record => record.Event == "TransitionFailed");
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        return repo;
    }

    private static async Task RetireEpicAsync(TempRepo repo)
    {
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
            ScriptedAgentRuntime.Completed(RetireAudit()));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);
        Assert.Equal(RoadmapOutcome.Paused, outcome);
    }

    private static TransitionJournalRecord[] ReadJournal(TempRepo repo) =>
        repo.Read(RoadmapArtifactPaths.TransitionJournal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .ToArray();

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

    private static string InsufficientEvidenceAudit() => """
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
        | Disposition | Insufficient Evidence |
        | Confidence | High |
        | Primary Reason | Repository evidence is not enough to safely realign, reimagine, or retire the epic. |
        | Evidence Strength | Weak |
        | Recommended Next Step | Gather More Evidence |
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
