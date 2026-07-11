using System.Text.Json;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Infrastructure.Services.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;
using LoopRelay.Orchestration.Services.Hitl;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.DerivedArtifacts;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.Projections;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Decisions;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.State;
using LoopRelay.Roadmap.Cli.Tests.Services.Cli;
using LoopRelay.Roadmap.Cli.Tests.Services.Execution;
using LoopRelay.Roadmap.Cli.Tests.Services.Projections;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using DecisionLedgerStore = LoopRelay.Roadmap.Cli.Services.Decisions.DecisionLedgerStore;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.Services.Projections.ProjectContextLoader;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.Services.State.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests.Services.State;

public sealed class RoadmapStateMachineSelectionTests
{
    [Fact]
    public async Task Missing_completion_context_triggers_bootstrap_before_selection()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateRoadmapCompletionContext")),
            ScriptedAgentRuntime.Completed("# Roadmap Completion Context"),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Contains("# Roadmap Completion Context", repo.Read(RoadmapArtifactPaths.RoadmapCompletionContext), StringComparison.Ordinal);
        Assert.Equal(4, runtime.OneShotCalls);
        Assert.Contains(runtime.Prompts, prompt => prompt.Contains("No completed epic markdown files were found under `.agents/archive/epics/*.md`.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_completion_context_bootstrap_passes_archived_epic_evidence()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        repo.Write(".agents/archive/epics/001-done.md", """
            # Epic: Archived Capability

            ## Epic Metadata

            | Field | Value |
            |---|---|
            | Epic ID | EPIC-DONE |

            ## Completion Evidence

            Implemented and verified.
            """);
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateRoadmapCompletionContext")),
            ScriptedAgentRuntime.Completed("# Roadmap Completion Context"),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        string bootstrapPrompt = runtime.Prompts.Single(prompt => prompt.Contains(".agents/archive/epics/001-done.md", StringComparison.Ordinal));
        Assert.Contains("Archived Capability", bootstrapPrompt, StringComparison.Ordinal);
        Assert.Contains("| Epic ID | EPIC-DONE |", bootstrapPrompt, StringComparison.Ordinal);
        Assert.Contains("| Evidence Quality | Strong |", bootstrapPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_completion_context_skips_bootstrap()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "existing context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(2, runtime.OneShotCalls);
        Assert.Equal("existing context", repo.Read(RoadmapArtifactPaths.RoadmapCompletionContext));
    }

    [Fact]
    public async Task Stale_selection_projection_writes_projection_blocker_before_transition_start()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "existing context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        string projectionPath = RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        repo.Write(projectionPath, ProjectionSamples.Valid("SelectNextEpic"));
        await SeedStaleSelectionProjectionManifestAsync(repo);
        var runtime = new ScriptedAgentRuntime();

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);
        Assert.Equal(0, runtime.OneShotCalls);
        string blockerPath = Assert.Single(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "projection-blocked.*.md"));
        Assert.Contains("Projection refresh recommended", repo.Read(blockerPath), StringComparison.Ordinal);
        Assert.Contains("SelectNextEpic", repo.Read(blockerPath), StringComparison.Ordinal);

        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.CoreReady, state.CurrentState);
        Assert.Equal("Preflight", state.LastTransition.Prompt);
        Assert.NotEqual(RoadmapState.SelectNextStrategicInitiative, state.LastTransition.To);

        if (await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.TransitionJournal))
        {
            string journal = repo.Read(RoadmapArtifactPaths.TransitionJournal);
            Assert.DoesNotContain("\"event\":\"TransitionStarted\"", journal, StringComparison.Ordinal);
            Assert.DoesNotContain("\"prompt\":\"SelectNextEpic\"", journal, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Selection_parse_failure_happens_after_selection_artifacts_and_before_decision_ledger_append()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "existing context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        const string invalidSelection = """
            # Next Strategic Initiative Selection

            ## Recommendation Summary

            | Field | Value |
            |---|---|
            | Recommended Outcome | Unsupported Outcome |
            | Recommended Initiative | Investigate A |
            | Initiative Type | Strategic Investigation |
            | Confidence | Medium |
            | Primary Reason | Parser should fail after materialization. |
            """;
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(invalidSelection));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);
        Assert.Equal(invalidSelection, repo.Read(RoadmapArtifactPaths.Selection));

        string evidencePath = Assert.Single(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.SelectionEvidenceDirectory, "selection.*.md"));
        Assert.Equal(invalidSelection, repo.Read(evidencePath));

        SelectionProvenanceManifest manifest = await new SelectionProvenanceManifestStore(repo.Artifacts).LoadAsync();
        DerivedArtifactManifestEntry provenance = Assert.Single(manifest.ActiveSelections);
        Assert.Equal((string?)RoadmapArtifactPaths.Selection, provenance.ArtifactPath);
        Assert.Equal(RoadmapHash.Sha256(invalidSelection), provenance.ArtifactHash);

        ArtifactLifecycleEntry lifecycle = Assert.Single(
            await new ArtifactLifecycleStore(repo.Artifacts).LoadAsync(),
            entry => entry.Path == RoadmapArtifactPaths.Selection);
        Assert.Equal(ArtifactLifecycleState.Ready, lifecycle.State);
        Assert.Equal(evidencePath, lifecycle.Notes);

        Assert.False((bool)await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.DecisionLedgerJson));
        Assert.Equal("None", await new DecisionLedgerStore(repo.Artifacts).LastDecisionIdAsync());

        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.SelectNextStrategicInitiative, state.CurrentState);
        Assert.Equal(TransitionStatus.Completed, state.LastTransition.Status);
        Assert.Equal("Completed", state.LastTransition.Decision);

        TransitionJournalRecord[] selectionRecords = ReadJournal(repo)
            .Where(record => record.Prompt == "SelectNextEpic")
            .ToArray();
        Assert.Contains(selectionRecords, record => record.Event == "TransitionCompleted");
        Assert.DoesNotContain(selectionRecords, record => record.Event == "TransitionFailed");
    }

    [Theory]
    [InlineData("Select New Intermediary Epic", "CreateNewEpic")]
    [InlineData("Select Split Epic", "SplitEpic")]
    [InlineData("Select Existing Epic", "EpicPreparationAudit")]
    public async Task Stale_active_selection_is_rejected_before_downstream_prompt_runs(
        string recommendedOutcome,
        string downstreamPrompt)
    {
        using var repo = SeedRepo();
        var runtime = new MutatingOneShotRuntime(
            new ScriptedAgentRuntime(
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
                ScriptedAgentRuntime.Completed(SelectionForOutcome(recommendedOutcome))),
            mutateAfterOneShotCall: 2,
            () => repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "changed completion context"));
        var console = new TestConsole();

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime, console).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);
        Assert.Equal(2, runtime.OneShotCalls);
        Assert.False((bool)await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.ProjectionPaths[downstreamPrompt]));
        AssertDownstreamPromptNotStarted(repo, downstreamPrompt);
        Assert.Contains(console.Errors, error => error.Contains("Active selection cannot be used because it does not belong to the current selection cycle", StringComparison.Ordinal));

        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.SelectNextStrategicInitiative, state.CurrentState);
        Assert.Equal("SelectNextEpic", state.LastTransition.Prompt);
        Assert.Equal(TransitionStatus.Completed, state.LastTransition.Status);
    }

    [Fact]
    public async Task Stale_active_selection_is_rejected_before_rewrite_fallback_prompt_runs()
    {
        using var repo = SeedRepo();
        var runtime = new MutatingOneShotRuntime(
            new ScriptedAgentRuntime(
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
                ScriptedAgentRuntime.Completed(ExistingEpicSelection()),
                ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("EpicPreparationAudit")),
                ScriptedAgentRuntime.Completed(AuditDisposition("Realign"))),
            mutateAfterOneShotCall: 4,
            () => repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "changed completion context"));
        var console = new TestConsole();

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime, console).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Failed, outcome);
        Assert.Equal(4, runtime.OneShotCalls);
        Assert.False((bool)await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.ProjectionPaths["RealignEpic"]));
        AssertDownstreamPromptNotStarted(repo, "RealignEpic");
        Assert.Contains(console.Errors, error => error.Contains("Active selection cannot be used because it does not belong to the current selection cycle", StringComparison.Ordinal));

        string auditPath = Assert.Single(await repo.Artifacts.ListAsync(RoadmapArtifactPaths.AuditEvidenceDirectory, "epic-preparation-audit.*.md"));
        Assert.Contains("Disposition | Realign", repo.Read(auditPath), StringComparison.Ordinal);

        RoadmapStateDocument state = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(RoadmapState.EpicPreparationAudit, state.CurrentState);
        Assert.Equal("EpicPreparationAudit", state.LastTransition.Prompt);
        Assert.Equal(TransitionStatus.Completed, state.LastTransition.Status);
    }

    [Fact]
    public async Task Selection_output_captures_structured_hitl_request_markers()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "existing context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed($"""
                {StrategicInvestigationSelection()}

                ## HITL-Requested Non-Implementation Deliverables

                | Path Or Pattern | Source | Source Hash | Rationale |
                | --- | --- | --- | --- |
                | docs/roadmap-note.md | user | abc | Human explicitly requested the note. |
                """));
        var ledger = new NonImplementationReviewLedgerStore(new RepositoryArtifactStore(repo.Store, repo.Repository));
        var capture = new ExplicitHitlNonImplementationRequestCaptureService(ledger);

        RoadmapOutcome outcome = await StateMachineFactory.Create(
            repo,
            runtime,
            hitlRequestCapture: capture).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        NonImplementationHitlRequestEntry request = Assert.Single((await ledger.LoadOrCreateAsync()).HitlRequests);
        Assert.Equal("docs/roadmap-note.md", request.DeliverablePathOrPattern);
        Assert.Equal((string?)RoadmapArtifactPaths.Selection, request.SourceArtifactPath);
        Assert.Equal(NonImplementationHitlProvenanceKind.HitlRequested, request.HitlProvenanceKind);
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        return repo;
    }

    private static async Task SeedStaleSelectionProjectionManifestAsync(TempRepo repo)
    {
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        ProjectionProvenance provenance = new ProjectionProvenanceFactory(new ProjectionRegistry())
            .Create("SelectNextEpic", projectContext);
        ProjectionProvenance staleProvenance = provenance with
        {
            ProjectContextHash = "old-context-hash",
            CausalInputs = provenance.CausalInputs.Select(input =>
                input.Kind == ProjectionProvenance.ProjectContextInputKind
                    ? input with { Version = "old-context-hash" }
                    : input).ToArray(),
        };
        await new ProjectionManifestStore(repo.Artifacts).UpsertAsync(
            ProjectionManifestEntry.FromTrustedProvenance(
                staleProvenance,
                RoadmapHash.Sha256(repo.Read(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"])),
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                ProjectionValidationStatus.Valid,
                ProjectionFreshness.Fresh,
                null));
    }

    private static TransitionJournalRecord[] ReadJournal(TempRepo repo) =>
        repo.Read(RoadmapArtifactPaths.TransitionJournal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .ToArray();

    private static void AssertDownstreamPromptNotStarted(TempRepo repo, string prompt)
    {
        string journal = repo.Read(RoadmapArtifactPaths.TransitionJournal);
        Assert.DoesNotContain($"\"prompt\":\"{prompt}\"", journal, StringComparison.Ordinal);
    }

    private static string SelectionForOutcome(string recommendedOutcome) =>
        recommendedOutcome switch
        {
            "Select New Intermediary Epic" => NewEpicSelection(),
            "Select Split Epic" => SplitSelection(),
            "Select Existing Epic" => ExistingEpicSelection(),
            _ => throw new ArgumentOutOfRangeException(nameof(recommendedOutcome), recommendedOutcome, null),
        };

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

    private static string NewEpicSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select New Intermediary Epic |
        | Recommended Initiative | Build stale selection test epic |
        | Initiative Type | New Intermediary Epic |
        | Confidence | High |
        | Primary Reason | Exercise stale selection rejection before creation. |
        """;

    private static string SplitSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select Split Epic |
        | Recommended Initiative | Split stale selection test epic |
        | Initiative Type | Split Epic |
        | Confidence | High |
        | Primary Reason | Exercise stale selection rejection before splitting. |
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
        | Claimed Strategic Purpose | Preserve roadmap selection freshness |
        | Apparent Projection Link | Selection Freshness |

        ## Audit Disposition

        | Field | Value |
        |---|---|
        | Disposition | {{disposition}} |
        | Confidence | High |
        | Primary Reason | Audit supports {{disposition}}. |
        | Evidence Strength | Strong |
        | Recommended Next Step | {{disposition}} Epic |
        """;

    private sealed class MutatingOneShotRuntime(
        ScriptedAgentRuntime inner,
        int mutateAfterOneShotCall,
        Action mutate) : IAgentRuntime
    {
        public AgentRuntimeCapabilities Capabilities { get; } = new("test", true, true, true);

        private bool mutated;

        public int OneShotCalls => inner.OneShotCalls;

        public Task<IAgentSession> OpenSessionAsync(
            AgentSessionSpec spec,
            CancellationToken cancellationToken = default) =>
            inner.OpenSessionAsync(spec, cancellationToken);

        public async Task<AgentTurnResult> RunOneShotAsync(
            AgentSessionSpec spec,
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default)
        {
            AgentTurnResult result = await inner.RunOneShotAsync(spec, prompt, onChunk, cancellationToken);
            if (!mutated && inner.OneShotCalls == mutateAfterOneShotCall)
            {
                mutated = true;
                mutate();
            }

            return result;
        }

        public ValueTask CloseSessionAsync(IAgentSession session) =>
            inner.CloseSessionAsync(session);
    }
}
