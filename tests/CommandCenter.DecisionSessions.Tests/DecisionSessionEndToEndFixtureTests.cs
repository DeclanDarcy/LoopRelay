using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Primitives;
using CommandCenter.DecisionSessions.Services;

namespace CommandCenter.DecisionSessions.Tests;

public sealed class DecisionSessionEndToEndFixtureTests
{
    [Fact]
    public async Task EndToEndLifecycleFixturePassesThroughWorkflowConsumptionAndCertification()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = new(2026, 6, 24, 18, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);

        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        DecisionSession active = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
        EndToEndFixtures fixtures = CreateFixtures(harness.Repository.Id, active, now);
        var metricsService = new FixedMetricsService(fixtures.Metrics);
        var economicsService = new FixedEconomicsService(fixtures.Economics);
        var coherenceService = new FixedCoherenceService(fixtures.Coherence);
        var policy = new PersistingLifecyclePolicy(harness.Repository, harness.RepositoryStore, fixtures.Policy);
        var evidenceReader = new FixedEvidenceReader(fixtures.Evidence);
        var eligibilityService = new DecisionSessionTransferEligibilityService(
            harness.RepositoryService,
            harness.RepositoryStore,
            harness.Recovery,
            policy,
            evidenceReader,
            timeProvider);
        var artifactService = new DecisionSessionContinuityArtifactService(
            harness.RepositoryService,
            harness.RepositoryStore,
            policy,
            metricsService,
            economicsService,
            coherenceService,
            evidenceReader,
            timeProvider);
        var transferService = new DecisionSessionTransferService(
            harness.RepositoryService,
            harness.RepositoryStore,
            harness.Registry,
            eligibilityService,
            policy,
            new DecisionSessionContinuityCaptureService(artifactService),
            new DecisionSessionContinuityIntegrationService(),
            artifactService,
            timeProvider);
        var recoveryService = new DecisionSessionRecoveryService(
            harness.RepositoryService,
            harness.RepositoryStore,
            timeProvider,
            metricsService,
            economicsService,
            coherenceService,
            policy,
            evidenceReader);
        var observability = new DecisionSessionObservabilityService(
            harness.RepositoryService,
            harness.RepositoryStore,
            timeProvider,
            metricsService,
            economicsService,
            coherenceService,
            policy,
            eligibilityService);
        var certification = new DecisionSessionCertificationService(
            harness.RepositoryService,
            harness.RepositoryStore,
            observability,
            timeProvider);

        DecisionSessionMetricsSnapshot metrics = await metricsService.GetMetricsAsync(harness.Repository.Id);
        await harness.RepositoryStore.WriteMetricsSnapshotAsync(harness.Repository, metrics);
        DecisionSessionEconomicsSnapshot economics = await economicsService.GetEconomicsAsync(harness.Repository.Id);
        await harness.RepositoryStore.WriteEconomicsSnapshotAsync(harness.Repository, economics);
        DecisionSessionCoherenceSnapshot coherence = await coherenceService.GetCoherenceAsync(harness.Repository.Id);
        await harness.RepositoryStore.WriteCoherenceSnapshotAsync(harness.Repository, coherence);
        DecisionSessionLifecycleSnapshot policySnapshot = await policy.EvaluateAsync(harness.Repository.Id);
        DecisionSessionTransferEligibilitySnapshot eligibility = await eligibilityService.CheckAsync(harness.Repository.Id);

        DecisionSessionTransferResult transfer = await transferService.ExecuteAsync(harness.Repository.Id);
        DecisionSessionRecoveryResult recovery = await recoveryService.RecoverAsync(harness.Repository.Id);
        DecisionSessionLifecycleProjection projection = await observability.GetProjectionAsync(harness.Repository.Id);
        DecisionSessionCertificationReport report = await certification.RunCertificationAsync(harness.Repository.Id);

        var fixture = new DecisionSessionLifecycleEndToEndFixture(
            harness.Repository.Id,
            active.Id,
            transfer.ReplacementSession?.Id,
            transfer.ContinuityArtifact?.ArtifactId,
            transfer.Transfer?.TransferId,
            recovery.RecoveryId,
            report.Result.Certified,
            now);

        Assert.Equal(active.Id, fixture.InitialSessionId);
        Assert.Equal(100, metrics.Metrics.EstimatedTokenCount);
        Assert.True(economics.Economics.EstimatedTransferValue > economics.Economics.EstimatedReuseValue);
        Assert.Equal(DecisionSessionLifecycleDecision.Transfer, policySnapshot.Evaluation.Decision);
        Assert.Equal(DecisionSessionTransferEligibilityStatus.Eligible, eligibility.Eligibility.Status);
        Assert.True(transfer.Succeeded);
        Assert.NotNull(fixture.ReplacementSessionId);
        Assert.NotNull(fixture.ContinuityArtifactId);
        Assert.NotNull(fixture.TransferId);
        Assert.True(recovery.Succeeded);
        Assert.Equal(fixture.ReplacementSessionId, recovery.ActiveSessionId);
        Assert.Equal(fixture.ReplacementSessionId, projection.ActiveSession?.Id);
        Assert.True(fixture.CertificationPassed, string.Join(Environment.NewLine, report.Result.Failures));
        Assert.Contains(report.Result.Findings, finding => finding.Id == "workflow-consumption-read-only-boundary" && finding.Passed);
    }

    private static EndToEndFixtures CreateFixtures(Guid repositoryId, DecisionSession active, DateTimeOffset now)
    {
        var metrics = new DecisionSessionMetrics(100, 400, 1, 1, 1, 1, 0, 0, 1, now, now);
        var statistics = new DecisionSessionStatistics(TimeSpan.FromHours(2), TimeSpan.FromHours(2), TimeSpan.Zero, 100m, 1m);
        var activity = new DecisionSessionActivity(4, now, TimeSpan.Zero, 1m);
        var growth = new DecisionSessionGrowth(400, 100, TimeSpan.FromHours(2), 100m);
        var cache = new DecisionSessionCacheMetrics(TimeSpan.FromHours(1), 0.1m, now.AddHours(1));
        var economics = new DecisionSessionEconomics(0.2m, 0.8m, 0.2m, 0.2m, 0.5m, 0.2m, 0.1m);
        var coherence = new DecisionSessionCoherence(0.410m, 0.8m, 0.5m, 0.5m, 0.8m);
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
            new DecisionSessionMetricsDiagnostics(
                repositoryId,
                now,
                [new DecisionSessionMetricsSourceDiagnostic("test", 1, 400, 400, [])],
                [],
                []),
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
        return new EndToEndFixtures(
            metricsSnapshot,
            economicsSnapshot,
            coherenceSnapshot,
            policy,
            CreateEvidence(repositoryId, now));
    }

    private static DecisionSessionEvidence CreateEvidence(Guid repositoryId, DateTimeOffset now)
    {
        DecisionSessionEvidenceSource[] sources =
        [
            new("decisions", 1, 10, 10, "decision", now, []),
            new("reasoning-events", 1, 10, 10, "event", now, []),
            new("reasoning-threads", 1, 10, 10, "thread", now, []),
            new("reasoning-relationships", 1, 10, 10, "relationship", now, []),
            new("operational-context-proposals", 1, 10, 10, "context", now, [])
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
            1,
            sources,
            []);
    }

    private sealed record EndToEndFixtures(
        DecisionSessionMetricsSnapshot Metrics,
        DecisionSessionEconomicsSnapshot Economics,
        DecisionSessionCoherenceSnapshot Coherence,
        DecisionSessionLifecycleSnapshot Policy,
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
        public Task<DecisionSessionEvidence> ReadAsync(Repository repository, DecisionSession? activeSession, DateTimeOffset measuredAt) =>
            Task.FromResult(evidence);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
