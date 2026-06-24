using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Primitives;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Services;

namespace CommandCenter.Backend.Tests;

public sealed class WorkflowDecisionSessionServiceTests
{
    [Fact]
    public async Task ProjectAsyncSurfacesContinueLifecycleState()
    {
        Guid repositoryId = Guid.NewGuid();
        DecisionSessionId sessionId = DecisionSessionId.New();
        DateTimeOffset now = DateTimeOffset.Parse("2026-06-23T10:00:00Z");
        var observability = new ObservabilityStub(
            CreateProjection(
                repositoryId,
                sessionId,
                now,
                DecisionSessionLifecycleDecision.Continue,
                DecisionSessionTransferEligibilityStatus.NotApplicable),
            CreateHealth(repositoryId, sessionId, now));
        var service = new WorkflowDecisionSessionService(observability);

        var projection = await service.ProjectAsync(repositoryId);

        Assert.Equal(repositoryId, projection.RepositoryId);
        Assert.Equal(sessionId.ToString(), projection.DecisionSessionId);
        Assert.Equal("Active", projection.DecisionSessionState);
        Assert.Equal(3210, projection.EstimatedTokenCount);
        Assert.Equal(TimeSpan.FromMinutes(45), projection.EstimatedCacheTtl);
        Assert.Equal(0.25m, projection.EstimatedCacheMissRisk);
        Assert.Equal(0.72m, projection.ReuseScore);
        Assert.Equal(0.31m, projection.TransferScore);
        Assert.Equal(0.82m, projection.CoherenceScore);
        Assert.Equal(0.19m, projection.TransferPressure);
        Assert.Equal("Continue", projection.CurrentLifecycleDecision);
        Assert.Equal("NotApplicable", projection.TransferEligibilityStatus);
        Assert.False(projection.Readiness.IsTransferRecommended);
        Assert.False(projection.Readiness.IsTransferEligible);
        Assert.Contains(projection.Summary.Highlights, highlight => highlight.Contains("Continue", StringComparison.Ordinal));
        Assert.Contains(projection.Diagnostics.Evidence, evidence => evidence == $"decision-session:{sessionId}:Active");
        Assert.Equal(1, observability.ProjectionCalls);
        Assert.Equal(1, observability.HealthCalls);
    }

    [Fact]
    public async Task ProjectAsyncSurfacesTransferEligibilityArtifactAndTransferLineage()
    {
        Guid repositoryId = Guid.NewGuid();
        DecisionSessionId sourceSessionId = DecisionSessionId.New();
        DecisionSessionId targetSessionId = DecisionSessionId.New();
        DateTimeOffset now = DateTimeOffset.Parse("2026-06-23T11:00:00Z");
        DecisionSessionLifecycleProjection lifecycleProjection = CreateProjection(
            repositoryId,
            sourceSessionId,
            now,
            DecisionSessionLifecycleDecision.Transfer,
            DecisionSessionTransferEligibilityStatus.Eligible,
            targetSessionId);
        var observability = new ObservabilityStub(
            lifecycleProjection,
            CreateHealth(repositoryId, sourceSessionId, now, DecisionSessionHealthStatus.Warning));
        var service = new WorkflowDecisionSessionService(observability);

        var projection = await service.ProjectAsync(repositoryId);

        Assert.Equal("Transfer", projection.CurrentLifecycleDecision);
        Assert.Equal("Eligible", projection.TransferEligibilityStatus);
        Assert.True(projection.Readiness.IsTransferRecommended);
        Assert.True(projection.Readiness.IsTransferEligible);
        Assert.True(projection.Readiness.HasContinuityArtifact);
        Assert.Equal("artifact-0001", projection.ContinuityArtifactId);
        Assert.Equal("fingerprint-0001", projection.ContinuityFingerprint);
        WorkflowContinuityArtifactProjection artifact = Assert.Single(projection.ContinuityArtifactLineage);
        Assert.Equal(sourceSessionId.ToString(), artifact.SourceSessionId);
        Assert.Equal(targetSessionId.ToString(), artifact.TargetSessionId);
        Assert.Equal(1, artifact.DecisionReferenceCount);
        Assert.Equal(1, artifact.ReasoningReferenceCount);
        WorkflowTransferProjection transfer = Assert.Single(projection.TransferLineage);
        Assert.Equal("transfer-0001", transfer.TransferId);
        Assert.Equal("Succeeded", transfer.Status);
        Assert.Equal("artifact-0001", transfer.ContinuityArtifactId);
        Assert.Equal("Warning", projection.Summary.HealthStatus);
    }

    [Fact]
    public async Task GetHealthAndInfluenceReturnWorkflowReadableGovernanceSignals()
    {
        Guid repositoryId = Guid.NewGuid();
        DecisionSessionId sessionId = DecisionSessionId.New();
        DateTimeOffset now = DateTimeOffset.Parse("2026-06-23T12:00:00Z");
        var observability = new ObservabilityStub(
            CreateProjection(
                repositoryId,
                sessionId,
                now,
                DecisionSessionLifecycleDecision.Transfer,
                DecisionSessionTransferEligibilityStatus.Blocked),
            CreateHealth(repositoryId, sessionId, now, DecisionSessionHealthStatus.Unhealthy),
            CreateInfluence(repositoryId, sessionId, now, DecisionSessionLifecycleDecision.Transfer, DecisionSessionTransferEligibilityStatus.Blocked));
        var service = new WorkflowDecisionSessionService(observability);

        var health = await service.GetHealthAsync(repositoryId);
        var influence = await service.GetInfluenceAsync(repositoryId);

        Assert.Equal("Decision sessions", health.Name);
        Assert.Equal("Unhealthy", health.Status);
        Assert.Contains("Blocked transfer eligibility.", health.Findings);
        Assert.Equal(sessionId.ToString(), influence.DecisionSessionId);
        Assert.Equal("Transfer", influence.LifecycleDecision);
        Assert.Equal("Blocked", influence.TransferEligibilityStatus);
        WorkflowGovernanceInfluenceSignal signal = Assert.Single(influence.Signals);
        Assert.Equal("Policy", signal.Category);
        Assert.Equal("Transfer pressure", signal.Name);
        Assert.Equal(0.88m, signal.Score);
        Assert.Equal(1, observability.HealthCalls);
        Assert.Equal(1, observability.InfluenceCalls);
    }

    [Fact]
    public void WorkflowDecisionSessionServiceDependsOnlyOnReadOnlyObservability()
    {
        var constructor = Assert.Single(typeof(WorkflowDecisionSessionService).GetConstructors());
        Type parameterType = Assert.Single(constructor.GetParameters()).ParameterType;

        Assert.Equal(typeof(IDecisionSessionObservabilityService), parameterType);
    }

    private static DecisionSessionLifecycleProjection CreateProjection(
        Guid repositoryId,
        DecisionSessionId sessionId,
        DateTimeOffset now,
        DecisionSessionLifecycleDecision decision,
        DecisionSessionTransferEligibilityStatus eligibilityStatus,
        DecisionSessionId? targetSessionId = null)
    {
        DecisionSession session = CreateSession(repositoryId, sessionId, now);
        DecisionSessionMetrics metrics = CreateMetrics(now);
        DecisionSessionStatistics statistics = new(TimeSpan.FromHours(3), TimeSpan.FromHours(3), TimeSpan.FromMinutes(12), 250m, 1.2m);
        DecisionSessionCacheMetrics cache = new(TimeSpan.FromMinutes(45), 0.25m, now.AddMinutes(45));
        DecisionSessionEconomics economics = new(0.67m, 0.44m, 0.22m, 0.18m, 0.64m, 0.55m, 0.25m);
        DecisionSessionCoherence coherence = new(0.82m, 0.12m, 0.77m, 0.79m, 0.19m);
        DecisionSessionLifecycleEvaluation policyEvaluation = new(
            decision,
            0.72m,
            0.31m,
            $"Policy chose {decision}.",
            ["test policy factor"],
            now);
        DecisionSessionDiagnostics diagnostics = new(repositoryId, true, 1, 1, [], [], now);
        DecisionSessionEvidence evidence = new(
            repositoryId,
            now.AddHours(-3),
            now,
            5,
            2,
            1,
            1,
            3,
            1,
            1,
            1,
            [],
            []);
        DecisionSessionTransferEligibility transferEligibility = new(
            eligibilityStatus,
            policyEvaluation,
            sessionId,
            eligibilityStatus is DecisionSessionTransferEligibilityStatus.Blocked
                ? [new DecisionSessionTransferEligibilityFinding("blocked-test", "Blocking", "Blocked transfer eligibility.")]
                : [],
            now);
        DecisionSessionContinuityArtifact? artifact = targetSessionId is null
            ? null
            : new DecisionSessionContinuityArtifact(
                "artifact-0001",
                repositoryId,
                sessionId,
                targetSessionId,
                now,
                policyEvaluation,
                metrics,
                economics,
                coherence,
                cache,
                [new DecisionSessionContinuityReference("decisions", "decision", 2, 200, now, "decision-fingerprint")],
                [new DecisionSessionContinuityReference("reasoning", "event", 3, 300, now, "reasoning-fingerprint")],
                [new DecisionSessionContinuityReference("continuity", "revision", 1, 100, now, "context-fingerprint")],
                "fingerprint-0001",
                []);

        return new DecisionSessionLifecycleProjection(
            repositoryId,
            DecisionSessionProjection.FromSession(session),
            [DecisionSessionProjection.FromSession(session)],
            new DecisionSessionMetricsSnapshot(
                repositoryId,
                metrics,
                statistics,
                new DecisionSessionActivity(5, now, TimeSpan.FromMinutes(12), 1.2m),
                new DecisionSessionGrowth(2048, 3210, TimeSpan.FromHours(3), 250m),
                cache,
                new DecisionSessionMetricsDiagnostics(repositoryId, now, [], [], []),
                now),
            new DecisionSessionSizeProjection(3210, 2048, 3, 2, TimeSpan.FromHours(3), TimeSpan.FromMinutes(12), 0.25m, now),
            new DecisionSessionEconomicsSnapshot(
                repositoryId,
                economics,
                new DecisionSessionEconomicsDiagnostics(
                    repositoryId,
                    now,
                    new DecisionSessionEconomicsInputs(
                        metrics,
                        statistics,
                        new DecisionSessionActivity(5, now, TimeSpan.FromMinutes(12), 1.2m),
                        new DecisionSessionGrowth(2048, 3210, TimeSpan.FromHours(3), 250m),
                        cache),
                    new ReuseValueAssessment(0.67m, 0.2m, 0.2m, 0.2m, 0.07m),
                    new TransferValueAssessment(0.44m, 0.1m, 0.1m, 0.1m, 0.14m),
                    new CacheBenefitAssessment(0.55m, 0.7m, 0.1m, 0.25m),
                    new CacheRiskAssessment(0.25m, TimeSpan.FromMinutes(45), now.AddMinutes(45)),
                    new ContinuityBenefitAssessment(0.64m, 0.2m, 0.2m, 0.24m),
                    [],
                    []),
                now),
            new DecisionSessionCoherenceSnapshot(
                repositoryId,
                coherence,
                new DecisionSessionCoherenceDiagnostics(
                    repositoryId,
                    now,
                    new DecisionSessionCoherenceInputs(metrics, statistics, cache, economics, 6, 8, 1, 1, 5, 1),
                    new FragmentationAssessment(0.12m, 0.04m, 0.04m, 0.04m),
                    new DensityAssessment(0.77m, 1.33m, 6, 8),
                    new ContinuityQualityAssessment(0.79m, 0.3m, 0.3m, 0.1m, 0.09m),
                    new TransferPressureAssessment(0.19m, 0.03m, 0.04m, 0.04m, 0.04m, 0.04m),
                    [],
                    []),
                now),
            new DecisionSessionLifecycleSnapshot(
                repositoryId,
                policyEvaluation,
                new DecisionSessionLifecycleDiagnostics(
                    repositoryId,
                    now,
                    new DecisionSessionLifecycleInputs(session, metrics, statistics, cache, economics, coherence),
                    new ReuseScoreAssessment(0.72m, 0.2m, 0.2m, 0.16m, 0.16m),
                    new TransferScoreAssessment(0.31m, 0.08m, 0.07m, 0.06m, 0.05m, 0.05m),
                    [],
                    []),
                now),
            new DecisionSessionTransferEligibilitySnapshot(
                repositoryId,
                transferEligibility,
                new DecisionSessionTransferEligibilityDiagnostics(
                    repositoryId,
                    now,
                    new DecisionSessionTransferEligibilityInputs(policyEvaluation, diagnostics, session, evidence),
                    [],
                    []),
                now),
            artifact,
            artifact is null
                ? []
                :
                [
                    new DecisionSessionContinuityArtifactProjection(
                        artifact.ArtifactId,
                        artifact.ContinuityFingerprint,
                        artifact.SourceSessionId,
                        artifact.TargetSessionId,
                        artifact.DecisionReferences,
                        artifact.ReasoningReferences,
                        artifact.OperationalContextReferences,
                        artifact.CreatedAt,
                        artifact.Diagnostics)
                ],
            [],
            [],
            targetSessionId is null
                ? []
                :
                [
                    new DecisionSessionTransferEventProjection(
                        "transfer-0001",
                        sessionId,
                        targetSessionId,
                        now,
                        now.AddMinutes(1),
                        true,
                        "test transfer",
                        metrics.EstimatedTokenCount,
                        decision,
                        policyEvaluation.ReuseScore,
                        policyEvaluation.TransferScore,
                        eligibilityStatus,
                        artifact?.ArtifactId,
                        [],
                        [])
                ],
            [],
            diagnostics,
            now);
    }

    private static DecisionSession CreateSession(Guid repositoryId, DecisionSessionId sessionId, DateTimeOffset now) =>
        new(
            sessionId,
            repositoryId,
            DecisionSessionState.Active,
            now.AddHours(-3),
            now.AddHours(-3),
            null,
            new DecisionSessionOwnership(repositoryId, "test", now.AddHours(-3)),
            new DecisionSessionMetadata(UpdatedAt: now));

    private static DecisionSessionMetrics CreateMetrics(DateTimeOffset now) =>
        new(3210, 2048, 3, 1, 1, 2, 1, 1, 1, now, now);

    private static DecisionSessionHealthAssessment CreateHealth(
        Guid repositoryId,
        DecisionSessionId sessionId,
        DateTimeOffset now,
        DecisionSessionHealthStatus status = DecisionSessionHealthStatus.Healthy) =>
        new(
            repositoryId,
            [
                new DecisionSessionHealthDimension(
                    "Lifecycle",
                    status,
                    status is DecisionSessionHealthStatus.Healthy ? [] : ["Blocked transfer eligibility."],
                    [$"decision-session:{sessionId}:Active"])
            ],
            CreateInfluence(repositoryId, sessionId, now, DecisionSessionLifecycleDecision.Continue, DecisionSessionTransferEligibilityStatus.NotApplicable),
            now);

    private static DecisionSessionInfluenceTrace CreateInfluence(
        Guid repositoryId,
        DecisionSessionId sessionId,
        DateTimeOffset now,
        DecisionSessionLifecycleDecision decision,
        DecisionSessionTransferEligibilityStatus eligibilityStatus) =>
        new(
            repositoryId,
            sessionId,
            decision,
            eligibilityStatus,
            [
                new DecisionSessionInfluenceSignal(
                    "Policy",
                    "Transfer pressure",
                    0.88m,
                    "High",
                    "Transfer pressure influences workflow visibility.",
                    ["coherence", "cache-risk"])
            ],
            ["influence generated"],
            now);

    private sealed class ObservabilityStub(
        DecisionSessionLifecycleProjection projection,
        DecisionSessionHealthAssessment health,
        DecisionSessionInfluenceTrace? influence = null) : IDecisionSessionObservabilityService
    {
        public int ProjectionCalls { get; private set; }

        public int HealthCalls { get; private set; }

        public int InfluenceCalls { get; private set; }

        public Task<DecisionSessionLifecycleProjection> GetProjectionAsync(Guid repositoryId)
        {
            ProjectionCalls++;
            return Task.FromResult(projection);
        }

        public Task<DecisionSessionLifecycleHistory> GetHistoryAsync(Guid repositoryId) =>
            throw new NotSupportedException("Workflow consumption does not require lifecycle history authority.");

        public Task<DecisionSessionInfluenceTrace> GetInfluenceTraceAsync(Guid repositoryId)
        {
            InfluenceCalls++;
            return Task.FromResult(influence ?? health.InfluenceTrace);
        }

        public Task<DecisionSessionHealthAssessment> GetHealthAsync(Guid repositoryId)
        {
            HealthCalls++;
            return Task.FromResult(health);
        }
    }
}
