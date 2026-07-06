using LoopRelay.Continuity.Services;
using LoopRelay.Core.Artifacts;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;
using LoopRelay.Decisions.Services;
using LoopRelay.DecisionSessions.Abstractions;
using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Services;
using LoopRelay.Reasoning.Models;
using LoopRelay.Reasoning.Projections;
using LoopRelay.Reasoning.Services;

namespace LoopRelay.DecisionSessions.Tests;

// Phase 1 pin tests for the time-correctness refactor (BuildBase + project(base, now)).
// These guard the single most important correctness rule of the Derivation Cache design:
// nothing time-dependent is ever frozen — every measuredAt-relative field is recomputed from
// the source-pure base + the injected TimeProvider on every read.
public sealed class DecisionSessionTimeProjectionTests
{
    // (a) Two-clock divergence: the SAME source evidence read at two different injected clocks must
    // produce diverging idle / cache-risk and, through the economics+policy chain, a Continue-vs-Transfer
    // divergence. A frozen-time golden would not catch a regression here — this test would.
    [Fact]
    public async Task TwoClocksDivergeOnIdleAndLifecycleDecision()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(harness.Store, new ReasoningArtifactProjectionService());
        var contextStore = new FileSystemOperationalContextProposalStore(harness.Store);

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
        await decisionRepository.SaveDecisionAsync(harness.Repository, CreateDecision(harness.Repository.Id, startedAt.AddMinutes(1)));
        await harness.Store.WriteAsync(
            Path.Combine(harness.Repository.Path, ".agents", "operational_context.md"),
            new string('c', 4096));

        // Two reads at two clocks over identical source evidence. Only the injected clock differs.
        DecisionSessionLifecycleSnapshot early =
            await EvaluatePolicyAsync(harness, decisionRepository, reasoningRepository, contextStore, startedAt.AddMinutes(2));
        DecisionSessionLifecycleSnapshot late =
            await EvaluatePolicyAsync(harness, decisionRepository, reasoningRepository, contextStore, startedAt.AddHours(48));

        // The time-dependent fields diverge: later read has strictly more idle and a higher transfer score.
        Assert.True(late.Diagnostics.Inputs.Statistics.IdleDuration > early.Diagnostics.Inputs.Statistics.IdleDuration);
        Assert.True(late.Diagnostics.Inputs.Cache.EstimatedCacheMissRisk > early.Diagnostics.Inputs.Cache.EstimatedCacheMissRisk);
        Assert.True(late.Evaluation.TransferScore > early.Evaluation.TransferScore);

        // The lifecycle Continue-vs-Transfer decision is the headline divergence: fresh evidence continues,
        // long-idle evidence tips to transfer. (Both flips would be impossible if `now` were frozen into a base.)
        Assert.Equal(DecisionSessionLifecycleDecision.Continue, early.Evaluation.Decision);
        Assert.Equal(DecisionSessionLifecycleDecision.Transfer, late.Evaluation.Decision);
    }

    // (b) Passive -> active cold-cache projection: GetProjectionAsync over a repository with NO pre-warmed
    // snapshots and a COLD derived cache must now ACTIVELY compute the snapshots (Phase 3) — so it yields
    // POPULATED metrics/economics/coherence snapshots — while keeping the warning/health array shape and
    // contents stable. This pins both hazards from the design's risk register: (1) passive->active read must
    // populate the snapshots the deleted pre-warm services used to materialize, and (2) the null-on-unavailable
    // warning semantics and the health-dimension array must not drift.
    [Fact]
    public async Task ColdCacheProjectionComputesPopulatedSnapshotsAndStableWarningAndHealthArrays()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession created = DecisionSession.Create(harness.Repository.Id, "test", DateTimeOffset.UtcNow.AddHours(-1));
        await harness.RepositoryStore.CreateAsync(harness.Repository, created);
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);

        // No pre-warm and no cache wired: the service must compute-on-read from source evidence.
        DecisionSessionObservabilityService service =
            harness.CreateObservabilityService(new FixedTimeProvider(DateTimeOffset.UtcNow));

        DecisionSessionLifecycleProjection projection = await service.GetProjectionAsync(harness.Repository.Id);
        DecisionSessionHealthAssessment health = await service.GetHealthAsync(harness.Repository.Id);

        // Active compute populates the analysis snapshots a cold passive read would have left null.
        Assert.NotNull(projection.Metrics);
        Assert.NotNull(projection.Economics);
        Assert.NotNull(projection.Coherence);
        Assert.NotNull(projection.Policy);
        Assert.NotNull(projection.TransferEligibility);

        // Registry read succeeds (no errors); the projection is valid and the warnings array is materialized
        // (null-on-unavailable semantics, not a throw), preserving the array shape consumers depend on. With
        // populated analysis the cold path no longer fabricates "snapshot is missing or unreadable" warnings.
        Assert.True(projection.Diagnostics.IsValid);
        Assert.Empty(projection.Diagnostics.Errors);
        Assert.NotNull(projection.Diagnostics.Warnings);
        Assert.DoesNotContain(projection.Diagnostics.Warnings, warning => warning.Contains("could not be read", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(projection.Sessions);
        Assert.NotNull(projection.ContinuityArtifacts);
        Assert.NotNull(projection.RecentTransfers);
        Assert.NotNull(projection.RecentRecoveryResults);

        // The seven independent health dimensions are always present and never collapse to a composite —
        // exactly the array the cold path must not reshape when pre-warm goes away.
        string[] expectedDimensions =
            ["Registry", "Analysis", "Policy", "Eligibility", "Continuity artifact", "Transfer", "Recovery"];
        foreach (string dimension in expectedDimensions)
        {
            Assert.Contains(health.Dimensions, candidate => candidate.Name == dimension);
        }

        Assert.DoesNotContain(health.Dimensions, candidate => candidate.Name == "Composite");
    }

    private static async Task<DecisionSessionLifecycleSnapshot> EvaluatePolicyAsync(
        DecisionSessionTestHarness harness,
        InMemoryDecisionRepository decisionRepository,
        FileSystemReasoningRepository reasoningRepository,
        FileSystemOperationalContextProposalStore contextStore,
        DateTimeOffset clock)
    {
        var timeProvider = new FixedTimeProvider(clock);
        var artifactService = new ArtifactService(harness.Store);
        var evidenceReader = new DecisionSessionEvidenceReader(decisionRepository, reasoningRepository, contextStore, artifactService);
        var metricsService = new DecisionSessionMetricsService(
            harness.RepositoryService,
            harness.Registry,
            harness.RepositoryStore,
            evidenceReader,
            new DeterministicTokenEstimator(),
            timeProvider);
        var economicsService = new DecisionSessionEconomicsService(
            harness.RepositoryService,
            harness.RepositoryStore,
            metricsService,
            // Push thresholds low so growth/idle pressure resolves into a meaningful transfer signal at scale.
            new DecisionSessionEconomicsOptions(
                LargeContextTokenThreshold: 1_000,
                LargeContextByteThreshold: 4_000),
            timeProvider);
        var policy = new DecisionSessionLifecyclePolicy(
            harness.RepositoryService,
            harness.RepositoryStore,
            metricsService,
            economicsService,
            // Coherence is held fixed (clock-independent here) so the only moving input is time-driven.
            new FixedCoherenceService(CreateNeutralCoherence(harness.Repository.Id, clock)),
            new DecisionSessionLifecyclePolicyOptions(),
            timeProvider);

        return await policy.EvaluateAsync(harness.Repository.Id);
    }

    private static DecisionSessionCoherenceSnapshot CreateNeutralCoherence(Guid repositoryId, DateTimeOffset generatedAt)
    {
        var coherence = new DecisionSessionCoherence(0.50m, 0.40m, 0.50m, 0.50m, 0.40m);
        var metrics = new DecisionSessionMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0, generatedAt, generatedAt);
        var statistics = new DecisionSessionStatistics(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 0m, 0m);
        var cache = new DecisionSessionCacheMetrics(TimeSpan.FromHours(1), 0m, null);
        var economics = new DecisionSessionEconomics(0m, 0m, 0m, 0m, 0m, 0m, 0m);
        var diagnostics = new DecisionSessionCoherenceDiagnostics(
            repositoryId,
            generatedAt,
            new DecisionSessionCoherenceInputs(metrics, statistics, cache, economics, 0, 0, 0, 0, 0, 0),
            new FragmentationAssessment(0.40m, 0m, 0m, 0m),
            new DensityAssessment(0.50m, 0.50m, 0, 0),
            new ContinuityQualityAssessment(0.50m, 0m, 0m, 0m, 0m),
            new TransferPressureAssessment(0.40m, 0m, 0m, 0m, 0m, 0m),
            [],
            []);
        return new DecisionSessionCoherenceSnapshot(repositoryId, coherence, diagnostics, generatedAt);
    }

    private static Decision CreateDecision(Guid repositoryId, DateTimeOffset timestamp)
    {
        return new Decision(
            new DecisionId("DEC-0001"),
            DecisionState.Open,
            DecisionClassification.Architectural,
            "Use decision sessions",
            "Decision sessions carry governance continuity.",
            new DecisionMetadata(repositoryId, timestamp, timestamp),
            null,
            [],
            [],
            [new DecisionHistoryEntry(timestamp, "Created", null, "Open", null, [])]);
    }

    private sealed class FixedCoherenceService(DecisionSessionCoherenceSnapshot snapshot) : IDecisionSessionCoherenceService
    {
        public Task<DecisionSessionCoherenceSnapshot> GetCoherenceAsync(Guid repositoryId) => Task.FromResult(snapshot);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
