using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowDecisionSessionService(
    IDecisionSessionObservabilityService observabilityService) : IWorkflowDecisionSessionService
{
    public async Task<WorkflowDecisionSessionProjection> ProjectAsync(Guid repositoryId)
    {
        DecisionSessionLifecycleProjection projection = await observabilityService.GetProjectionAsync(repositoryId);
        DecisionSessionHealthAssessment health = await observabilityService.GetHealthAsync(repositoryId);
        WorkflowGovernanceSummary summary = CreateSummary(projection, health);
        WorkflowGovernanceReadiness readiness = CreateReadiness(projection);
        DecisionSessionWorkflowDiagnostics diagnostics = CreateDiagnostics(projection, readiness);

        return new WorkflowDecisionSessionProjection(
            repositoryId,
            projection.ActiveSession?.Id.ToString(),
            projection.ActiveSession?.State.ToString(),
            projection.Size?.EstimatedTokenCount ?? projection.Metrics?.Metrics.EstimatedTokenCount,
            projection.Metrics?.Cache.EstimatedCacheTtl,
            projection.Size?.CacheMissRisk ?? projection.Metrics?.Cache.EstimatedCacheMissRisk,
            projection.Policy?.Evaluation.ReuseScore,
            projection.Policy?.Evaluation.TransferScore,
            projection.Coherence?.Coherence.CoherenceScore,
            projection.Coherence?.Coherence.TransferPressure,
            projection.Policy?.Evaluation.Decision.ToString(),
            projection.TransferEligibility?.Eligibility.Status.ToString(),
            projection.CurrentContinuityArtifact?.ArtifactId,
            projection.CurrentContinuityArtifact?.ContinuityFingerprint,
            projection.TransferEvents.Select(CreateTransferProjection).ToArray(),
            projection.ContinuityArtifacts.Select(CreateArtifactProjection).ToArray(),
            health.Dimensions.Select(CreateHealthProjection).ToArray(),
            summary,
            readiness,
            diagnostics,
            projection.GeneratedAt);
    }

    public async Task<WorkflowGovernanceSummary> GetSummaryAsync(Guid repositoryId)
    {
        DecisionSessionLifecycleProjection projection = await observabilityService.GetProjectionAsync(repositoryId);
        DecisionSessionHealthAssessment health = await observabilityService.GetHealthAsync(repositoryId);
        return CreateSummary(projection, health);
    }

    public async Task<WorkflowGovernanceHealthProjection> GetHealthAsync(Guid repositoryId)
    {
        DecisionSessionHealthAssessment health = await observabilityService.GetHealthAsync(repositoryId);
        return new WorkflowGovernanceHealthProjection(
            "Decision sessions",
            OverallStatus(health.Dimensions),
            health.Dimensions.SelectMany(dimension => dimension.Findings).Distinct(StringComparer.Ordinal).ToArray(),
            health.Dimensions.SelectMany(dimension => dimension.Evidence).Distinct(StringComparer.Ordinal).ToArray());
    }

    public async Task<WorkflowGovernanceInfluenceProjection> GetInfluenceAsync(Guid repositoryId)
    {
        DecisionSessionInfluenceTrace influence = await observabilityService.GetInfluenceTraceAsync(repositoryId);
        return new WorkflowGovernanceInfluenceProjection(
            repositoryId,
            influence.ActiveSessionId?.ToString(),
            influence.PolicyDecision?.ToString(),
            influence.TransferEligibilityStatus?.ToString(),
            influence.Signals
                .Select(signal => new WorkflowGovernanceInfluenceSignal(
                    signal.Category,
                    signal.Name,
                    signal.Score,
                    signal.Value,
                    signal.Description,
                    signal.ContributingFactors))
                .ToArray(),
            influence.Diagnostics,
            influence.GeneratedAt);
    }

    public async Task<DecisionSessionWorkflowDiagnostics> GetDiagnosticsAsync(Guid repositoryId)
    {
        DecisionSessionLifecycleProjection projection = await observabilityService.GetProjectionAsync(repositoryId);
        return CreateDiagnostics(projection, CreateReadiness(projection));
    }

    private static WorkflowGovernanceSummary CreateSummary(
        DecisionSessionLifecycleProjection projection,
        DecisionSessionHealthAssessment health)
    {
        List<string> highlights = [];
        if (projection.Policy is not null)
        {
            highlights.Add($"Lifecycle decision is {projection.Policy.Evaluation.Decision}.");
        }

        if (projection.TransferEligibility is not null)
        {
            highlights.Add($"Transfer eligibility is {projection.TransferEligibility.Eligibility.Status}.");
        }

        if (projection.Coherence is not null)
        {
            highlights.Add($"Transfer pressure is {projection.Coherence.Coherence.TransferPressure}.");
        }

        if (projection.CurrentContinuityArtifact is not null)
        {
            highlights.Add($"Continuity artifact {projection.CurrentContinuityArtifact.ArtifactId} is available.");
        }

        return new WorkflowGovernanceSummary(
            projection.RepositoryId,
            projection.ActiveSession?.Id.ToString(),
            projection.ActiveSession?.State.ToString(),
            projection.Policy?.Evaluation.Decision.ToString(),
            projection.TransferEligibility?.Eligibility.Status.ToString(),
            projection.Size?.EstimatedTokenCount ?? projection.Metrics?.Metrics.EstimatedTokenCount,
            projection.Metrics?.Cache.EstimatedCacheTtl,
            projection.Size?.CacheMissRisk ?? projection.Metrics?.Cache.EstimatedCacheMissRisk,
            projection.Coherence?.Coherence.CoherenceScore,
            projection.Coherence?.Coherence.TransferPressure,
            OverallStatus(health.Dimensions),
            highlights,
            projection.GeneratedAt);
    }

    private static WorkflowGovernanceReadiness CreateReadiness(DecisionSessionLifecycleProjection projection)
    {
        var missing = new List<string>();
        var blocking = new List<string>();

        if (projection.ActiveSession is null)
        {
            missing.Add("active decision session");
        }

        if (projection.Metrics is null || projection.Economics is null || projection.Coherence is null)
        {
            missing.Add("complete decision-session analysis");
        }

        if (projection.Policy is null)
        {
            missing.Add("lifecycle policy snapshot");
        }

        if (projection.TransferEligibility is null)
        {
            missing.Add("transfer eligibility snapshot");
        }

        bool transferRecommended = projection.Policy?.Evaluation.Decision is DecisionSessionLifecycleDecision.Transfer;
        bool transferEligible = projection.TransferEligibility?.Eligibility.Status is DecisionSessionTransferEligibilityStatus.Eligible;
        if (projection.TransferEligibility?.Eligibility.Status is DecisionSessionTransferEligibilityStatus.Blocked)
        {
            blocking.AddRange(projection.TransferEligibility.Eligibility.Findings.Select(finding => finding.Message));
        }

        if (transferRecommended && transferEligible && projection.CurrentContinuityArtifact is null)
        {
            missing.Add("continuity artifact");
        }

        return new WorkflowGovernanceReadiness(
            projection.ActiveSession is not null,
            projection.Metrics is not null && projection.Economics is not null && projection.Coherence is not null,
            projection.Policy is not null,
            projection.TransferEligibility is not null,
            transferRecommended,
            transferEligible,
            projection.CurrentContinuityArtifact is not null,
            missing.Distinct(StringComparer.Ordinal).ToArray(),
            blocking.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static DecisionSessionWorkflowDiagnostics CreateDiagnostics(
        DecisionSessionLifecycleProjection projection,
        WorkflowGovernanceReadiness readiness)
    {
        IReadOnlyList<string> evidence =
        [
            $"decision-session:{projection.ActiveSession?.Id.ToString() ?? "none"}:{projection.ActiveSession?.State.ToString() ?? "none"}",
            $"sessions:{projection.Sessions.Count}",
            $"metrics:{(projection.Metrics is null ? "missing" : projection.Metrics.GeneratedAt.ToString("O"))}",
            $"economics:{(projection.Economics is null ? "missing" : projection.Economics.GeneratedAt.ToString("O"))}",
            $"coherence:{(projection.Coherence is null ? "missing" : projection.Coherence.GeneratedAt.ToString("O"))}",
            $"policy:{projection.Policy?.Evaluation.Decision.ToString() ?? "missing"}",
            $"eligibility:{projection.TransferEligibility?.Eligibility.Status.ToString() ?? "missing"}",
            $"continuity-artifacts:{projection.ContinuityArtifacts.Count}",
            $"transfers:{projection.TransferEvents.Count}",
            $"recovery-results:{projection.RecentRecoveryResults.Count}"
        ];

        return new DecisionSessionWorkflowDiagnostics(
            projection.RepositoryId,
            projection.Diagnostics.IsValid && readiness.BlockingSignals.Count == 0,
            evidence,
            projection.Diagnostics.Warnings.Concat(readiness.MissingEvidence.Select(item => $"Missing {item}.")).ToArray(),
            projection.Diagnostics.Errors.Concat(readiness.BlockingSignals).ToArray(),
            projection.GeneratedAt);
    }

    private static WorkflowTransferProjection CreateTransferProjection(DecisionSessionTransferEventProjection transfer) =>
        new(
            transfer.TransferId,
            transfer.SourceSessionId.ToString(),
            transfer.TargetSessionId?.ToString(),
            transfer.StartedAt,
            transfer.CompletedAt,
            transfer.Succeeded,
            transfer.ContinuityArtifactId,
            transfer.Succeeded ? "Succeeded" : transfer.CompletedAt is null ? "Incomplete" : "Failed",
            transfer.Diagnostics);

    private static WorkflowContinuityArtifactProjection CreateArtifactProjection(DecisionSessionContinuityArtifactProjection artifact) =>
        new(
            artifact.ArtifactId,
            artifact.ContinuityFingerprint,
            artifact.SourceSessionId.ToString(),
            artifact.TargetSessionId?.ToString(),
            artifact.CreatedAt,
            artifact.DecisionReferences.Count,
            artifact.ReasoningReferences.Count,
            artifact.OperationalContextReferences.Count,
            artifact.Diagnostics);

    private static WorkflowGovernanceHealthProjection CreateHealthProjection(DecisionSessionHealthDimension dimension) =>
        new(dimension.Name, dimension.Status.ToString(), dimension.Findings, dimension.Evidence);

    private static string OverallStatus(IReadOnlyList<DecisionSessionHealthDimension> dimensions)
    {
        if (dimensions.Any(dimension => dimension.Status is DecisionSessionHealthStatus.Unhealthy))
        {
            return "Unhealthy";
        }

        if (dimensions.Any(dimension => dimension.Status is DecisionSessionHealthStatus.Warning))
        {
            return "Warning";
        }

        return dimensions.Count == 0 ? "Unknown" : "Healthy";
    }
}
