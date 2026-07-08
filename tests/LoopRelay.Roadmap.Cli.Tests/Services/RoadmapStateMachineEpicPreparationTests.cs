using System.Text.Json;
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
        Assert.DoesNotContain("Retire Epic", state.NextValidTransitions);
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
        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Failed, state.LastTransition.Status);
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

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime, console).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Failed, outcome);
        Assert.Equal(4, runtime.OneShotCalls);

        string auditPath = Assert.Single(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.AuditEvidenceDirectory, "epic-preparation-audit.*.md"));
        Assert.Equal(audit, repo.Read(auditPath));

        Cli.DecisionLedgerPersistenceDocument ledger = JsonSerializer.Deserialize<Cli.DecisionLedgerPersistenceDocument>(
            repo.Read(Cli.RoadmapArtifactPaths.DecisionLedgerJson),
            Cli.RoadmapJson.Options)!;
        Cli.DecisionLedgerEntry auditDecision = Assert.Single(
            ledger.ToDomain(),
            entry => entry.State == Cli.RoadmapState.EpicPreparationAudit);
        Assert.Equal("EpicPreparationAudit", auditDecision.Transition);
        Assert.Equal(Cli.RoadmapArtifactPaths.ProjectionPaths["EpicPreparationAudit"], auditDecision.ProjectionPath);
        Assert.Equal([auditPath], auditDecision.OutputArtifactPaths);
        Assert.Equal("Insufficient Evidence", auditDecision.Decision);
        Assert.Equal("High", auditDecision.Confidence);
        Assert.Equal("Gather More Evidence", auditDecision.RationaleExcerpt);

        Cli.RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.EpicPreparationAudit, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Completed, state.LastTransition.Status);
        Assert.Equal(Cli.RoadmapState.ExistingEpicSelected, state.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.EpicPreparationAudit, state.LastTransition.To);
        Assert.Equal("EpicPreparationAudit", state.LastTransition.Prompt);
        Assert.Equal(Cli.RoadmapArtifactPaths.AuditEvidenceDirectory, state.LastTransition.Output);
        Assert.Equal("Completed", state.LastTransition.Decision);
        Assert.Empty(state.Blockers);
        Assert.Equal("None", state.TransitionIntent.Intent);
        Assert.Empty(await repo.Artifacts.ListAsync(Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory, "*.md"));

        Assert.Contains(console.Errors, error => error.Contains("Epic preparation audit requires more evidence.", StringComparison.Ordinal));
        Assert.Contains(console.Warnings, warning =>
            warning.Contains("Roadmap state machine blocked: Epic preparation audit requires more evidence.", StringComparison.Ordinal) &&
            warning.Contains("Persisted roadmap state remains EpicPreparationAudit.", StringComparison.Ordinal));

        Cli.TransitionJournalRecord[] auditRecords = ReadJournal(repo)
            .Where(record => record.Prompt == "EpicPreparationAudit")
            .ToArray();
        Assert.Contains(auditRecords, record => record.Event == "TransitionCompleted");
        Assert.DoesNotContain(auditRecords, record => record.Event == "TransitionFailed");
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
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

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);
        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
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
