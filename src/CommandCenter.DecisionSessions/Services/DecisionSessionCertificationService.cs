using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionCertificationService(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository,
    IDecisionSessionObservabilityService observabilityService,
    TimeProvider timeProvider) : IDecisionSessionCertificationService
{
    public async Task<DecisionSessionCertificationReport?> GetLatestReportAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return (await sessionRepository.ListCertificationReportsAsync(repository))
            .OrderByDescending(report => report.GeneratedAt)
            .ThenByDescending(report => report.ReportId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public async Task<DecisionSessionCertificationReport> GetCurrentReportAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await BuildReportAsync(repository);
    }

    public async Task<DecisionSessionCertificationReport> RunCertificationAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionSessionCertificationReport report = await BuildReportAsync(repository);
        await sessionRepository.WriteCertificationReportAsync(repository, report);
        return report;
    }

    private async Task<DecisionSessionCertificationReport> BuildReportAsync(Repository repository)
    {
        DecisionSessionLifecycleProjection projection = await observabilityService.GetProjectionAsync(repository.Id);
        DecisionSessionLifecycleHistory history = await observabilityService.GetHistoryAsync(repository.Id);
        DecisionSessionHealthAssessment health = await observabilityService.GetHealthAsync(repository.Id);
        DateTimeOffset generatedAt = timeProvider.GetUtcNow();

        var findings = new List<DecisionSessionCertificationFinding>
        {
            CertifyAuthorityBoundary(),
            CertifySingleActiveSession(projection),
            CertifyAnalysisEvidence(projection),
            CertifyCacheEvidence(projection),
            CertifyPolicyEvidence(projection),
            CertifyEligibility(projection),
            CertifyContinuityArtifacts(projection),
            CertifyTransfers(projection),
            CertifyRecovery(projection, history),
            CertifyContinuity(projection, history),
            CertifyWorkflowConsumptionBoundary(),
            CertifyDiagnostics(projection, history),
            CertifyHealth(projection, health)
        };
        IReadOnlyList<string> failures = findings
            .Where(finding => !finding.Passed)
            .Select(finding => $"{finding.Id}: {finding.Summary}")
            .ToArray();
        string reportId = $"certification.{generatedAt.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.json";
        string inputFingerprint = Fingerprint(
            projection.RepositoryId,
            projection.ActiveSession?.Id.ToString() ?? "none",
            projection.Diagnostics.ActiveSessionCount,
            projection.Metrics,
            projection.Economics,
            projection.Coherence,
            projection.Policy?.Evaluation,
            projection.TransferEligibility?.Eligibility.Status,
            projection.ContinuityArtifacts.Select(artifact => artifact.ContinuityFingerprint).Order(StringComparer.Ordinal).ToArray(),
            projection.RecentTransfers.Select(transfer => $"{transfer.TransferId}:{transfer.Succeeded}:{transfer.TargetSessionId}").Order(StringComparer.Ordinal).ToArray(),
            projection.RecentRecoveryResults.Select(recovery => $"{recovery.RecoveryId}:{recovery.Succeeded}:{recovery.ActiveSessionCount}").Order(StringComparer.Ordinal).ToArray(),
            health.Dimensions.Select(dimension => $"{dimension.Name}:{dimension.Status}:{string.Join("|", dimension.Findings)}").Order(StringComparer.Ordinal).ToArray());

        var result = new DecisionSessionCertificationResult(
            reportId,
            repository.Id,
            generatedAt,
            inputFingerprint,
            failures.Count == 0,
            findings.Count(finding => finding.Passed),
            findings.Count(finding => !finding.Passed),
            findings,
            failures,
            [
                $"Certification observed {projection.Sessions.Count} decision sessions.",
                $"Certification observed {projection.ContinuityArtifacts.Count} continuity artifacts.",
                $"Certification observed {projection.RecentTransfers.Count} recent transfers.",
                $"Certification observed {projection.RecentRecoveryResults.Count} recent recovery results."
            ]);
        var governance = new DecisionSessionGovernanceReport(
            repository.Id,
            projection.ActiveSession,
            projection.Diagnostics.SessionCount,
            projection.Diagnostics.ActiveSessionCount,
            projection.Policy?.Evaluation.Decision,
            projection.TransferEligibility?.Eligibility.Status,
            projection.Diagnostics.Errors.Concat(projection.Diagnostics.Warnings).ToArray(),
            generatedAt);
        DecisionSessionHealthStatus overallStatus = ResolveOverallHealth(health);
        var healthReport = new DecisionSessionHealthReport(
            repository.Id,
            overallStatus,
            health.Dimensions,
            health.InfluenceTrace.Diagnostics,
            generatedAt);
        return new DecisionSessionCertificationReport(reportId, repository.Id, generatedAt, result, governance, healthReport);
    }

    private static DecisionSessionCertificationFinding CertifyAuthorityBoundary()
    {
        return new DecisionSessionCertificationFinding(
            "authority-observational-certification",
            "Authority",
            true,
            "Certification is observational and writes only certification report evidence.",
            "The certification service depends on repository lookup, decision-session report persistence, and read-only observability; it does not receive registry, transfer, lifecycle policy, or eligibility mutator services.",
            [
                "service:DecisionSessionCertificationService",
                "dependency:IRepositoryService",
                "dependency:IDecisionSessionRepository",
                "dependency:IDecisionSessionObservabilityService"
            ],
            []);
    }

    private static DecisionSessionCertificationFinding CertifySingleActiveSession(DecisionSessionLifecycleProjection projection)
    {
        var diagnostics = new List<string>();
        if (projection.Diagnostics.ActiveSessionCount > 1)
        {
            diagnostics.Add($"Registry reports {projection.Diagnostics.ActiveSessionCount} active decision sessions.");
        }

        diagnostics.AddRange(projection.Diagnostics.Errors.Where(error =>
            error.Contains("More than one active", StringComparison.OrdinalIgnoreCase)));
        bool passed = diagnostics.Count == 0;
        return new DecisionSessionCertificationFinding(
            "registry-single-active-session",
            "Single active session",
            passed,
            passed
                ? "Registry evidence has at most one active decision session."
                : "Registry evidence violates the single-active-session invariant.",
            "A repository may have zero active sessions before initialization or during diagnostic failure states, but it must never have more than one active session.",
            [$"sessions:{projection.Diagnostics.SessionCount}", $"active:{projection.Diagnostics.ActiveSessionCount}"],
            diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static DecisionSessionCertificationFinding CertifyAnalysisEvidence(DecisionSessionLifecycleProjection projection)
    {
        var diagnostics = new List<string>();
        if (projection.Metrics is null)
        {
            diagnostics.Add("Metrics snapshot is missing.");
        }

        if (projection.Economics is null)
        {
            diagnostics.Add("Economics snapshot is missing.");
        }

        if (projection.Coherence is null)
        {
            diagnostics.Add("Coherence snapshot is missing.");
        }

        bool passed = diagnostics.Count == 0;
        return new DecisionSessionCertificationFinding(
            "analysis-determinism-evidence-present",
            "Analysis determinism",
            passed,
            passed
                ? "Metrics, economics, and coherence snapshots are present for deterministic comparison."
                : "Analysis snapshots are incomplete.",
            "Certification requires rebuildable analysis evidence before deeper deterministic comparison can prove identical inputs produce identical outputs.",
            FilterNull([
                projection.Metrics is null ? null : $"metrics:{projection.Metrics.GeneratedAt:O}:{projection.Metrics.Metrics.EstimatedTokenCount}",
                projection.Economics is null ? null : $"economics:{projection.Economics.GeneratedAt:O}:{projection.Economics.Economics.EstimatedReuseValue}:{projection.Economics.Economics.EstimatedTransferValue}",
                projection.Coherence is null ? null : $"coherence:{projection.Coherence.GeneratedAt:O}:{projection.Coherence.Coherence.CoherenceScore}"
            ]),
            diagnostics);
    }

    private static DecisionSessionCertificationFinding CertifyCacheEvidence(DecisionSessionLifecycleProjection projection)
    {
        var diagnostics = new List<string>();
        if (projection.Metrics is null)
        {
            diagnostics.Add("Metrics snapshot is missing TTL and cache-risk evidence.");
        }
        else
        {
            if (projection.Metrics.Cache.EstimatedCacheTtl < TimeSpan.Zero)
            {
                diagnostics.Add("Estimated cache TTL is negative.");
            }

            if (projection.Metrics.Cache.EstimatedCacheMissRisk < 0m || projection.Metrics.Cache.EstimatedCacheMissRisk > 1m)
            {
                diagnostics.Add("Estimated cache miss risk must be between 0 and 1.");
            }
        }

        bool passed = diagnostics.Count == 0;
        return new DecisionSessionCertificationFinding(
            "analysis-cache-ttl-risk-present",
            "TTL and cache risk",
            passed,
            passed
                ? "TTL and cache miss risk evidence are present and bounded."
                : "TTL or cache miss risk evidence is missing or invalid.",
            "Lifecycle analysis must expose estimated cache TTL and cache miss risk because transfer economics depend on both.",
            projection.Metrics is null
                ? []
                : [$"ttl:{projection.Metrics.Cache.EstimatedCacheTtl}", $"risk:{projection.Metrics.Cache.EstimatedCacheMissRisk}"],
            diagnostics);
    }

    private static DecisionSessionCertificationFinding CertifyPolicyEvidence(DecisionSessionLifecycleProjection projection)
    {
        var diagnostics = new List<string>();
        if (projection.Policy is null)
        {
            diagnostics.Add("Lifecycle policy snapshot is missing.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(projection.Policy.Evaluation.Reason))
            {
                diagnostics.Add("Lifecycle policy evaluation is missing its reason.");
            }

            if (projection.Policy.Evaluation.ReuseScore < 0m || projection.Policy.Evaluation.TransferScore < 0m)
            {
                diagnostics.Add("Lifecycle policy scores must be non-negative.");
            }
        }

        bool passed = diagnostics.Count == 0;
        return new DecisionSessionCertificationFinding(
            "policy-determinism-evidence-present",
            "Policy determinism",
            passed,
            passed
                ? "Lifecycle policy evidence is present and explainable."
                : "Lifecycle policy evidence is incomplete.",
            "Certification requires deterministic, explainable policy output before transfer eligibility or execution evidence can be trusted.",
            projection.Policy is null
                ? []
                : [
                    $"decision:{projection.Policy.Evaluation.Decision}",
                    $"reuse:{projection.Policy.Evaluation.ReuseScore}",
                    $"transfer:{projection.Policy.Evaluation.TransferScore}",
                    $"reason:{projection.Policy.Evaluation.Reason}"
                ],
            diagnostics);
    }

    private static DecisionSessionCertificationFinding CertifyEligibility(DecisionSessionLifecycleProjection projection)
    {
        var diagnostics = new List<string>();
        if (projection.TransferEligibility is null)
        {
            diagnostics.Add("Transfer eligibility snapshot is missing.");
        }

        bool hasUnsafeTransfer = projection.TransferEvents.Any(transfer =>
            transfer.Succeeded &&
            (projection.Policy?.Evaluation.Decision != DecisionSessionLifecycleDecision.Transfer ||
                projection.TransferEligibility?.Eligibility.Status is DecisionSessionTransferEligibilityStatus.Blocked
                    or DecisionSessionTransferEligibilityStatus.Deferred
                    or DecisionSessionTransferEligibilityStatus.NotApplicable));
        if (hasUnsafeTransfer)
        {
            diagnostics.Add("A successful transfer is present while current policy or eligibility evidence does not allow transfer.");
        }

        bool passed = diagnostics.Count == 0;
        return new DecisionSessionCertificationFinding(
            "eligibility-prevents-unsafe-transfer",
            "Transfer eligibility",
            passed,
            passed
                ? "Transfer eligibility evidence does not permit unsafe transfer execution."
                : "Transfer eligibility evidence contradicts transfer execution.",
            "Transfer execution is allowed only when policy recommends Transfer and eligibility is Eligible; blocked, deferred, and not-applicable eligibility must prevent execution.",
            FilterNull([
                projection.Policy is null ? null : $"policy:{projection.Policy.Evaluation.Decision}",
                projection.TransferEligibility is null ? null : $"eligibility:{projection.TransferEligibility.Eligibility.Status}",
                $"succeeded-transfers:{projection.TransferEvents.Count(transfer => transfer.Succeeded)}"
            ]),
            diagnostics);
    }

    private static DecisionSessionCertificationFinding CertifyContinuityArtifacts(DecisionSessionLifecycleProjection projection)
    {
        var diagnostics = new List<string>();
        foreach (DecisionSessionTransferEventProjection transfer in projection.TransferEvents.Where(transfer => transfer.Succeeded))
        {
            if (string.IsNullOrWhiteSpace(transfer.ContinuityArtifactId))
            {
                diagnostics.Add($"Transfer {transfer.TransferId} succeeded without a continuity artifact id.");
                continue;
            }

            DecisionSessionContinuityArtifactProjection? artifact = projection.ContinuityArtifacts.FirstOrDefault(candidate =>
                string.Equals(candidate.ArtifactId, transfer.ContinuityArtifactId, StringComparison.Ordinal));
            if (artifact is null)
            {
                diagnostics.Add($"Transfer {transfer.TransferId} references missing continuity artifact {transfer.ContinuityArtifactId}.");
                continue;
            }

            if (artifact.DecisionReferences.Count == 0 ||
                artifact.ReasoningReferences.Count == 0 ||
                artifact.OperationalContextReferences.Count == 0)
            {
                diagnostics.Add($"Continuity artifact {artifact.ArtifactId} lacks decision, reasoning, or operational context references.");
            }

            if (string.IsNullOrWhiteSpace(artifact.ContinuityFingerprint))
            {
                diagnostics.Add($"Continuity artifact {artifact.ArtifactId} lacks a continuity fingerprint.");
            }
        }

        bool passed = diagnostics.Count == 0;
        return new DecisionSessionCertificationFinding(
            "transfer-continuity-artifact-valid",
            "Continuity artifact",
            passed,
            passed
                ? "Successful transfer evidence has valid continuity artifacts."
                : "Transfer evidence lacks valid continuity artifacts.",
            "A transfer must create and retain a first-class continuity artifact with decision, reasoning, operational context, and fingerprint evidence.",
            projection.ContinuityArtifacts.Select(artifact =>
                $"artifact:{artifact.ArtifactId}:{artifact.ContinuityFingerprint}:d={artifact.DecisionReferences.Count}:r={artifact.ReasoningReferences.Count}:c={artifact.OperationalContextReferences.Count}").ToArray(),
            diagnostics);
    }

    private static DecisionSessionCertificationFinding CertifyTransfers(DecisionSessionLifecycleProjection projection)
    {
        var diagnostics = new List<string>();
        foreach (DecisionSessionTransferEventProjection transfer in projection.TransferEvents.Where(transfer => transfer.Succeeded))
        {
            if (transfer.TargetSessionId is null)
            {
                diagnostics.Add($"Transfer {transfer.TransferId} succeeded without a target session.");
            }

            if (transfer.CompletedAt is null)
            {
                diagnostics.Add($"Transfer {transfer.TransferId} succeeded without completion time.");
            }

            if (!transfer.Events.Any(transferEvent => transferEvent.EventType == DecisionSessionTransferEventType.Started) ||
                !transfer.Events.Any(transferEvent => transferEvent.EventType == DecisionSessionTransferEventType.Completed))
            {
                diagnostics.Add($"Transfer {transfer.TransferId} lacks started or completed events.");
            }

            DecisionSessionProjection? source = projection.Sessions.FirstOrDefault(session => session.Id == transfer.SourceSessionId);
            if (source is not null && source.State == DecisionSessionState.Active)
            {
                diagnostics.Add($"Transfer {transfer.TransferId} left source session {source.Id} active.");
            }

            if (transfer.TargetSessionId is not null &&
                !projection.Sessions.Any(session => session.Id == transfer.TargetSessionId && session.State == DecisionSessionState.Active))
            {
                diagnostics.Add($"Transfer {transfer.TransferId} does not have an active replacement session.");
            }
        }

        bool passed = diagnostics.Count == 0;
        return new DecisionSessionCertificationFinding(
            "transfer-preserves-lifecycle-invariants",
            "Transfer",
            passed,
            passed
                ? "Transfer evidence preserves lifecycle invariants."
                : "Transfer evidence violates lifecycle invariants.",
            "A completed transfer must retire the source, activate a replacement, preserve lineage, and maintain the single-active-session invariant.",
            projection.TransferEvents.Select(transfer =>
                $"transfer:{transfer.TransferId}:succeeded={transfer.Succeeded}:source={transfer.SourceSessionId}:target={transfer.TargetSessionId?.ToString() ?? "none"}:artifact={transfer.ContinuityArtifactId ?? "none"}").ToArray(),
            diagnostics);
    }

    private static DecisionSessionCertificationFinding CertifyRecovery(
        DecisionSessionLifecycleProjection projection,
        DecisionSessionLifecycleHistory history)
    {
        var diagnostics = new List<string>();
        diagnostics.AddRange(projection.RecentRecoveryResults
            .Where(recovery => !recovery.Succeeded)
            .Select(recovery => $"Recovery {recovery.RecoveryId} failed."));

        bool missingDerivedSnapshots = projection.Metrics is null ||
            projection.Economics is null ||
            projection.Coherence is null ||
            projection.Policy is null ||
            projection.TransferEligibility is null;
        bool recoveryObserved = history.Events.Any(lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.Recovered);
        if (missingDerivedSnapshots && !recoveryObserved)
        {
            diagnostics.Add("Missing derived snapshots have no recovery history evidence.");
        }

        bool passed = diagnostics.Count == 0;
        return new DecisionSessionCertificationFinding(
            "recovery-rebuilds-derived-evidence",
            "Recovery",
            passed,
            passed
                ? "Recovery evidence does not contradict derived snapshot rebuild requirements."
                : "Recovery evidence is insufficient for derived snapshot rebuild requirements.",
            "Recovery must rebuild missing analysis, policy, and eligibility snapshots or surface diagnostics rather than silently losing lifecycle evidence.",
            [
                $"recovery-results:{projection.RecentRecoveryResults.Count}",
                $"history-recovered-events:{history.Events.Count(lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.Recovered)}",
                $"missing-derived-snapshots:{missingDerivedSnapshots}"
            ],
            diagnostics);
    }

    private static DecisionSessionCertificationFinding CertifyContinuity(
        DecisionSessionLifecycleProjection projection,
        DecisionSessionLifecycleHistory history)
    {
        bool transferHistoryHasLineage = projection.TransferEvents
            .Where(transfer => transfer.Succeeded)
            .All(transfer => transfer.TargetSessionId is not null && !string.IsNullOrWhiteSpace(transfer.ContinuityArtifactId));
        bool artifactHistoryPresent = projection.ContinuityArtifacts.Count == 0 ||
            history.Events.Any(lifecycleEvent => lifecycleEvent.EventType == DecisionSessionLifecycleHistoryEventType.ContinuityArtifactCreated);
        var diagnostics = new List<string>();
        if (!transferHistoryHasLineage)
        {
            diagnostics.Add("Successful transfer history lacks target-session or continuity-artifact lineage.");
        }

        if (!artifactHistoryPresent)
        {
            diagnostics.Add("Continuity artifacts exist without lifecycle history events.");
        }

        bool passed = diagnostics.Count == 0;
        return new DecisionSessionCertificationFinding(
            "continuity-lineage-preserved",
            "Continuity",
            passed,
            passed
                ? "Continuity lineage is preserved in transfer and history evidence."
                : "Continuity lineage evidence is incomplete.",
            "Governance continuity requires transfer lineage and continuity artifact history to remain observable after lifecycle changes.",
            [
                $"artifacts:{projection.ContinuityArtifacts.Count}",
                $"transfers:{projection.TransferEvents.Count}",
                $"history-events:{history.Events.Count}"
            ],
            diagnostics);
    }

    private static DecisionSessionCertificationFinding CertifyWorkflowConsumptionBoundary()
    {
        return new DecisionSessionCertificationFinding(
            "workflow-consumption-read-only-boundary",
            "Workflow integration",
            true,
            "Workflow integration is certified as a read-only consumer boundary from decision-session certification.",
            "Decision-session certification cannot reference workflow implementation because the decision-session project is the governance trunk; workflow-specific consumption is verified by workflow tests and backend endpoint composition.",
            [
                "project-boundary:CommandCenter.DecisionSessions has no CommandCenter.Workflow reference",
                "workflow-consumption:observability projection is the public lifecycle surface"
            ],
            []);
    }

    private static DecisionSessionCertificationFinding CertifyDiagnostics(
        DecisionSessionLifecycleProjection projection,
        DecisionSessionLifecycleHistory history)
    {
        var diagnostics = new List<string>();
        if (history.Events.Count == 0 && projection.Diagnostics.SessionCount > 0)
        {
            diagnostics.Add("Lifecycle history has no events despite session evidence.");
        }

        bool blockedOrFailed = projection.TransferEligibility?.Eligibility.Status is DecisionSessionTransferEligibilityStatus.Blocked
            or DecisionSessionTransferEligibilityStatus.Deferred ||
            projection.RecentRecoveryResults.Any(recovery => !recovery.Succeeded) ||
            projection.Diagnostics.Errors.Count > 0;
        bool hasExplanation = projection.Diagnostics.Errors.Count > 0 ||
            projection.Diagnostics.Warnings.Count > 0 ||
            projection.TransferEligibility?.Eligibility.Findings.Count > 0 ||
            projection.RecentRecoveryResults.Any(recovery => recovery.Findings.Count > 0 || recovery.Diagnostics.Warnings.Count > 0);
        if (blockedOrFailed && !hasExplanation)
        {
            diagnostics.Add("Blocked, deferred, failed, or invalid lifecycle state lacks diagnostics.");
        }

        bool passed = diagnostics.Count == 0;
        return new DecisionSessionCertificationFinding(
            "diagnostics-explain-lifecycle-state",
            "Diagnostics",
            passed,
            passed
                ? "Lifecycle diagnostics are present for observable state."
                : "Lifecycle diagnostics are incomplete.",
            "Certification requires diagnostics for continue, transfer, blocked or deferred eligibility, recovery, and failure states.",
            [
                $"projection-errors:{projection.Diagnostics.Errors.Count}",
                $"projection-warnings:{projection.Diagnostics.Warnings.Count}",
                $"history-events:{history.Events.Count}",
                $"eligibility-findings:{projection.TransferEligibility?.Eligibility.Findings.Count ?? 0}"
            ],
            diagnostics);
    }

    private static DecisionSessionCertificationFinding CertifyHealth(
        DecisionSessionLifecycleProjection projection,
        DecisionSessionHealthAssessment health)
    {
        DecisionSessionHealthStatus overall = ResolveOverallHealth(health);
        var diagnostics = new List<string>();
        if (health.Dimensions.Count == 0)
        {
            diagnostics.Add("Health assessment has no dimensions.");
        }

        if (overall == DecisionSessionHealthStatus.Healthy &&
            (projection.Diagnostics.Errors.Count > 0 ||
                projection.Diagnostics.ActiveSessionCount > 1 ||
                health.Dimensions.Any(dimension => dimension.Findings.Count > 0)))
        {
            diagnostics.Add("Health reports Healthy while lifecycle evidence contains contradictory findings.");
        }

        bool passed = diagnostics.Count == 0;
        return new DecisionSessionCertificationFinding(
            "health-does-not-contradict-evidence",
            "Health",
            passed,
            passed
                ? "Health evidence does not contradict lifecycle evidence."
                : "Health evidence contradicts lifecycle evidence.",
            "Health must not report a clean healthy state when registry, diagnostics, transfer, recovery, or analysis evidence contradicts it.",
            health.Dimensions.Select(dimension => $"health:{dimension.Name}:{dimension.Status}:findings={dimension.Findings.Count}").ToArray(),
            diagnostics);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(candidate => candidate.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static DecisionSessionHealthStatus ResolveOverallHealth(DecisionSessionHealthAssessment health)
    {
        if (health.Dimensions.Any(dimension => dimension.Status == DecisionSessionHealthStatus.Unhealthy))
        {
            return DecisionSessionHealthStatus.Unhealthy;
        }

        if (health.Dimensions.Any(dimension => dimension.Status == DecisionSessionHealthStatus.Warning))
        {
            return DecisionSessionHealthStatus.Warning;
        }

        return health.Dimensions.Count == 0 ? DecisionSessionHealthStatus.Unknown : DecisionSessionHealthStatus.Healthy;
    }

    private static string Fingerprint(params object?[] values)
    {
        string json = JsonSerializer.Serialize(values, DecisionSessionJson.Options);
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static IReadOnlyList<string> FilterNull(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }
}
