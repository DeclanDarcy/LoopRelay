using System.Text.Json;
using LoopRelay.Infrastructure.Services.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;
using LoopRelay.Orchestration.Services.Hitl;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.State;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;
using LoopRelay.Roadmap.Cli.Services.TransitionState;
using LoopRelay.Roadmap.Cli.Tests.Services.State;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using DecisionLedgerStore = LoopRelay.Roadmap.Cli.Services.Decisions.DecisionLedgerStore;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.Services.State.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests.Services.TransitionCoordination;

public sealed class ActiveEpicPromotionCoordinatorTests
{
    [Fact]
    public async Task PromoteAsync_promoted_candidate_writes_active_epic_lifecycle_hitl_journal_and_completed_state()
    {
        using var repo = new TempRepo();
        var ledger = new NonImplementationReviewLedgerStore(new RepositoryArtifactStore(repo.Store, repo.Repository));
        var capture = new HitlArtifactCapture(new ExplicitHitlNonImplementationRequestCaptureService(ledger));
        CoordinatorHarness harness = CreateHarness(repo, capture);
        string epic = WithHitlRequest(RoadmapSamples.ValidEpic("Promoted Epic", "EPIC-COORD"));
        PromptTransitionCompletion completion = Completion("CreateNewEpic", epic);

        ArtifactPromotionResult result = await harness.Coordinator.PromoteAsync(
            RoadmapState.NewEpicProposed,
            "CreateNewEpic",
            RoadmapArtifactPaths.ProjectionPaths["CreateNewEpic"],
            completion,
            "Caller supplied note.");

        Assert.True(result.Promoted);
        Assert.Equal((string?)RoadmapArtifactPaths.ActiveEpic, result.TargetPath);
        Assert.Equal(epic, repo.Read(RoadmapArtifactPaths.ActiveEpic));

        ArtifactLifecycleEntry lifecycle = Assert.Single(
            await harness.LifecycleStore.LoadAsync(),
            entry => entry.Path == RoadmapArtifactPaths.ActiveEpic);
        Assert.Equal(ArtifactLifecycleState.Ready, lifecycle.State);
        Assert.Equal("Caller supplied note.", lifecycle.Notes);

        NonImplementationHitlRequestEntry request = Assert.Single((await ledger.LoadOrCreateAsync()).HitlRequests);
        Assert.Equal("docs/active-epic-note.md", request.DeliverablePathOrPattern);
        Assert.Equal((string?)RoadmapArtifactPaths.ActiveEpic, request.SourceArtifactPath);
        Assert.Equal(NonImplementationHitlProvenanceKind.HitlRequested, request.HitlProvenanceKind);

        TransitionJournalRecord journal = Assert.Single(ReadJournal(repo));
        Assert.Equal("ArtifactPromoted", journal.Event);
        Assert.Equal(completion.CorrelationId, journal.CorrelationId);
        Assert.Equal(RoadmapState.NewEpicProposed, journal.PreviousState);
        Assert.Equal(RoadmapState.ActiveEpicReady, journal.AttemptedState);
        Assert.Equal("CreateNewEpic", journal.Prompt);
        Assert.Equal((string?)RoadmapArtifactPaths.ProjectionPaths["CreateNewEpic"], journal.Projection);
        Assert.Equal("ArtifactPromotionService", journal.PromptContractKey);
        Assert.Equal([RoadmapArtifactPaths.ActiveEpic], journal.OutputPaths);
        Assert.Equal("Promoted", journal.Result);
        Assert.Equal("Active epic promoted", journal.ParserDecision);
        Assert.Null(journal.ErrorMessage);
        Assert.NotNull(journal.InputSnapshot);
        Assert.Equal(completion.InputSnapshot.SnapshotHash, journal.InputSnapshot.SnapshotHash);
        Assert.Equal(completion.InputSnapshot.ToInputArtifactHashes(), journal.InputArtifactHashes);

        RoadmapStateDocument state = (await harness.StateStore.LoadAsync())!;
        Assert.Equal(RoadmapState.ActiveEpicReady, state.CurrentState);
        Assert.Equal(TransitionStatus.Completed, state.LastTransition.Status);
        Assert.Equal(RoadmapState.NewEpicProposed, state.LastTransition.From);
        Assert.Equal(RoadmapState.ActiveEpicReady, state.LastTransition.To);
        Assert.Equal("CreateNewEpic", state.LastTransition.Prompt);
        Assert.Equal((string?)RoadmapArtifactPaths.ActiveEpic, state.LastTransition.Output);
        Assert.Equal("Artifact Promoted", state.LastTransition.Decision);
        Assert.Equal(["GenerateMilestoneDeepDives"], state.NextValidTransitions);
    }

    [Theory]
    [MemberData(nameof(RejectedCandidates))]
    public async Task PromoteAsync_rejected_candidate_preserves_exact_evidence_and_pauses(
        string candidate,
        string expectedStatus,
        string expectedDecision)
    {
        using var repo = new TempRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD", "Ready");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, existing);
        CoordinatorHarness harness = CreateHarness(repo, new HitlArtifactCapture(null));
        PromptTransitionCompletion completion = Completion("RealignEpic", candidate);

        ArtifactPromotionResult result = await harness.Coordinator.PromoteAsync(
            RoadmapState.RealignEpic,
            "RealignEpic",
            RoadmapArtifactPaths.ProjectionPaths["RealignEpic"],
            completion);

        Assert.False(result.Promoted);
        Assert.Equal(expectedStatus, result.Status.ToString());
        Assert.Equal(existing, repo.Read(RoadmapArtifactPaths.ActiveEpic));

        RoadmapStateDocument state = (await harness.StateStore.LoadAsync())!;
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal($"{RoadmapArtifactPaths.BlockerEvidenceDirectory}/active-epic-promotion.0001.md", evidencePath);
        Assert.Equal(candidate, repo.Read(evidencePath));
        Assert.Equal(RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal(RoadmapState.RealignEpic, state.LastTransition.From);
        Assert.Equal(RoadmapState.ActiveEpicReady, state.LastTransition.To);
        Assert.Equal("RealignEpic", state.LastTransition.Prompt);
        Assert.Equal((string?)RoadmapArtifactPaths.ProjectionPaths["RealignEpic"], state.LastTransition.Projection);
        Assert.Equal(evidencePath, state.LastTransition.Output);
        Assert.Equal(expectedDecision, state.LastTransition.Decision);
        Assert.Equal("ResolveArtifactPromotionBlocker", state.TransitionIntent.Intent);
        Assert.Equal(RoadmapState.EvidenceBlocked, state.TransitionIntent.DispatchState);
        Assert.Equal(["Resolve blocker and rerun"], state.NextValidTransitions);
        BlockerRow blocker = Assert.Single(state.Blockers);
        Assert.Equal(result.Reason, blocker.Blocker);
        Assert.Equal($"Review {evidencePath} and rerun the roadmap CLI after resolving the blocker.", blocker.RequiredNextStep);

        ArtifactLifecycleEntry evidence = Assert.Single(
            await harness.LifecycleStore.LoadAsync(),
            entry => entry.Path == evidencePath);
        Assert.Equal(ArtifactLifecycleState.Blocked, evidence.State);
        Assert.Equal(result.Reason, evidence.Notes);

        TransitionJournalRecord journal = Assert.Single(ReadJournal(repo));
        Assert.Equal("ArtifactPromotionBlocked", journal.Event);
        Assert.Equal(completion.CorrelationId, journal.CorrelationId);
        Assert.Equal(RoadmapState.RealignEpic, journal.PreviousState);
        Assert.Equal(RoadmapState.ActiveEpicReady, journal.AttemptedState);
        Assert.Equal("ArtifactPromotionService", journal.PromptContractKey);
        Assert.Equal([evidencePath], journal.OutputPaths);
        Assert.Equal(expectedStatus, journal.Result);
        Assert.Equal(expectedDecision, journal.ParserDecision);
        Assert.Equal(result.Reason, journal.ErrorMessage);
        Assert.NotNull(journal.InputSnapshot);
        Assert.Equal(completion.InputSnapshot.SnapshotHash, journal.InputSnapshot.SnapshotHash);
    }

    public static IEnumerable<object[]> RejectedCandidates()
    {
        yield return
        [
            """
            # Epic Realignment Blocked

            ## Reason

            The audit does not support safe realignment.
            """,
            "Blocked",
            "Artifact Promotion Blocked",
        ];
        yield return
        [
            "There is not enough information to promote an active epic.",
            "Ambiguous",
            "Artifact Promotion Ambiguous",
        ];
        yield return
        [
            """
            # Epic: Missing Structure

            ## Epic Metadata

            | Field | Value |
            |---|---|
            | Epic ID | EPIC-BAD |
            | Status | Authored |
            """,
            "StructurallyInvalid",
            "Artifact Promotion Invalid",
        ];
    }

    private static CoordinatorHarness CreateHarness(TempRepo repo, HitlArtifactCapture capture)
    {
        var manifestStore = new ProjectionManifestStore(repo.Artifacts);
        var stateStore = new RoadmapStateStore(repo.Artifacts);
        var decisionLedger = new DecisionLedgerStore(repo.Artifacts);
        var journalStore = new TransitionJournalStore(repo.Artifacts);
        var lifecycleStore = new ArtifactLifecycleStore(repo.Artifacts);
        var transitionPersistence = new RoadmapTransitionPersistence(
            repo.Artifacts,
            manifestStore,
            stateStore,
            decisionLedger,
            journalStore);
        var promotionService = new ArtifactPromotionService(repo.Artifacts, lifecycleStore);
        return new CoordinatorHarness(
            new ActiveEpicPromotionCoordinator(
                promotionService,
                capture,
                journalStore,
                transitionPersistence),
            stateStore,
            lifecycleStore);
    }

    private static PromptTransitionCompletion Completion(string prompt, string output)
    {
        string projectionPath = RoadmapArtifactPaths.ProjectionPaths[prompt];
        TransitionInputSnapshot snapshot = Snapshot(prompt, projectionPath);
        return new PromptTransitionCompletion(
            "correlation-001",
            DateTimeOffset.Parse("2026-01-05T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-05T00:00:01Z"),
            42,
            output,
            snapshot);
    }

    private static TransitionInputSnapshot Snapshot(string prompt, string projectionPath)
    {
        string projectionHash = RoadmapHash.Sha256($"projection:{prompt}");
        string selectionHash = RoadmapHash.Sha256("selection");
        return new TransitionInputSnapshot(
            prompt,
            new TransitionProjectionIdentity(prompt, projectionPath, projectionHash),
            [
                new TransitionArtifactInput(
                    projectionPath,
                    TransitionInputRole.Projection,
                    Required: true,
                    TransitionInputPresence.Present,
                    projectionHash),
                new TransitionArtifactInput(
                    RoadmapArtifactPaths.Selection,
                    TransitionInputRole.Selection,
                    Required: true,
                    TransitionInputPresence.Present,
                    selectionHash),
            ],
            RoadmapHash.Sha256("rendered context"),
            RoadmapHash.Sha256("secondary input"),
            RoadmapHash.Sha256($"snapshot:{prompt}"));
    }

    private static TransitionJournalRecord[] ReadJournal(TempRepo repo) =>
        repo.Read(RoadmapArtifactPaths.TransitionJournal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .ToArray();

    private static string WithHitlRequest(string epic) => epic + """

        ## HITL-Requested Non-Implementation Deliverables

        | Path Or Pattern | Source | Source Hash | Rationale |
        | --- | --- | --- | --- |
        | docs/active-epic-note.md | user | abc | Human requested the active epic note. |
        """;

    private sealed record CoordinatorHarness(
        ActiveEpicPromotionCoordinator Coordinator,
        RoadmapStateStore StateStore,
        ArtifactLifecycleStore LifecycleStore);
}
