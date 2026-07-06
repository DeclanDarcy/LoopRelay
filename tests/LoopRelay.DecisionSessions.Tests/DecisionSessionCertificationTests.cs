using LoopRelay.DecisionSessions.Abstractions;
using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Primitives;
using LoopRelay.DecisionSessions.Services;

namespace LoopRelay.DecisionSessions.Tests;

public sealed class DecisionSessionCertificationTests
{
    [Fact]
    public async Task CertificationPassesForCompleteLifecycleEvidenceAndPersistsReport()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection active = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-2));
        DecisionSessionLifecycleProjection projection = CreateProjection(harness.Repository.Id, active, now);
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, active.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Healthy, now);
        var service = CreateService(harness, projection, history, health, now);

        DecisionSessionCertificationReport report = await service.RunCertificationAsync(harness.Repository.Id);
        DecisionSessionCertificationReport? latest = await service.GetLatestReportAsync(harness.Repository.Id);

        Assert.True(
            report.Result.Certified,
            string.Join(Environment.NewLine, report.Result.Findings.Where(finding => !finding.Passed).Select(finding => $"{finding.Id}: {string.Join("; ", finding.Diagnostics)}")));
        Assert.Equal(0, report.Result.FailedFindingCount);
        Assert.All(report.Result.Findings, finding => Assert.True(finding.Passed, finding.Id));
        Assert.NotNull(latest);
        Assert.Equal(report.ReportId, latest.ReportId);
        Assert.Equal(harness.Repository.Id, report.Governance.RepositoryId);
        Assert.Equal(DecisionSessionHealthStatus.Healthy, report.Health.OverallStatus);
    }

    [Fact]
    public async Task CertificationFailsWhenRegistryReportsDuplicateActiveSessions()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection active = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-2));
        DecisionSessionLifecycleProjection projection = CreateProjection(
            harness.Repository.Id,
            active,
            now,
            activeSessionCount: 2,
            diagnosticsErrors: ["More than one active decision session exists for this repository."]);
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, active.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Unhealthy, now);
        var service = CreateService(harness, projection, history, health, now);

        DecisionSessionCertificationReport report = await service.GetCurrentReportAsync(harness.Repository.Id);

        Assert.False(report.Result.Certified);
        DecisionSessionCertificationFinding finding = Assert.Single(report.Result.Findings, finding => finding.Id == "registry-single-active-session");
        Assert.False(finding.Passed);
        Assert.Contains(finding.Diagnostics, diagnostic => diagnostic.Contains("More than one active", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CertificationFailsUnsafeTransferWhenEligibilityIsBlocked()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection source = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Retired, now.AddHours(-3));
        DecisionSessionProjection target = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-1));
        DecisionSessionLifecycleProjection projection = CreateProjection(
            harness.Repository.Id,
            target,
            now,
            sessions: [source, target],
            eligibilityStatus: DecisionSessionTransferEligibilityStatus.Blocked,
            transfer: CreateTransferProjection(harness.Repository.Id, source.Id, target.Id, "continuity.test.json", now));
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, target.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Warning, now);
        var service = CreateService(harness, projection, history, health, now);

        DecisionSessionCertificationReport report = await service.GetCurrentReportAsync(harness.Repository.Id);

        DecisionSessionCertificationFinding finding = Assert.Single(report.Result.Findings, finding => finding.Id == "eligibility-prevents-unsafe-transfer");
        Assert.False(finding.Passed);
        Assert.Contains("blocked", string.Join(" ", finding.Evidence), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CertificationFailsTransferWithoutContinuityArtifact()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection source = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Retired, now.AddHours(-3));
        DecisionSessionProjection target = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-1));
        DecisionSessionLifecycleProjection projection = CreateProjection(
            harness.Repository.Id,
            target,
            now,
            sessions: [source, target],
            artifacts: [],
            transfer: CreateTransferProjection(harness.Repository.Id, source.Id, target.Id, null, now));
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, target.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Warning, now);
        var service = CreateService(harness, projection, history, health, now);

        DecisionSessionCertificationReport report = await service.GetCurrentReportAsync(harness.Repository.Id);

        DecisionSessionCertificationFinding finding = Assert.Single(report.Result.Findings, finding => finding.Id == "transfer-continuity-artifact-valid");
        Assert.False(finding.Passed);
        Assert.Contains(finding.Diagnostics, diagnostic => diagnostic.Contains("without a continuity artifact", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CertificationFailsHealthyReportThatContradictsRegistryErrors()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection active = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-2));
        DecisionSessionLifecycleProjection projection = CreateProjection(
            harness.Repository.Id,
            active,
            now,
            diagnosticsErrors: ["Registry could not be read."]);
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, active.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Healthy, now);
        var service = CreateService(harness, projection, history, health, now);

        DecisionSessionCertificationReport report = await service.GetCurrentReportAsync(harness.Repository.Id);

        DecisionSessionCertificationFinding finding = Assert.Single(report.Result.Findings, finding => finding.Id == "health-does-not-contradict-evidence");
        Assert.False(finding.Passed);
        Assert.Contains(finding.Diagnostics, diagnostic => diagnostic.Contains("contradictory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CertificationFailsWhenAnalysisContradictsDeterministicInputs()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection active = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-2));
        DecisionSessionLifecycleProjection projection = CreateProjection(
            harness.Repository.Id,
            active,
            now,
            mutateEconomics: economics => economics with
            {
                Economics = economics.Economics with { EstimatedReuseValue = economics.Economics.EstimatedReuseValue + 0.1m }
            });
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, active.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Warning, now);
        var service = CreateService(harness, projection, history, health, now);

        DecisionSessionCertificationReport report = await service.GetCurrentReportAsync(harness.Repository.Id);

        DecisionSessionCertificationFinding finding = Assert.Single(report.Result.Findings, finding => finding.Id == "analysis-determinism-evidence-present");
        Assert.False(finding.Passed);
        Assert.Contains(finding.Diagnostics, diagnostic => diagnostic.Contains("Economics reuse value", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationFailsWhenPolicyContradictsDeterministicScores()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection active = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-2));
        DecisionSessionLifecycleProjection projection = CreateProjection(
            harness.Repository.Id,
            active,
            now,
            mutatePolicy: policy => policy with
            {
                Evaluation = policy.Evaluation with { Decision = DecisionSessionLifecycleDecision.Continue }
            });
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, active.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Warning, now);
        var service = CreateService(harness, projection, history, health, now);

        DecisionSessionCertificationReport report = await service.GetCurrentReportAsync(harness.Repository.Id);

        DecisionSessionCertificationFinding finding = Assert.Single(report.Result.Findings, finding => finding.Id == "policy-determinism-evidence-present");
        Assert.False(finding.Passed);
        Assert.Contains(finding.Diagnostics, diagnostic => diagnostic.Contains("Policy decision", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationFailsMissingDerivedSnapshotsWithoutRecoveryFindings()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection active = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-2));
        DecisionSessionLifecycleProjection projection = CreateProjection(
            harness.Repository.Id,
            active,
            now,
            omitMetrics: true);
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, active.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Warning, now);
        var service = CreateService(harness, projection, history, health, now);

        DecisionSessionCertificationReport report = await service.GetCurrentReportAsync(harness.Repository.Id);

        DecisionSessionCertificationFinding finding = Assert.Single(report.Result.Findings, finding => finding.Id == "recovery-rebuilds-derived-evidence");
        Assert.False(finding.Passed);
        Assert.Contains(finding.Diagnostics, diagnostic => diagnostic.Contains("lack recovery findings", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DiagnosticsCertificationSurfacesContinueAndTransferPolicyEvidence()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection active = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-2));
        DecisionSessionLifecycleProjection transferProjection = CreateProjection(harness.Repository.Id, active, now);
        DecisionSessionLifecycleProjection continueProjection = CreateProjection(
            harness.Repository.Id,
            active,
            now,
            eligibilityStatus: DecisionSessionTransferEligibilityStatus.NotApplicable,
            mutatePolicy: policy => policy with
            {
                Evaluation = new DecisionSessionLifecycleEvaluation(
                    DecisionSessionLifecycleDecision.Continue,
                    0.8m,
                    0.2m,
                    "Policy decided Continue.",
                    ["reuse value"],
                    now),
                Diagnostics = policy.Diagnostics with
                {
                    ReuseScore = policy.Diagnostics.ReuseScore with { Score = 0.8m },
                    TransferScore = policy.Diagnostics.TransferScore with { Score = 0.2m }
                }
            });
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, active.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Healthy, now);

        DecisionSessionCertificationReport transferReport =
            await CreateService(harness, transferProjection, history, health, now).GetCurrentReportAsync(harness.Repository.Id);
        DecisionSessionCertificationReport continueReport =
            await CreateService(harness, continueProjection, history, health, now).GetCurrentReportAsync(harness.Repository.Id);

        DecisionSessionCertificationFinding transferFinding = Assert.Single(
            transferReport.Result.Findings,
            finding => finding.Id == "diagnostics-explain-lifecycle-state");
        DecisionSessionCertificationFinding continueFinding = Assert.Single(
            continueReport.Result.Findings,
            finding => finding.Id == "diagnostics-explain-lifecycle-state");
        Assert.True(transferFinding.Passed, string.Join("; ", transferFinding.Diagnostics));
        Assert.True(continueFinding.Passed, string.Join("; ", continueFinding.Diagnostics));
        Assert.Contains(transferFinding.Evidence, evidence => evidence.Contains("policy:Transfer", StringComparison.Ordinal));
        Assert.Contains(continueFinding.Evidence, evidence => evidence.Contains("policy:Continue", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(DecisionSessionTransferEligibilityStatus.Blocked)]
    [InlineData(DecisionSessionTransferEligibilityStatus.Deferred)]
    public async Task DiagnosticsCertificationRequiresEligibilityFindingsForBlockedOrDeferredStatus(
        DecisionSessionTransferEligibilityStatus status)
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection active = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-2));
        DecisionSessionLifecycleProjection projection = CreateProjection(
            harness.Repository.Id,
            active,
            now,
            eligibilityStatus: status,
            omitEligibilityFindings: true);
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, active.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Warning, now);
        var service = CreateService(harness, projection, history, health, now);

        DecisionSessionCertificationReport report = await service.GetCurrentReportAsync(harness.Repository.Id);

        DecisionSessionCertificationFinding finding = Assert.Single(report.Result.Findings, finding => finding.Id == "diagnostics-explain-lifecycle-state");
        Assert.False(finding.Passed);
        Assert.Contains(finding.Diagnostics, diagnostic => diagnostic.Contains($"Transfer eligibility status {status}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiagnosticsCertificationRequiresRecoveryFindingsWarningsOrEvents()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection active = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-2));
        DecisionSessionRecoveryResult recovery = CreateRecoveryResult(harness.Repository.Id, active.Id, now, includeExplanation: false);
        DecisionSessionLifecycleProjection projection = CreateProjection(
            harness.Repository.Id,
            active,
            now,
            recoveryResults: [recovery]);
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, active.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Warning, now);
        var service = CreateService(harness, projection, history, health, now);

        DecisionSessionCertificationReport report = await service.GetCurrentReportAsync(harness.Repository.Id);

        DecisionSessionCertificationFinding finding = Assert.Single(report.Result.Findings, finding => finding.Id == "diagnostics-explain-lifecycle-state");
        Assert.False(finding.Passed);
        Assert.Contains(finding.Diagnostics, diagnostic => diagnostic.Contains($"Recovery {recovery.RecoveryId}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiagnosticsCertificationRequiresFailedTransferDiagnostics()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection active = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-2));
        DecisionSessionTransferEventProjection failedTransfer = CreateTransferProjection(
            harness.Repository.Id,
            active.Id,
            null,
            null,
            now,
            succeeded: false,
            includeDiagnostics: false);
        DecisionSessionLifecycleProjection projection = CreateProjection(
            harness.Repository.Id,
            active,
            now,
            transfer: failedTransfer);
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, active.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Warning, now);
        var service = CreateService(harness, projection, history, health, now);

        DecisionSessionCertificationReport report = await service.GetCurrentReportAsync(harness.Repository.Id);

        DecisionSessionCertificationFinding finding = Assert.Single(report.Result.Findings, finding => finding.Id == "diagnostics-explain-lifecycle-state");
        Assert.False(finding.Passed);
        Assert.Contains(finding.Diagnostics, diagnostic => diagnostic.Contains($"Failed transfer {failedTransfer.TransferId}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiagnosticsCertificationAcceptsDuplicateActiveAndMissingDerivedSnapshotExplanations()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSessionProjection active = CreateSessionProjection(harness.Repository.Id, DecisionSessionState.Active, now.AddHours(-2));
        DecisionSessionRecoveryResult recovery = CreateDerivedSnapshotRecoveryResult(harness.Repository.Id, active.Id, now);
        DecisionSessionLifecycleProjection projection = CreateProjection(
            harness.Repository.Id,
            active,
            now,
            activeSessionCount: 2,
            diagnosticsErrors: ["More than one active decision session exists for this repository."],
            recoveryResults: [recovery],
            omitMetrics: true);
        DecisionSessionLifecycleHistory history = CreateHistory(harness.Repository.Id, active.Id, now, includeRecovery: true);
        DecisionSessionHealthAssessment health = CreateHealth(harness.Repository.Id, DecisionSessionHealthStatus.Unhealthy, now);
        var service = CreateService(harness, projection, history, health, now);

        DecisionSessionCertificationReport report = await service.GetCurrentReportAsync(harness.Repository.Id);

        DecisionSessionCertificationFinding diagnostics = Assert.Single(report.Result.Findings, finding => finding.Id == "diagnostics-explain-lifecycle-state");
        DecisionSessionCertificationFinding recoveryFinding = Assert.Single(report.Result.Findings, finding => finding.Id == "recovery-rebuilds-derived-evidence");
        Assert.True(diagnostics.Passed, string.Join("; ", diagnostics.Diagnostics));
        Assert.True(recoveryFinding.Passed, string.Join("; ", recoveryFinding.Diagnostics));
        Assert.Contains(diagnostics.Evidence, evidence => evidence.Contains("projection-errors:1", StringComparison.Ordinal));
        Assert.Contains(diagnostics.Evidence, evidence => evidence.Contains($"recovery:{recovery.RecoveryId}", StringComparison.Ordinal));
    }

    private static DecisionSessionCertificationService CreateService(
        DecisionSessionTestHarness harness,
        DecisionSessionLifecycleProjection projection,
        DecisionSessionLifecycleHistory history,
        DecisionSessionHealthAssessment health,
        DateTimeOffset now)
    {
        return new DecisionSessionCertificationService(
            harness.RepositoryService,
            harness.RepositoryStore,
            new StubDecisionSessionObservabilityService(projection, history, health),
            new FixedTimeProvider(now));
    }

    private static DecisionSessionLifecycleProjection CreateProjection(
        Guid repositoryId,
        DecisionSessionProjection active,
        DateTimeOffset now,
        int activeSessionCount = 1,
        IReadOnlyList<string>? diagnosticsErrors = null,
        IReadOnlyList<DecisionSessionProjection>? sessions = null,
        IReadOnlyList<DecisionSessionContinuityArtifactProjection>? artifacts = null,
        DecisionSessionTransferEligibilityStatus eligibilityStatus = DecisionSessionTransferEligibilityStatus.Eligible,
        DecisionSessionTransferEventProjection? transfer = null,
        Func<DecisionSessionEconomicsSnapshot, DecisionSessionEconomicsSnapshot>? mutateEconomics = null,
        Func<DecisionSessionLifecycleSnapshot, DecisionSessionLifecycleSnapshot>? mutatePolicy = null,
        IReadOnlyList<DecisionSessionRecoveryResult>? recoveryResults = null,
        bool omitEligibilityFindings = false,
        bool omitMetrics = false)
    {
        DecisionSessionMetricsSnapshot metrics = CreateMetricsSnapshot(repositoryId, now);
        DecisionSessionEconomicsSnapshot economics = mutateEconomics?.Invoke(CreateEconomicsSnapshot(repositoryId, metrics, now)) ??
            CreateEconomicsSnapshot(repositoryId, metrics, now);
        DecisionSessionCoherenceSnapshot coherence = CreateCoherenceSnapshot(repositoryId, metrics, economics, now);
        DecisionSessionLifecycleSnapshot policy = mutatePolicy?.Invoke(CreatePolicySnapshot(repositoryId, active, metrics, economics, coherence, now)) ??
            CreatePolicySnapshot(repositoryId, active, metrics, economics, coherence, now);
        DecisionSessionTransferEligibilitySnapshot eligibility = CreateEligibilitySnapshot(
            repositoryId,
            active.Id,
            policy.Evaluation,
            eligibilityStatus,
            now,
            omitEligibilityFindings);
        IReadOnlyList<DecisionSessionContinuityArtifactProjection> artifactList = artifacts ??
            (transfer?.ContinuityArtifactId is null ? [] : [CreateArtifact(active.Id, transfer.ContinuityArtifactId, now)]);
        IReadOnlyList<DecisionSessionTransferEventProjection> transferList = transfer is null ? [] : [transfer];
        return new DecisionSessionLifecycleProjection(
            repositoryId,
            active,
            sessions ?? [active],
            omitMetrics ? null : metrics,
            omitMetrics ? null : new DecisionSessionSizeProjection(
                metrics.Metrics.EstimatedTokenCount,
                metrics.Metrics.ContextByteSize,
                metrics.Metrics.ReasoningEventCount,
                metrics.Metrics.DecisionCount,
                metrics.Statistics.SessionAge,
                metrics.Statistics.IdleDuration,
                metrics.Cache.EstimatedCacheMissRisk,
                metrics.Metrics.MeasuredAt),
            economics,
            coherence,
            policy,
            eligibility,
            null,
            artifactList,
            [],
            [],
            transferList,
            recoveryResults ?? [],
            new DecisionSessionDiagnostics(
                repositoryId,
                diagnosticsErrors is null || diagnosticsErrors.Count == 0,
                sessions?.Count ?? 1,
                activeSessionCount,
                diagnosticsErrors ?? [],
                [],
                now),
            now);
    }

    private static DecisionSessionProjection CreateSessionProjection(Guid repositoryId, DecisionSessionState state, DateTimeOffset createdAt)
    {
        return new DecisionSessionProjection(
            DecisionSessionId.New(),
            repositoryId,
            state,
            createdAt,
            state == DecisionSessionState.Active ? createdAt.AddSeconds(1) : createdAt.AddSeconds(1),
            state == DecisionSessionState.Retired ? createdAt.AddHours(1) : null,
            "test");
    }

    private static DecisionSessionMetricsSnapshot CreateMetricsSnapshot(Guid repositoryId, DateTimeOffset now)
    {
        var metrics = new DecisionSessionMetrics(100, 400, 1, 1, 1, 1, 0, 0, 1, now, now);
        var statistics = new DecisionSessionStatistics(TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.Zero, 100m, 1m);
        var activity = new DecisionSessionActivity(4, now, TimeSpan.Zero, 1m);
        var growth = new DecisionSessionGrowth(400, 100, TimeSpan.FromHours(1), 100m);
        var cache = new DecisionSessionCacheMetrics(TimeSpan.FromHours(1), 0.1m, now.AddHours(1));
        return new DecisionSessionMetricsSnapshot(
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
    }

    private static DecisionSessionEconomicsSnapshot CreateEconomicsSnapshot(
        Guid repositoryId,
        DecisionSessionMetricsSnapshot metrics,
        DateTimeOffset now)
    {
        var economics = new DecisionSessionEconomics(0.2m, 0.8m, 0.2m, 0.2m, 0.5m, 0.2m, 0.1m);
        return new DecisionSessionEconomicsSnapshot(
            repositoryId,
            economics,
            new DecisionSessionEconomicsDiagnostics(
                repositoryId,
                now,
                new DecisionSessionEconomicsInputs(metrics.Metrics, metrics.Statistics, metrics.Activity, metrics.Growth, metrics.Cache),
                new ReuseValueAssessment(0.2m, 0m, 0m, 0m, 0m),
                new TransferValueAssessment(0.8m, 0m, 0m, 0m, 0m),
                new CacheBenefitAssessment(0.2m, 0m, 0.1m, 0m),
                new CacheRiskAssessment(0.1m, TimeSpan.FromHours(1), now.AddHours(1)),
                new ContinuityBenefitAssessment(0.5m, 0m, 0m, 0m),
                [],
                []),
            now);
    }

    private static DecisionSessionCoherenceSnapshot CreateCoherenceSnapshot(
        Guid repositoryId,
        DecisionSessionMetricsSnapshot metrics,
        DecisionSessionEconomicsSnapshot economics,
        DateTimeOffset now)
    {
        var coherence = new DecisionSessionCoherence(0.410m, 0.8m, 0.5m, 0.5m, 0.8m);
        return new DecisionSessionCoherenceSnapshot(
            repositoryId,
            coherence,
            new DecisionSessionCoherenceDiagnostics(
                repositoryId,
                now,
                new DecisionSessionCoherenceInputs(metrics.Metrics, metrics.Statistics, metrics.Cache, economics.Economics, 3, 1, 0, 1, 1, 0),
                new FragmentationAssessment(0.8m, 0m, 0m, 0m),
                new DensityAssessment(0.5m, 0.5m, 3, 1),
                new ContinuityQualityAssessment(0.5m, 0m, 0m, 0m, 0m),
                new TransferPressureAssessment(0.8m, 0m, 0m, 0m, 0m, 0m),
                [],
                []),
            now);
    }

    private static DecisionSessionLifecycleSnapshot CreatePolicySnapshot(
        Guid repositoryId,
        DecisionSessionProjection active,
        DecisionSessionMetricsSnapshot metrics,
        DecisionSessionEconomicsSnapshot economics,
        DecisionSessionCoherenceSnapshot coherence,
        DateTimeOffset now)
    {
        DecisionSession session = new(
            active.Id,
            repositoryId,
            active.State,
            active.CreatedAt,
            active.ActivatedAt,
            active.RetiredAt,
            new DecisionSessionOwnership(repositoryId, active.CreatedBy, active.CreatedAt),
            new DecisionSessionMetadata(UpdatedAt: now));
        var evaluation = new DecisionSessionLifecycleEvaluation(
            DecisionSessionLifecycleDecision.Transfer,
            0.2m,
            0.8m,
            "Policy decided Transfer.",
            ["transfer pressure"],
            now);
        return new DecisionSessionLifecycleSnapshot(
            repositoryId,
            evaluation,
            new DecisionSessionLifecycleDiagnostics(
                repositoryId,
                now,
                new DecisionSessionLifecycleInputs(session, metrics.Metrics, metrics.Statistics, metrics.Cache, economics.Economics, coherence.Coherence),
                new ReuseScoreAssessment(0.2m, 0m, 0m, 0m, 0m),
                new TransferScoreAssessment(0.8m, 0m, 0m, 0m, 0m, 0m),
                [],
                []),
            now);
    }

    private static DecisionSessionTransferEligibilitySnapshot CreateEligibilitySnapshot(
        Guid repositoryId,
        DecisionSessionId sourceSessionId,
        DecisionSessionLifecycleEvaluation evaluation,
        DecisionSessionTransferEligibilityStatus status,
        DateTimeOffset now,
        bool omitFindings = false)
    {
        return new DecisionSessionTransferEligibilitySnapshot(
            repositoryId,
            new DecisionSessionTransferEligibility(
                status,
                evaluation,
                sourceSessionId,
                omitFindings
                    ? []
                    : [new DecisionSessionTransferEligibilityFinding(status.ToString().ToLowerInvariant(), status == DecisionSessionTransferEligibilityStatus.Eligible ? "Info" : "Blocked", $"Eligibility is {status}.")],
                now),
            new DecisionSessionTransferEligibilityDiagnostics(
                repositoryId,
                now,
                new DecisionSessionTransferEligibilityInputs(
                    evaluation,
                    new DecisionSessionDiagnostics(repositoryId, true, 1, 1, [], [], now),
                    null,
                    null),
                [],
                []),
            now);
    }

    private static DecisionSessionContinuityArtifactProjection CreateArtifact(
        DecisionSessionId sourceSessionId,
        string artifactId,
        DateTimeOffset now)
    {
        DecisionSessionContinuityReference[] references =
        [
            new("decisions", "decision-records", 1, 10, now, "decisions"),
            new("reasoning", "reasoning-events", 1, 10, now, "reasoning"),
            new("operational-context", "context-proposals", 1, 10, now, "context")
        ];
        return new DecisionSessionContinuityArtifactProjection(
            artifactId,
            "fingerprint",
            sourceSessionId,
            null,
            [references[0]],
            [references[1]],
            [references[2]],
            now,
            []);
    }

    private static DecisionSessionTransferEventProjection CreateTransferProjection(
        Guid repositoryId,
        DecisionSessionId sourceSessionId,
        DecisionSessionId? targetSessionId,
        string? artifactId,
        DateTimeOffset now,
        bool succeeded = true,
        bool includeDiagnostics = true)
    {
        string transferId = $"transfer.{now.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.{sourceSessionId}.json";
        return new DecisionSessionTransferEventProjection(
            transferId,
            sourceSessionId,
            targetSessionId,
            now,
            succeeded ? now.AddSeconds(1) : null,
            succeeded,
            "Policy decided Transfer.",
            100,
            DecisionSessionLifecycleDecision.Transfer,
            0.2m,
            0.8m,
            DecisionSessionTransferEligibilityStatus.Eligible,
            artifactId,
            includeDiagnostics || succeeded
                ? [
                    new DecisionSessionTransferEvent(
                        $"{transferId}.started",
                        DecisionSessionTransferEventType.Started,
                        repositoryId,
                        sourceSessionId,
                        null,
                        artifactId,
                        now,
                        "Started.",
                        []),
                    new DecisionSessionTransferEvent(
                        $"{transferId}.{(succeeded ? "completed" : "failed")}",
                        succeeded ? DecisionSessionTransferEventType.Completed : DecisionSessionTransferEventType.Failed,
                        repositoryId,
                        sourceSessionId,
                        targetSessionId,
                        artifactId,
                        now.AddSeconds(1),
                        succeeded ? "Completed." : "Failed.",
                        includeDiagnostics ? ["Transfer failed because eligibility changed."] : [])
                ]
                : [],
            includeDiagnostics && !succeeded ? ["Transfer failed because eligibility changed."] : []);
    }

    private static DecisionSessionRecoveryResult CreateRecoveryResult(
        Guid repositoryId,
        DecisionSessionId activeSessionId,
        DateTimeOffset now,
        bool includeExplanation)
    {
        return new DecisionSessionRecoveryResult(
            $"recovery.{now.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.json",
            repositoryId,
            true,
            activeSessionId,
            1,
            includeExplanation ? [new DecisionSessionRecoveryFinding("RegistryHealthy", "Info", "Recovery validated registry.", activeSessionId, null)] : [],
            new DecisionSessionRecoveryDiagnostics(
                repositoryId,
                now,
                new DecisionSessionDiagnostics(repositoryId, true, 1, 1, [], [], now),
                [],
                includeExplanation ? ["Recovery validated registry."] : []),
            includeExplanation
                ? [new DecisionSessionRecoveryEvent("event.recovered", repositoryId, "Recovered", now, "Recovery completed.", [])]
                : [],
            now);
    }

    private static DecisionSessionRecoveryResult CreateDerivedSnapshotRecoveryResult(
        Guid repositoryId,
        DecisionSessionId activeSessionId,
        DateTimeOffset now)
    {
        string[] codes =
        [
            "MetricsSnapshotRebuilt",
            "EconomicsSnapshotRebuilt",
            "CoherenceSnapshotRebuilt",
            "LifecyclePolicySnapshotRebuilt",
            "TransferEligibilitySnapshotRebuilt"
        ];
        return new DecisionSessionRecoveryResult(
            $"recovery.{now.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.json",
            repositoryId,
            true,
            activeSessionId,
            1,
            codes.Select(code => new DecisionSessionRecoveryFinding(code, "Info", $"{code} during recovery.", activeSessionId, code)).ToArray(),
            new DecisionSessionRecoveryDiagnostics(
                repositoryId,
                now,
                new DecisionSessionDiagnostics(repositoryId, false, 1, 2, ["More than one active decision session exists for this repository."], [], now),
                [],
                ["Recovery rebuilt missing derived snapshots."]),
            [new DecisionSessionRecoveryEvent("event.recovered", repositoryId, "Recovered", now, "Recovery rebuilt missing derived snapshots.", [])],
            now);
    }

    private static DecisionSessionLifecycleHistory CreateHistory(
        Guid repositoryId,
        DecisionSessionId activeSessionId,
        DateTimeOffset now,
        bool includeRecovery)
    {
        List<DecisionSessionLifecycleHistoryEvent> events =
        [
            new(
                DecisionSessionLifecycleHistoryEventType.Activated,
                now.AddHours(-1),
                activeSessionId,
                null,
                null,
                null,
                null,
                "Decision session was activated.",
                [])
        ];
        if (includeRecovery)
        {
            events.Add(new DecisionSessionLifecycleHistoryEvent(
                DecisionSessionLifecycleHistoryEventType.Recovered,
                now,
                activeSessionId,
                null,
                null,
                null,
                "recovery.test.json",
                "Decision session recovery completed.",
                []));
        }

        return new DecisionSessionLifecycleHistory(repositoryId, events, now);
    }

    private static DecisionSessionHealthAssessment CreateHealth(
        Guid repositoryId,
        DecisionSessionHealthStatus status,
        DateTimeOffset now)
    {
        var dimension = new DecisionSessionHealthDimension("Registry", status, [], ["1 active"]);
        var trace = new DecisionSessionInfluenceTrace(repositoryId, null, null, null, [], [], now);
        return new DecisionSessionHealthAssessment(repositoryId, [dimension], trace, now);
    }

    private sealed class StubDecisionSessionObservabilityService(
        DecisionSessionLifecycleProjection projection,
        DecisionSessionLifecycleHistory history,
        DecisionSessionHealthAssessment health) : IDecisionSessionObservabilityService
    {
        public Task<DecisionSessionLifecycleProjection> GetProjectionAsync(Guid repositoryId) => Task.FromResult(projection);

        public Task<DecisionSessionLifecycleHistory> GetHistoryAsync(Guid repositoryId) => Task.FromResult(history);

        public Task<DecisionSessionInfluenceTrace> GetInfluenceTraceAsync(Guid repositoryId) => Task.FromResult(health.InfluenceTrace);

        public Task<DecisionSessionHealthAssessment> GetHealthAsync(Guid repositoryId) => Task.FromResult(health);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
