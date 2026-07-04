using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.DecisionSessions.Services;

namespace CommandCenter.DecisionSessions.Tests;

public sealed class DecisionSessionTransferTests
{
    [Fact]
    public async Task EligibleTransferCreatesArtifactTransfersSourceAndActivatesReplacement()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = new(2026, 6, 24, 16, 0, 0, TimeSpan.Zero);
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-2));
        TransferFixtures fixtures = CreateFixtures(harness.Repository.Id, active, now, DecisionSessionLifecycleDecision.Transfer);
        DecisionSessionTransferService service = CreateService(harness, fixtures, now);

        DecisionSessionTransferResult result = await service.ExecuteAsync(harness.Repository.Id);
        IReadOnlyList<DecisionSession> sessions = await harness.RepositoryStore.ListAsync(harness.Repository);
        IReadOnlyList<DecisionSessionTransfer> transfers = await harness.RepositoryStore.ListTransfersAsync(harness.Repository);
        IReadOnlyList<DecisionSessionContinuityArtifact> artifacts = await harness.RepositoryStore.ListContinuityArtifactsAsync(harness.Repository);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Transfer);
        Assert.NotNull(result.ReplacementSession);
        Assert.NotNull(result.ContinuityArtifact);
        Assert.Equal(DecisionSessionState.Transferred, result.SourceSession?.State);
        Assert.Equal(DecisionSessionState.Active, result.ReplacementSession.State);
        Assert.Single(sessions, session => session.State == DecisionSessionState.Active);
        Assert.Equal(result.ReplacementSession.Id, sessions.Single(session => session.State == DecisionSessionState.Active).Id);
        Assert.Single(transfers);
        Assert.True(transfers[0].Succeeded);
        Assert.Contains(transfers[0].Events, transferEvent => transferEvent.EventType == DecisionSessionTransferEventType.Started);
        Assert.Contains(transfers[0].Events, transferEvent => transferEvent.EventType == DecisionSessionTransferEventType.Completed);
        Assert.Single(artifacts);
        Assert.Equal(active.Id, artifacts[0].SourceSessionId);
        Assert.Equal(result.ReplacementSession.Id, artifacts[0].TargetSessionId);
    }

    [Fact]
    public async Task BlockedEligibilityDoesNotMutateRegistry()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = new(2026, 6, 24, 16, 30, 0, TimeSpan.Zero);
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-2));
        TransferFixtures fixtures = CreateFixtures(
            harness.Repository.Id,
            active,
            now,
            DecisionSessionLifecycleDecision.Transfer,
            evidence: CreateEvidence(harness.Repository.Id, now, operationalContextRevisionCount: 0));
        DecisionSessionTransferService service = CreateService(harness, fixtures, now);

        DecisionSessionTransferResult result = await service.ExecuteAsync(harness.Repository.Id);
        IReadOnlyList<DecisionSession> sessions = await harness.RepositoryStore.ListAsync(harness.Repository);
        IReadOnlyList<DecisionSessionTransfer> transfers = await harness.RepositoryStore.ListTransfersAsync(harness.Repository);

        Assert.False(result.Succeeded);
        Assert.Null(result.Transfer);
        Assert.Equal(active.Id, sessions.Single(session => session.State == DecisionSessionState.Active).Id);
        Assert.Empty(transfers);
    }

    [Fact]
    public async Task FailedTransferPersistsDiagnosticsAndLeavesSourceTransferPending()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = new(2026, 6, 24, 17, 0, 0, TimeSpan.Zero);
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-2));
        TransferFixtures fixtures = CreateFixtures(harness.Repository.Id, active, now, DecisionSessionLifecycleDecision.Transfer);
        DecisionSessionTransferService service = CreateService(
            harness,
            fixtures,
            now,
            new ThrowingContinuityIntegrationService(new InvalidOperationException("integration failed")));

        DecisionSessionTransferResult result = await service.ExecuteAsync(harness.Repository.Id);
        IReadOnlyList<DecisionSession> sessions = await harness.RepositoryStore.ListAsync(harness.Repository);
        IReadOnlyList<DecisionSessionTransfer> transfers = await harness.RepositoryStore.ListTransfersAsync(harness.Repository);

        Assert.False(result.Succeeded);
        Assert.Equal(DecisionSessionState.TransferPending, sessions.Single(session => session.Id == active.Id).State);
        Assert.DoesNotContain(sessions, session => session.State == DecisionSessionState.Active);
        Assert.Single(transfers);
        Assert.Contains(transfers[0].Events, transferEvent => transferEvent.EventType == DecisionSessionTransferEventType.Started);
        Assert.Contains(transfers[0].Events, transferEvent => transferEvent.EventType == DecisionSessionTransferEventType.Failed);
        Assert.Contains(transfers[0].Diagnostics, diagnostic => diagnostic.Contains("integration failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TransferEndpointsReturnHistoryAndDiagnostics()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = new(2026, 6, 24, 17, 30, 0, TimeSpan.Zero);
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-2));
        TransferFixtures fixtures = CreateFixtures(harness.Repository.Id, active, now, DecisionSessionLifecycleDecision.Transfer);
        DecisionSessionTransferService service = CreateService(harness, fixtures, now);

        await service.ExecuteAsync(harness.Repository.Id);
        IReadOnlyList<DecisionSessionTransfer> history = await service.ListAsync(harness.Repository.Id);
        DecisionSessionTransferDiagnostics diagnostics = await service.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Single(history);
        Assert.Contains(diagnostics.Events, transferEvent => transferEvent.EventType == DecisionSessionTransferEventType.Completed);
    }

    private static async Task<DecisionSession> CreateActiveSessionAsync(DecisionSessionTestHarness harness, DateTimeOffset createdAt)
    {
        DecisionSession created = DecisionSession.Create(harness.Repository.Id, "test", createdAt);
        await harness.RepositoryStore.CreateAsync(harness.Repository, created);
        return await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
    }

    private static DecisionSessionTransferService CreateService(
        DecisionSessionTestHarness harness,
        TransferFixtures fixtures,
        DateTimeOffset now,
        IDecisionSessionContinuityIntegrationService? integrationService = null)
    {
        var timeProvider = new FixedTimeProvider(now);
        var policy = new PersistingLifecyclePolicy(harness.Repository, harness.RepositoryStore, fixtures.Policy);
        var metrics = new FixedMetricsService(fixtures.Metrics);
        var economics = new FixedEconomicsService(fixtures.Economics);
        var coherence = new FixedCoherenceService(fixtures.Coherence);
        var evidence = new FixedEvidenceReader(fixtures.Evidence);
        var artifactService = new DecisionSessionContinuityArtifactService(
            harness.RepositoryService,
            harness.RepositoryStore,
            policy,
            metrics,
            economics,
            coherence,
            evidence,
            timeProvider);
        var eligibility = new DecisionSessionTransferEligibilityService(
            harness.RepositoryService,
            harness.RepositoryStore,
            harness.Recovery,
            policy,
            evidence,
            timeProvider);
        var capture = new DecisionSessionContinuityCaptureService(artifactService);
        return new DecisionSessionTransferService(
            harness.RepositoryService,
            harness.RepositoryStore,
            harness.Registry,
            eligibility,
            policy,
            capture,
            integrationService ?? new DecisionSessionContinuityIntegrationService(),
            artifactService,
            timeProvider);
    }

    private static TransferFixtures CreateFixtures(
        Guid repositoryId,
        DecisionSession active,
        DateTimeOffset now,
        DecisionSessionLifecycleDecision decision,
        DecisionSessionEvidence? evidence = null)
    {
        var metrics = new DecisionSessionMetrics(100, 400, 1, 1, 1, 1, 0, 0, 1, now, now);
        var statistics = new DecisionSessionStatistics(TimeSpan.FromHours(2), TimeSpan.FromHours(2), TimeSpan.Zero, 100m, 1m);
        var cache = new DecisionSessionCacheMetrics(TimeSpan.FromHours(1), 0.1m, now.AddHours(1));
        var economics = new DecisionSessionEconomics(0.2m, 0.8m, 0.2m, 0.2m, 0.5m, 0.2m, 0.1m);
        var coherence = new DecisionSessionCoherence(0.3m, 0.8m, 0.5m, 0.5m, 0.8m);
        var evaluation = new DecisionSessionLifecycleEvaluation(
            decision,
            decision == DecisionSessionLifecycleDecision.Continue ? 0.8m : 0.2m,
            decision == DecisionSessionLifecycleDecision.Transfer ? 0.8m : 0.2m,
            $"Policy decided {decision}.",
            [],
            now);
        var policy = new DecisionSessionLifecycleSnapshot(
            repositoryId,
            evaluation,
            new DecisionSessionLifecycleDiagnostics(
                repositoryId,
                now,
                new DecisionSessionLifecycleInputs(active, metrics, statistics, cache, economics, coherence),
                new ReuseScoreAssessment(evaluation.ReuseScore, 0m, 0m, 0m, 0m),
                new TransferScoreAssessment(evaluation.TransferScore, 0m, 0m, 0m, 0m, 0m),
                [],
                []),
            now);
        var metricsSnapshot = new DecisionSessionMetricsSnapshot(
            repositoryId,
            metrics,
            statistics,
            new DecisionSessionActivity(4, now, TimeSpan.Zero, 1m),
            new DecisionSessionGrowth(400, 100, TimeSpan.FromHours(2), 100m),
            cache,
            new DecisionSessionMetricsDiagnostics(repositoryId, now, [], [], []),
            now);
        var economicsSnapshot = new DecisionSessionEconomicsSnapshot(
            repositoryId,
            economics,
            new DecisionSessionEconomicsDiagnostics(
                repositoryId,
                now,
                new DecisionSessionEconomicsInputs(
                    metrics,
                    statistics,
                    new DecisionSessionActivity(4, now, TimeSpan.Zero, 1m),
                    new DecisionSessionGrowth(400, 100, TimeSpan.FromHours(2), 100m),
                    cache),
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
        return new TransferFixtures(
            policy,
            metricsSnapshot,
            economicsSnapshot,
            coherenceSnapshot,
            evidence ?? CreateEvidence(repositoryId, now, operationalContextRevisionCount: 1));
    }

    private static DecisionSessionEvidence CreateEvidence(
        Guid repositoryId,
        DateTimeOffset now,
        long operationalContextRevisionCount)
    {
        DecisionSessionEvidenceSource[] sources =
        [
            new("decisions", 1, 10, 10, "decision", now, []),
            new("reasoning-events", 1, 10, 10, "event", now, []),
            new("reasoning-threads", 1, 10, 10, "thread", now, []),
            new("reasoning-relationships", 1, 10, 10, "relationship", now, []),
            new("operational-context-proposals", operationalContextRevisionCount, 10, 10, "context", now, [])
        ];
        return new DecisionSessionEvidence(
            repositoryId,
            now.AddHours(-2),
            now,
            sources.Sum(source => source.ItemCount),
            1,
            0,
            0,
            1,
            1,
            1,
            operationalContextRevisionCount,
            sources,
            []);
    }

    private sealed record TransferFixtures(
        DecisionSessionLifecycleSnapshot Policy,
        DecisionSessionMetricsSnapshot Metrics,
        DecisionSessionEconomicsSnapshot Economics,
        DecisionSessionCoherenceSnapshot Coherence,
        DecisionSessionEvidence Evidence);

    private sealed class PersistingLifecyclePolicy(
        Repository repository,
        IDecisionSessionRepository sessionRepository,
        DecisionSessionLifecycleSnapshot snapshot) : IDecisionSessionLifecyclePolicy
    {
        public async Task<DecisionSessionLifecycleSnapshot> EvaluateAsync(Guid repositoryId)
        {
            await sessionRepository.WriteLifecyclePolicySnapshotAsync(repository, snapshot);
            return snapshot;
        }
    }

    private sealed class FixedMetricsService(DecisionSessionMetricsSnapshot snapshot) : IDecisionSessionMetricsService
    {
        public Task<DecisionSessionMetricsSnapshot> GetMetricsAsync(Guid repositoryId) => Task.FromResult(snapshot);
    }

    private sealed class FixedEconomicsService(DecisionSessionEconomicsSnapshot snapshot) : IDecisionSessionEconomicsService
    {
        public Task<DecisionSessionEconomicsSnapshot> GetEconomicsAsync(Guid repositoryId) => Task.FromResult(snapshot);
    }

    private sealed class FixedCoherenceService(DecisionSessionCoherenceSnapshot snapshot) : IDecisionSessionCoherenceService
    {
        public Task<DecisionSessionCoherenceSnapshot> GetCoherenceAsync(Guid repositoryId) => Task.FromResult(snapshot);
    }

    private sealed class FixedEvidenceReader(DecisionSessionEvidence evidence) : IDecisionSessionEvidenceReader
    {
        public Task<DecisionSessionEvidence> ReadAsync(Repository repository, DecisionSession? activeSession, DateTimeOffset measuredAt)
        {
            return Task.FromResult(evidence);
        }
    }

    private sealed class ThrowingContinuityIntegrationService(Exception exception) : IDecisionSessionContinuityIntegrationService
    {
        public Task<IReadOnlyList<string>> IntegrateAsync(Guid repositoryId, DecisionSessionContinuityArtifact artifact)
        {
            throw exception;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
