using System.Text.Json;
using LoopRelay.Infrastructure.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class ActiveEpicPromotionCoordinatorTests
{
    [Fact]
    public async Task PromoteAsync_promoted_candidate_writes_active_epic_lifecycle_hitl_journal_and_completed_state()
    {
        using var repo = new TempRepo();
        var ledger = new NonImplementationReviewLedgerStore(new RepositoryArtifactStore(repo.Store, repo.Repository));
        var capture = new Cli.HitlArtifactCapture(new ExplicitHitlNonImplementationRequestCaptureService(ledger));
        CoordinatorHarness harness = CreateHarness(repo, capture);
        string epic = WithHitlRequest(RoadmapSamples.ValidEpic("Promoted Epic", "EPIC-COORD"));
        Cli.PromptTransitionCompletion completion = Completion("CreateNewEpic", epic);

        Cli.ArtifactPromotionResult result = await harness.Coordinator.PromoteAsync(
            Cli.RoadmapState.NewEpicProposed,
            "CreateNewEpic",
            Cli.RoadmapArtifactPaths.ProjectionPaths["CreateNewEpic"],
            completion,
            "Caller supplied note.");

        Assert.True(result.Promoted);
        Assert.Equal(Cli.RoadmapArtifactPaths.ActiveEpic, result.TargetPath);
        Assert.Equal(epic, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));

        Cli.ArtifactLifecycleEntry lifecycle = Assert.Single(
            await harness.LifecycleStore.LoadAsync(),
            entry => entry.Path == Cli.RoadmapArtifactPaths.ActiveEpic);
        Assert.Equal(Cli.ArtifactLifecycleState.Ready, lifecycle.State);
        Assert.Equal("Caller supplied note.", lifecycle.Notes);

        NonImplementationHitlRequestEntry request = Assert.Single((await ledger.LoadOrCreateAsync()).HitlRequests);
        Assert.Equal("docs/active-epic-note.md", request.DeliverablePathOrPattern);
        Assert.Equal(Cli.RoadmapArtifactPaths.ActiveEpic, request.SourceArtifactPath);
        Assert.Equal(NonImplementationHitlProvenanceKind.HitlRequested, request.HitlProvenanceKind);

        Cli.TransitionJournalRecord journal = Assert.Single(ReadJournal(repo));
        Assert.Equal("ArtifactPromoted", journal.Event);
        Assert.Equal(completion.CorrelationId, journal.CorrelationId);
        Assert.Equal(Cli.RoadmapState.NewEpicProposed, journal.PreviousState);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, journal.AttemptedState);
        Assert.Equal("CreateNewEpic", journal.Prompt);
        Assert.Equal(Cli.RoadmapArtifactPaths.ProjectionPaths["CreateNewEpic"], journal.Projection);
        Assert.Equal("ArtifactPromotionService", journal.PromptContractKey);
        Assert.Equal([Cli.RoadmapArtifactPaths.ActiveEpic], journal.OutputPaths);
        Assert.Equal("Promoted", journal.Result);
        Assert.Equal("Active epic promoted", journal.ParserDecision);
        Assert.Null(journal.ErrorMessage);
        Assert.NotNull(journal.InputSnapshot);
        Assert.Equal(completion.InputSnapshot.SnapshotHash, journal.InputSnapshot.SnapshotHash);
        Assert.Equal(completion.InputSnapshot.ToInputArtifactHashes(), journal.InputArtifactHashes);

        Cli.RoadmapStateDocument state = (await harness.StateStore.LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Completed, state.LastTransition.Status);
        Assert.Equal(Cli.RoadmapState.NewEpicProposed, state.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, state.LastTransition.To);
        Assert.Equal("CreateNewEpic", state.LastTransition.Prompt);
        Assert.Equal(Cli.RoadmapArtifactPaths.ActiveEpic, state.LastTransition.Output);
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
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
        CoordinatorHarness harness = CreateHarness(repo, new Cli.HitlArtifactCapture(null));
        Cli.PromptTransitionCompletion completion = Completion("RealignEpic", candidate);

        Cli.ArtifactPromotionResult result = await harness.Coordinator.PromoteAsync(
            Cli.RoadmapState.RealignEpic,
            "RealignEpic",
            Cli.RoadmapArtifactPaths.ProjectionPaths["RealignEpic"],
            completion);

        Assert.False(result.Promoted);
        Assert.Equal(expectedStatus, result.Status.ToString());
        Assert.Equal(existing, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));

        Cli.RoadmapStateDocument state = (await harness.StateStore.LoadAsync())!;
        string evidencePath = Assert.Single(state.TransitionIntent.EvidencePaths);
        Assert.Equal($"{Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory}/active-epic-promotion.0001.md", evidencePath);
        Assert.Equal(candidate, repo.Read(evidencePath));
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.CurrentState);
        Assert.Equal(Cli.TransitionStatus.Paused, state.LastTransition.Status);
        Assert.Equal(Cli.RoadmapState.RealignEpic, state.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, state.LastTransition.To);
        Assert.Equal("RealignEpic", state.LastTransition.Prompt);
        Assert.Equal(Cli.RoadmapArtifactPaths.ProjectionPaths["RealignEpic"], state.LastTransition.Projection);
        Assert.Equal(evidencePath, state.LastTransition.Output);
        Assert.Equal(expectedDecision, state.LastTransition.Decision);
        Assert.Equal("ResolveArtifactPromotionBlocker", state.TransitionIntent.Intent);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, state.TransitionIntent.DispatchState);
        Assert.Equal(["Resolve blocker and rerun"], state.NextValidTransitions);
        Cli.BlockerRow blocker = Assert.Single(state.Blockers);
        Assert.Equal(result.Reason, blocker.Blocker);
        Assert.Equal($"Review {evidencePath} and rerun the roadmap CLI after resolving the blocker.", blocker.RequiredNextStep);

        Cli.ArtifactLifecycleEntry evidence = Assert.Single(
            await harness.LifecycleStore.LoadAsync(),
            entry => entry.Path == evidencePath);
        Assert.Equal(Cli.ArtifactLifecycleState.Blocked, evidence.State);
        Assert.Equal(result.Reason, evidence.Notes);

        Cli.TransitionJournalRecord journal = Assert.Single(ReadJournal(repo));
        Assert.Equal("ArtifactPromotionBlocked", journal.Event);
        Assert.Equal(completion.CorrelationId, journal.CorrelationId);
        Assert.Equal(Cli.RoadmapState.RealignEpic, journal.PreviousState);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, journal.AttemptedState);
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

    private static CoordinatorHarness CreateHarness(TempRepo repo, Cli.HitlArtifactCapture capture)
    {
        var manifestStore = new Cli.ProjectionManifestStore(repo.Artifacts);
        var stateStore = new RoadmapStateStore(repo.Artifacts);
        var decisionLedger = new DecisionLedgerStore(repo.Artifacts);
        var journalStore = new Cli.TransitionJournalStore(repo.Artifacts);
        var lifecycleStore = new Cli.ArtifactLifecycleStore(repo.Artifacts);
        var transitionPersistence = new Cli.RoadmapTransitionPersistence(
            repo.Artifacts,
            manifestStore,
            stateStore,
            decisionLedger,
            journalStore);
        var promotionService = new Cli.ArtifactPromotionService(repo.Artifacts, lifecycleStore);
        return new CoordinatorHarness(
            new Cli.ActiveEpicPromotionCoordinator(
                promotionService,
                capture,
                journalStore,
                transitionPersistence),
            stateStore,
            lifecycleStore);
    }

    private static Cli.PromptTransitionCompletion Completion(string prompt, string output)
    {
        string projectionPath = Cli.RoadmapArtifactPaths.ProjectionPaths[prompt];
        Cli.TransitionInputSnapshot snapshot = Snapshot(prompt, projectionPath);
        return new Cli.PromptTransitionCompletion(
            "correlation-001",
            DateTimeOffset.Parse("2026-01-05T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-05T00:00:01Z"),
            42,
            output,
            snapshot);
    }

    private static Cli.TransitionInputSnapshot Snapshot(string prompt, string projectionPath)
    {
        string projectionHash = Cli.RoadmapHash.Sha256($"projection:{prompt}");
        string selectionHash = Cli.RoadmapHash.Sha256("selection");
        return new Cli.TransitionInputSnapshot(
            prompt,
            new Cli.TransitionProjectionIdentity(prompt, projectionPath, projectionHash),
            [
                new Cli.TransitionArtifactInput(
                    projectionPath,
                    Cli.TransitionInputRole.Projection,
                    Required: true,
                    Cli.TransitionInputPresence.Present,
                    projectionHash),
                new Cli.TransitionArtifactInput(
                    Cli.RoadmapArtifactPaths.Selection,
                    Cli.TransitionInputRole.Selection,
                    Required: true,
                    Cli.TransitionInputPresence.Present,
                    selectionHash),
            ],
            Cli.RoadmapHash.Sha256("rendered context"),
            Cli.RoadmapHash.Sha256("secondary input"),
            Cli.RoadmapHash.Sha256($"snapshot:{prompt}"));
    }

    private static Cli.TransitionJournalRecord[] ReadJournal(TempRepo repo) =>
        repo.Read(Cli.RoadmapArtifactPaths.TransitionJournal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<Cli.TransitionJournalRecord>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .ToArray();

    private static string WithHitlRequest(string epic) => epic + """

        ## HITL-Requested Non-Implementation Deliverables

        | Path Or Pattern | Source | Source Hash | Rationale |
        | --- | --- | --- | --- |
        | docs/active-epic-note.md | user | abc | Human requested the active epic note. |
        """;

    private sealed record CoordinatorHarness(
        Cli.ActiveEpicPromotionCoordinator Coordinator,
        RoadmapStateStore StateStore,
        Cli.ArtifactLifecycleStore LifecycleStore);
}
