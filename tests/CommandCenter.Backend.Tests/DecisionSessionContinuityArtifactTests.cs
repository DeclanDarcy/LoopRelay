using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.DecisionSessions.Primitives;
using CommandCenter.DecisionSessions.Services;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionSessionContinuityArtifactTests
{
    [Fact]
    public async Task ContinuityArtifactIsCreatedPersistedAndListed()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = new(2026, 6, 24, 12, 30, 15, TimeSpan.Zero);
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-1));
        var fixtures = CreateFixtures(harness.Repository.Id, active, now);
        DecisionSessionContinuityArtifactService service = CreateService(harness, fixtures, now);

        DecisionSessionContinuityArtifact artifact = await service.CreateAsync(harness.Repository.Id, active.Id);
        IReadOnlyList<DecisionSessionContinuityArtifact> artifacts = await service.ListAsync(harness.Repository.Id);
        DecisionSessionContinuityArtifact? read = await service.GetAsync(harness.Repository.Id, artifact.ArtifactId);

        Assert.Equal($"continuity.{now.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.{active.Id}.json", artifact.ArtifactId);
        Assert.Equal(harness.Repository.Id, artifact.RepositoryId);
        Assert.Equal(active.Id, artifact.SourceSessionId);
        Assert.Null(artifact.TargetSessionId);
        Assert.NotEmpty(artifact.ContinuityFingerprint);
        Assert.Contains(artifact.DecisionReferences, reference => reference.Source == "decisions" && reference.ItemCount == 1);
        Assert.Contains(artifact.ReasoningReferences, reference => reference.Source == "reasoning-events" && reference.ItemCount == 1);
        Assert.Contains(artifact.OperationalContextReferences, reference => reference.Source == "operational-context-proposals" && reference.ItemCount == 1);
        Assert.Single(artifacts);
        Assert.NotNull(read);
        Assert.Equal(artifact.ArtifactId, read.ArtifactId);
        Assert.Equal(artifact.RepositoryId, read.RepositoryId);
        Assert.Equal(artifact.SourceSessionId, read.SourceSessionId);
        Assert.Equal(artifact.ContinuityFingerprint, read.ContinuityFingerprint);
    }

    [Fact]
    public async Task ContinuityArtifactRejectsSourceSessionMismatch()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-1));
        var fixtures = CreateFixtures(harness.Repository.Id, active, now);
        DecisionSessionContinuityArtifactService service = CreateService(harness, fixtures, now);

        await Assert.ThrowsAsync<DecisionSessionValidationException>(() =>
            service.CreateAsync(harness.Repository.Id, DecisionSessionId.New()));
    }

    [Fact]
    public async Task ContinuityArtifactValidationRejectsMissingRequiredReferences()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-1));
        var fixtures = CreateFixtures(
            harness.Repository.Id,
            active,
            now,
            evidence: CreateEvidence(harness.Repository.Id, now, includeOperationalContext: false));
        DecisionSessionContinuityArtifactService service = CreateService(harness, fixtures, now);

        await Assert.ThrowsAsync<DecisionSessionValidationException>(() =>
            service.CreateAsync(harness.Repository.Id, active.Id));
    }

    [Fact]
    public async Task ContinuityArtifactValidationRejectsFingerprintMismatch()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-1));
        var fixtures = CreateFixtures(harness.Repository.Id, active, now);
        DecisionSessionContinuityArtifactService service = CreateService(harness, fixtures, now);
        DecisionSessionContinuityArtifact artifact = await service.CreateAsync(harness.Repository.Id, active.Id);

        DecisionSessionContinuityArtifactValidation validation = service.Validate(artifact with { ContinuityFingerprint = "wrong" });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("fingerprint", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ContinuityArtifactReadRejectsRepositoryMismatch()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-1));
        var fixtures = CreateFixtures(harness.Repository.Id, active, now);
        DecisionSessionContinuityArtifactService service = CreateService(harness, fixtures, now);
        DecisionSessionContinuityArtifact artifact = await service.CreateAsync(harness.Repository.Id, active.Id);
        var document = new DecisionSessionArtifactDocument<DecisionSessionContinuityArtifact>(
            DecisionSessionArtifactPaths.SchemaVersion,
            Guid.NewGuid(),
            artifact.CreatedAt,
            now,
            artifact);
        await harness.Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, DecisionSessionArtifactPaths.ContinuityArtifactJson(artifact.ArtifactId)),
            JsonSerializer.Serialize(document, DecisionSessionJson.Options));

        await Assert.ThrowsAsync<DecisionSessionValidationException>(() =>
            harness.RepositoryStore.ReadContinuityArtifactAsync(harness.Repository, artifact.ArtifactId));
    }

    [Fact]
    public async Task ContinuityArtifactReadRejectsUnsupportedSchemaVersion()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession active = await CreateActiveSessionAsync(harness, now.AddHours(-1));
        var fixtures = CreateFixtures(harness.Repository.Id, active, now);
        DecisionSessionContinuityArtifactService service = CreateService(harness, fixtures, now);
        DecisionSessionContinuityArtifact artifact = await service.CreateAsync(harness.Repository.Id, active.Id);
        var document = new DecisionSessionArtifactDocument<DecisionSessionContinuityArtifact>(
            "decision-sessions.v0",
            harness.Repository.Id,
            artifact.CreatedAt,
            now,
            artifact);
        await harness.Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, DecisionSessionArtifactPaths.ContinuityArtifactJson(artifact.ArtifactId)),
            JsonSerializer.Serialize(document, DecisionSessionJson.Options));

        await Assert.ThrowsAsync<DecisionSessionValidationException>(() =>
            harness.RepositoryStore.ReadContinuityArtifactAsync(harness.Repository, artifact.ArtifactId));
    }

    private static async Task<DecisionSession> CreateActiveSessionAsync(DecisionSessionTestHarness harness, DateTimeOffset createdAt)
    {
        DecisionSession created = DecisionSession.Create(harness.Repository.Id, "test", createdAt);
        await harness.RepositoryStore.CreateAsync(harness.Repository, created);
        return await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
    }

    private static DecisionSessionContinuityArtifactService CreateService(
        DecisionSessionTestHarness harness,
        ContinuityFixtures fixtures,
        DateTimeOffset now)
    {
        return new DecisionSessionContinuityArtifactService(
            harness.RepositoryService,
            harness.RepositoryStore,
            new FixedLifecyclePolicy(fixtures.Policy),
            new FixedMetricsService(fixtures.Metrics),
            new FixedEconomicsService(fixtures.Economics),
            new FixedCoherenceService(fixtures.Coherence),
            new FixedEvidenceReader(fixtures.Evidence),
            new FixedTimeProvider(now));
    }

    private static ContinuityFixtures CreateFixtures(
        Guid repositoryId,
        DecisionSession active,
        DateTimeOffset now,
        DecisionSessionEvidence? evidence = null)
    {
        var metrics = new DecisionSessionMetrics(
            100,
            400,
            1,
            1,
            1,
            1,
            0,
            0,
            1,
            now,
            now);
        var statistics = new DecisionSessionStatistics(TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.Zero, 100m, 1m);
        var cache = new DecisionSessionCacheMetrics(TimeSpan.FromHours(1), 0.1m, now.AddHours(1));
        var economics = new DecisionSessionEconomics(0.2m, 0.8m, 0.2m, 0.2m, 0.5m, 0.2m, 0.1m);
        var coherence = new DecisionSessionCoherence(0.3m, 0.8m, 0.5m, 0.5m, 0.8m);
        var evaluation = new DecisionSessionLifecycleEvaluation(
            DecisionSessionLifecycleDecision.Transfer,
            0.2m,
            0.8m,
            "Policy decided Transfer.",
            [],
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
        var metricsSnapshot = new DecisionSessionMetricsSnapshot(
            repositoryId,
            metrics,
            statistics,
            new DecisionSessionActivity(3, now, TimeSpan.Zero, 1m),
            new DecisionSessionGrowth(400, 100, TimeSpan.FromHours(1), 100m),
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
                    new DecisionSessionActivity(3, now, TimeSpan.Zero, 1m),
                    new DecisionSessionGrowth(400, 100, TimeSpan.FromHours(1), 100m),
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
        return new ContinuityFixtures(
            policy,
            metricsSnapshot,
            economicsSnapshot,
            coherenceSnapshot,
            evidence ?? CreateEvidence(repositoryId, now));
    }

    private static DecisionSessionEvidence CreateEvidence(
        Guid repositoryId,
        DateTimeOffset now,
        bool includeOperationalContext = true)
    {
        DecisionSessionEvidenceSource[] sources =
        [
            new("decisions", 1, 10, 10, "decision", now, []),
            new("decision-candidates", 0, 2, 2, "[]", null, []),
            new("decision-proposals", 0, 2, 2, "[]", null, []),
            new("reasoning-events", 1, 10, 10, "event", now, []),
            new("reasoning-threads", 1, 10, 10, "thread", now, []),
            new("reasoning-relationships", 1, 10, 10, "relationship", now, []),
            new("operational-context-proposals", includeOperationalContext ? 1 : 0, 10, 10, "context", now, []),
            new("operational-context-artifacts", 0, 2, 2, "[]", null, [])
        ];
        return new DecisionSessionEvidence(
            repositoryId,
            now.AddHours(-1),
            now,
            sources.Sum(source => source.ItemCount),
            1,
            0,
            0,
            1,
            1,
            1,
            includeOperationalContext ? 1 : 0,
            sources,
            []);
    }

    private sealed record ContinuityFixtures(
        DecisionSessionLifecycleSnapshot Policy,
        DecisionSessionMetricsSnapshot Metrics,
        DecisionSessionEconomicsSnapshot Economics,
        DecisionSessionCoherenceSnapshot Coherence,
        DecisionSessionEvidence Evidence);

    private sealed class FixedLifecyclePolicy(DecisionSessionLifecycleSnapshot snapshot) : IDecisionSessionLifecyclePolicy
    {
        public Task<DecisionSessionLifecycleSnapshot> EvaluateAsync(Guid repositoryId) => Task.FromResult(snapshot);
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
