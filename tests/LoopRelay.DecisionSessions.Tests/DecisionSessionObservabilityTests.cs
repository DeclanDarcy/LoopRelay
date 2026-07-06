using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Persistence;
using LoopRelay.DecisionSessions.Primitives;
using LoopRelay.DecisionSessions.Services;

namespace LoopRelay.DecisionSessions.Tests;

public sealed class DecisionSessionObservabilityTests
{
    [Fact]
    public async Task ProjectionComposesCurrentLifecycleEvidence()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow.AddMinutes(-10);
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-1));
        DecisionSessionFixtures fixtures = CreateFixtures(harness.Repository.Id, active, now);
        await harness.RepositoryStore.WriteContinuityArtifactAsync(harness.Repository, fixtures.Artifact);
        await harness.RepositoryStore.WriteTransferAsync(harness.Repository, fixtures.Transfer);
        await harness.RepositoryStore.WriteRecoveryResultAsync(harness.Repository, fixtures.Recovery);
        DecisionSessionObservabilityService service = CreateService(harness, fixtures, now.AddMinutes(5));

        DecisionSessionLifecycleProjection projection = await service.GetProjectionAsync(harness.Repository.Id);

        Assert.Equal(harness.Repository.Id, projection.RepositoryId);
        Assert.Equal(active.Id, projection.ActiveSession?.Id);
        Assert.NotNull(projection.Metrics);
        Assert.NotNull(projection.Size);
        Assert.Equal(fixtures.Metrics.Metrics.EstimatedTokenCount, projection.Size.EstimatedTokenCount);
        Assert.Equal(fixtures.Metrics.Metrics.ContextByteSize, projection.Size.ContextByteSize);
        Assert.Equal(fixtures.Metrics.Metrics.ReasoningEventCount, projection.Size.ReasoningEventCount);
        Assert.Equal(fixtures.Metrics.Metrics.DecisionCount, projection.Size.DecisionCount);
        Assert.NotNull(projection.Economics);
        Assert.NotNull(projection.Coherence);
        Assert.NotNull(projection.Policy);
        Assert.NotNull(projection.TransferEligibility);
        Assert.Equal(fixtures.Artifact.ArtifactId, projection.CurrentContinuityArtifact?.ArtifactId);
        DecisionSessionContinuityArtifactProjection artifactProjection = Assert.Single(projection.ContinuityArtifacts);
        Assert.Equal(fixtures.Artifact.ArtifactId, artifactProjection.ArtifactId);
        Assert.Equal(fixtures.Artifact.ContinuityFingerprint, artifactProjection.ContinuityFingerprint);
        Assert.Equal(fixtures.Artifact.SourceSessionId, artifactProjection.SourceSessionId);
        Assert.Single(artifactProjection.DecisionReferences);
        Assert.Single(artifactProjection.ReasoningReferences);
        Assert.Single(artifactProjection.OperationalContextReferences);
        Assert.Single(projection.RecentTransfers);
        Assert.Contains(projection.RecentTransferEvents, transferEvent => transferEvent.EventType == DecisionSessionTransferEventType.Completed);
        DecisionSessionTransferEventProjection transferProjection = Assert.Single(projection.TransferEvents);
        Assert.Equal(fixtures.Transfer.TransferId, transferProjection.TransferId);
        Assert.Equal(fixtures.Transfer.SourceSessionId, transferProjection.SourceSessionId);
        Assert.Equal(fixtures.Transfer.TargetSessionId, transferProjection.TargetSessionId);
        Assert.Equal(fixtures.Transfer.StartedAt, transferProjection.StartedAt);
        Assert.Equal(fixtures.Transfer.CompletedAt, transferProjection.CompletedAt);
        Assert.True(transferProjection.Succeeded);
        Assert.Equal(fixtures.Policy.Evaluation.Decision, transferProjection.PolicyDecision);
        Assert.Equal(fixtures.Policy.Evaluation.ReuseScore, transferProjection.ReuseScore);
        Assert.Equal(fixtures.Policy.Evaluation.TransferScore, transferProjection.TransferScore);
        Assert.Equal(fixtures.Metrics.Metrics.EstimatedTokenCount, transferProjection.EstimatedTokenCount);
        Assert.Equal(fixtures.Eligibility.Eligibility.Status, transferProjection.EligibilityStatus);
        Assert.Equal(fixtures.Artifact.ArtifactId, transferProjection.ContinuityArtifactId);
        Assert.Single(projection.RecentRecoveryResults);
        Assert.True(projection.Diagnostics.IsValid);
        Assert.Empty(projection.Diagnostics.Errors);
    }

    [Fact]
    public async Task HistoryReconstructsLifecycleEventsFromDurableEvidence()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow.AddMinutes(-10);
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-1));
        DecisionSessionFixtures fixtures = CreateFixtures(harness.Repository.Id, active, now);
        await harness.RepositoryStore.WriteContinuityArtifactAsync(harness.Repository, fixtures.Artifact);
        await harness.RepositoryStore.WriteTransferAsync(harness.Repository, fixtures.Transfer);
        await harness.RepositoryStore.WriteRecoveryResultAsync(harness.Repository, fixtures.Recovery);
        DecisionSessionObservabilityService service = CreateService(harness, fixtures, now.AddMinutes(5));

        DecisionSessionLifecycleHistory history = await service.GetHistoryAsync(harness.Repository.Id);

        Assert.Contains(history.Events, lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.Created);
        Assert.Contains(history.Events, lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.Activated);
        Assert.Contains(history.Events, lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.AnalysisCaptured);
        Assert.Contains(history.Events, lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.PolicyEvaluated);
        Assert.Contains(history.Events, lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.TransferEligibilityEvaluated);
        Assert.Contains(history.Events, lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.ContinuityArtifactCreated);
        Assert.Contains(history.Events, lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.TransferStarted);
        Assert.Contains(history.Events, lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.TransferCompleted);
        Assert.Contains(history.Events, lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.ReplacementCreated);
        Assert.Contains(history.Events, lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.Recovered);
        Assert.Equal(history.Events.OrderBy(lifecycleEvent => lifecycleEvent.OccurredAt).Select(lifecycleEvent => lifecycleEvent.OccurredAt), history.Events.Select(lifecycleEvent => lifecycleEvent.OccurredAt));
    }

    [Fact]
    public async Task InfluenceTraceContainsLifecycleDecisionSignals()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow.AddMinutes(-10);
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-1));
        DecisionSessionFixtures fixtures = CreateFixtures(harness.Repository.Id, active, now);
        await harness.RepositoryStore.WriteContinuityArtifactAsync(harness.Repository, fixtures.Artifact);
        await harness.RepositoryStore.WriteTransferAsync(harness.Repository, fixtures.Transfer);
        await harness.RepositoryStore.WriteRecoveryResultAsync(harness.Repository, fixtures.Recovery);
        DecisionSessionObservabilityService service = CreateService(harness, fixtures, now.AddMinutes(5));

        DecisionSessionInfluenceTrace trace = await service.GetInfluenceTraceAsync(harness.Repository.Id);

        Assert.Equal(harness.Repository.Id, trace.RepositoryId);
        Assert.Equal(active.Id, trace.ActiveSessionId);
        Assert.Equal(DecisionSessionLifecycleDecision.Transfer, trace.PolicyDecision);
        Assert.Equal(DecisionSessionTransferEligibilityStatus.Eligible, trace.TransferEligibilityStatus);
        Assert.Contains(trace.Signals, signal => signal.Category == "Metrics");
        Assert.Contains(trace.Signals, signal => signal.Category == "Cache TTL");
        Assert.Contains(trace.Signals, signal => signal.Category == "Cache miss risk");
        Assert.Contains(trace.Signals, signal => signal.Category == "Economics" && signal.Name == "Reuse value");
        Assert.Contains(trace.Signals, signal => signal.Category == "Economics" && signal.Name == "Transfer value");
        Assert.Contains(trace.Signals, signal => signal.Category == "Coherence" && signal.Name == "Coherence score");
        Assert.Contains(trace.Signals, signal => signal.Category == "Policy" && signal.ContributingFactors.Contains("transfer pressure"));
        Assert.Contains(trace.Signals, signal => signal.Category == "Eligibility" && signal.Value == DecisionSessionTransferEligibilityStatus.Eligible.ToString());
        Assert.Contains(trace.Signals, signal => signal.Category == "Continuity artifact" && signal.Value == fixtures.Artifact.ArtifactId);
        Assert.Contains(trace.Signals, signal => signal.Category == "Transfer" && signal.Name == fixtures.Transfer.TransferId);
        Assert.Contains(trace.Signals, signal => signal.Category == "Recovery" && signal.Name == fixtures.Recovery.RecoveryId);
    }

    [Fact]
    public async Task HealthAssessmentReportsIndependentSubsystemDimensions()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow.AddMinutes(-10);
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-1));
        DecisionSessionFixtures fixtures = CreateFixtures(harness.Repository.Id, active, now);
        await harness.RepositoryStore.WriteContinuityArtifactAsync(harness.Repository, fixtures.Artifact);
        await harness.RepositoryStore.WriteTransferAsync(harness.Repository, fixtures.Transfer);
        await harness.RepositoryStore.WriteRecoveryResultAsync(harness.Repository, fixtures.Recovery);
        DecisionSessionObservabilityService service = CreateService(harness, fixtures, now.AddMinutes(5));

        DecisionSessionHealthAssessment health = await service.GetHealthAsync(harness.Repository.Id);

        Assert.Equal(harness.Repository.Id, health.RepositoryId);
        Assert.Equal(harness.Repository.Id, health.InfluenceTrace.RepositoryId);
        Assert.Contains(health.Dimensions, dimension => dimension.Name == "Registry" && dimension.Status == DecisionSessionHealthStatus.Healthy);
        Assert.Contains(health.Dimensions, dimension => dimension.Name == "Analysis" && dimension.Status == DecisionSessionHealthStatus.Healthy);
        Assert.Contains(health.Dimensions, dimension => dimension.Name == "Policy" && dimension.Status == DecisionSessionHealthStatus.Healthy);
        Assert.Contains(health.Dimensions, dimension => dimension.Name == "Eligibility" && dimension.Status == DecisionSessionHealthStatus.Healthy);
        Assert.Contains(health.Dimensions, dimension => dimension.Name == "Continuity artifact" && dimension.Status == DecisionSessionHealthStatus.Healthy);
        Assert.Contains(health.Dimensions, dimension => dimension.Name == "Transfer" && dimension.Status == DecisionSessionHealthStatus.Healthy);
        Assert.Contains(health.Dimensions, dimension => dimension.Name == "Recovery" && dimension.Status == DecisionSessionHealthStatus.Healthy);
        Assert.DoesNotContain(health.Dimensions, dimension => dimension.Name == "Composite");
    }

    [Fact]
    public async Task ProjectionReportsCorruptDerivedSnapshotAsDiagnostics()
    {
        // Phase 3 retarget (refactor-lazy-sqlite.md): metrics is computed on read, not read from a pre-warmed
        // file, so an "unreadable" snapshot is modelled as the metrics provider raising a read failure. The
        // invariant is unchanged: a failed metrics read yields a null Metrics snapshot plus the same
        // "metrics snapshot could not be read" warning, and the projection stays valid.
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow.AddMinutes(-10);
        await CreateActiveSessionAsync(harness, now.AddHours(-1));
        var service = new DecisionSessionObservabilityService(
            harness.RepositoryService,
            harness.RepositoryStore,
            new FixedTimeProvider(now),
            new StubMetricsService(null, new System.Text.Json.JsonException("Existing metrics snapshot JSON is invalid.")),
            new StubEconomicsService(null),
            new StubCoherenceService(null),
            new StubLifecyclePolicy(null),
            new StubTransferEligibilityService(null));

        DecisionSessionLifecycleProjection projection = await service.GetProjectionAsync(harness.Repository.Id);

        Assert.True(projection.Diagnostics.IsValid);
        Assert.Null(projection.Metrics);
        Assert.Contains(projection.Diagnostics.Warnings, warning => warning.Contains("metrics snapshot could not be read", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<DecisionSession> CreateActiveSessionAsync(DecisionSessionTestHarness harness, DateTimeOffset createdAt)
    {
        DecisionSession created = DecisionSession.Create(harness.Repository.Id, "test", createdAt);
        await harness.RepositoryStore.CreateAsync(harness.Repository, created);
        return await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
    }

    // Phase 3: the observability service computes snapshots on read via the analysis providers. The fixture
    // snapshots are fed through stub providers (the new read seam) instead of pre-warmed files (the old seam),
    // preserving every downstream invariant the original file-backed tests asserted.
    private static DecisionSessionObservabilityService CreateService(
        DecisionSessionTestHarness harness,
        DecisionSessionFixtures fixtures,
        DateTimeOffset now)
    {
        return new DecisionSessionObservabilityService(
            harness.RepositoryService,
            harness.RepositoryStore,
            new FixedTimeProvider(now),
            new StubMetricsService(fixtures.Metrics),
            new StubEconomicsService(fixtures.Economics),
            new StubCoherenceService(fixtures.Coherence),
            new StubLifecyclePolicy(fixtures.Policy),
            new StubTransferEligibilityService(fixtures.Eligibility));
    }

    private static DecisionSessionFixtures CreateFixtures(Guid repositoryId, DecisionSession active, DateTimeOffset now)
    {
        var metrics = new DecisionSessionMetrics(100, 400, 1, 1, 1, 1, 0, 0, 1, now, now);
        var statistics = new DecisionSessionStatistics(TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.Zero, 100m, 1m);
        var activity = new DecisionSessionActivity(4, now, TimeSpan.Zero, 1m);
        var growth = new DecisionSessionGrowth(400, 100, TimeSpan.FromHours(1), 100m);
        var cache = new DecisionSessionCacheMetrics(TimeSpan.FromHours(1), 0.1m, now.AddHours(1));
        var economics = new DecisionSessionEconomics(0.2m, 0.8m, 0.2m, 0.2m, 0.5m, 0.2m, 0.1m);
        var coherence = new DecisionSessionCoherence(0.3m, 0.8m, 0.5m, 0.5m, 0.8m);
        var evaluation = new DecisionSessionLifecycleEvaluation(
            DecisionSessionLifecycleDecision.Transfer,
            0.2m,
            0.8m,
            "Policy decided Transfer.",
            ["transfer pressure"],
            now);
        var metricsSnapshot = new DecisionSessionMetricsSnapshot(
            repositoryId,
            metrics,
            statistics,
            activity,
            growth,
            cache,
            new DecisionSessionMetricsDiagnostics(repositoryId, now, [], [], []),
            now);
        var economicsSnapshot = new DecisionSessionEconomicsSnapshot(
            repositoryId,
            economics,
            new DecisionSessionEconomicsDiagnostics(
                repositoryId,
                now,
                new DecisionSessionEconomicsInputs(metrics, statistics, activity, growth, cache),
                new ReuseValueAssessment(0.2m, 0m, 0m, 0m, 0m),
                new TransferValueAssessment(0.8m, 0m, 0m, 0m, 0m),
                new CacheBenefitAssessment(0.2m, 0m, 0.1m, 0m),
                new CacheRiskAssessment(0.1m, TimeSpan.FromHours(1), now.AddHours(1)),
                new ContinuityBenefitAssessment(0.5m, 0m, 0m, 0m),
                [],
                []),
            now);
        var coherenceSnapshot = new DecisionSessionCoherenceSnapshot(
            repositoryId,
            coherence,
            new DecisionSessionCoherenceDiagnostics(
                repositoryId,
                now,
                new DecisionSessionCoherenceInputs(metrics, statistics, cache, economics, 3, 1, 0, 1, 1, 0),
                new FragmentationAssessment(0.8m, 0m, 0m, 0m),
                new DensityAssessment(0.5m, 0.5m, 3, 1),
                new ContinuityQualityAssessment(0.5m, 0m, 0m, 0m, 0m),
                new TransferPressureAssessment(0.8m, 0m, 0m, 0m, 0m, 0m),
                [],
                []),
            now);
        var policy = new DecisionSessionLifecycleSnapshot(
            repositoryId,
            evaluation,
            new DecisionSessionLifecycleDiagnostics(
                repositoryId,
                now,
                new DecisionSessionLifecycleInputs(active, metrics, statistics, cache, economics, coherence),
                new ReuseScoreAssessment(0.2m, 0m, 0m, 0m, 0m),
                new TransferScoreAssessment(0.8m, 0m, 0m, 0m, 0m, 0m),
                [],
                []),
            now);
        var eligibility = new DecisionSessionTransferEligibilitySnapshot(
            repositoryId,
            new DecisionSessionTransferEligibility(
                DecisionSessionTransferEligibilityStatus.Eligible,
                evaluation,
                active.Id,
                [new DecisionSessionTransferEligibilityFinding("eligible", "Info", "All transfer eligibility preconditions passed.")],
                now.AddSeconds(1)),
            new DecisionSessionTransferEligibilityDiagnostics(
                repositoryId,
                now.AddSeconds(1),
                new DecisionSessionTransferEligibilityInputs(
                    evaluation,
                    new DecisionSessionDiagnostics(repositoryId, true, 1, 1, [], [], now),
                    active,
                    null),
                [],
                []),
            now.AddSeconds(1));
        var artifact = CreateArtifact(repositoryId, active.Id, evaluation, metrics, economics, coherence, cache, now.AddSeconds(2));
        var transfer = CreateTransfer(repositoryId, active.Id, artifact.ArtifactId, now.AddSeconds(3));
        var recovery = CreateRecovery(repositoryId, active.Id, now.AddSeconds(5));
        return new DecisionSessionFixtures(metricsSnapshot, economicsSnapshot, coherenceSnapshot, policy, eligibility, artifact, transfer, recovery);
    }

    private static DecisionSessionContinuityArtifact CreateArtifact(
        Guid repositoryId,
        DecisionSessionId sourceSessionId,
        DecisionSessionLifecycleEvaluation evaluation,
        DecisionSessionMetrics metrics,
        DecisionSessionEconomics economics,
        DecisionSessionCoherence coherence,
        DecisionSessionCacheMetrics cache,
        DateTimeOffset createdAt)
    {
        DecisionSessionContinuityReference[] references =
        [
            new("decisions", "decision-records", 1, 10, createdAt, "decisions"),
            new("reasoning", "reasoning-events", 1, 10, createdAt, "reasoning"),
            new("operational-context", "context-proposals", 1, 10, createdAt, "context")
        ];
        return new DecisionSessionContinuityArtifact(
            $"continuity.{createdAt.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.{sourceSessionId}.json",
            repositoryId,
            sourceSessionId,
            null,
            createdAt,
            evaluation,
            metrics,
            economics,
            coherence,
            cache,
            [references[0]],
            [references[1]],
            [references[2]],
            "fingerprint",
            []);
    }

    private static DecisionSessionTransfer CreateTransfer(
        Guid repositoryId,
        DecisionSessionId sourceSessionId,
        string artifactId,
        DateTimeOffset startedAt)
    {
        string transferId = $"transfer.{startedAt.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.{sourceSessionId}.json";
        DecisionSessionId targetSessionId = DecisionSessionId.New();
        DecisionSessionTransferEvent started = new(
            $"{transferId}.started",
            DecisionSessionTransferEventType.Started,
            repositoryId,
            sourceSessionId,
            null,
            artifactId,
            startedAt,
            "Decision session transfer started.",
            []);
        DecisionSessionTransferEvent completed = new(
            $"{transferId}.completed",
            DecisionSessionTransferEventType.Completed,
            repositoryId,
            sourceSessionId,
            targetSessionId,
            artifactId,
            startedAt.AddSeconds(1),
            "Decision session transfer completed.",
            []);
        return new DecisionSessionTransfer(
            transferId,
            repositoryId,
            sourceSessionId,
            targetSessionId,
            artifactId,
            startedAt,
            startedAt.AddSeconds(1),
            true,
            [started, completed],
            []);
    }

    private static DecisionSessionRecoveryResult CreateRecovery(Guid repositoryId, DecisionSessionId activeSessionId, DateTimeOffset recoveredAt)
    {
        return new DecisionSessionRecoveryResult(
            $"recovery.{recoveredAt.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.json",
            repositoryId,
            true,
            activeSessionId,
            1,
            [],
            new DecisionSessionRecoveryDiagnostics(
                repositoryId,
                recoveredAt,
                new DecisionSessionDiagnostics(repositoryId, true, 1, 1, [], [], recoveredAt),
                [],
                []),
            [],
            recoveredAt);
    }

    private sealed record DecisionSessionFixtures(
        DecisionSessionMetricsSnapshot Metrics,
        DecisionSessionEconomicsSnapshot Economics,
        DecisionSessionCoherenceSnapshot Coherence,
        DecisionSessionLifecycleSnapshot Policy,
        DecisionSessionTransferEligibilitySnapshot Eligibility,
        DecisionSessionContinuityArtifact Artifact,
        DecisionSessionTransfer Transfer,
        DecisionSessionRecoveryResult Recovery);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
