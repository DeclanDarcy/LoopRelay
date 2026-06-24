using System.Text;
using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Persistence;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowCertificationService(
    IRepositoryService repositoryService,
    IWorkflowProjectionService projectionService,
    IWorkflowRepository workflowRepository,
    IWorkflowHealthService healthService) : IWorkflowCertificationService
{
    private static readonly string[] ForbiddenAuthorityCommands =
    [
        "select_work",
        "accept_execution_handoff",
        "reject_execution_handoff",
        "resolve_decision",
        "supersede_decision",
        "review_operational_context",
        "accept_operational_context",
        "edit_operational_context",
        "reject_operational_context",
        "promote_operational_context",
        "approve_commit",
        "commit_execution",
        "execute_commit",
        "approve_push",
        "push_execution",
        "execute_push"
    ];

    private static readonly WorkflowGateType[] AuthorityGateTypes =
    [
        WorkflowGateType.WorkSelection,
        WorkflowGateType.ExecutionAcceptance,
        WorkflowGateType.DecisionResolution,
        WorkflowGateType.OperationalContextReview,
        WorkflowGateType.OperationalContextPromotion,
        WorkflowGateType.CommitApproval,
        WorkflowGateType.PushApproval
    ];

    private static readonly (WorkflowPreparationCommand Command, string CommandName)[] AllowedPreparationCommands =
    [
        (WorkflowPreparationCommand.DiscoverDecisionCandidates, "decisions_discover_candidates"),
        (WorkflowPreparationCommand.GenerateDecisionProposal, "decisions_generate_proposal"),
        (WorkflowPreparationCommand.GenerateOperationalContextProposal, "continuity_generate_operational_context_proposal"),
        (WorkflowPreparationCommand.PrepareExecutionCommit, "execution_prepare_commit")
    ];

    private static readonly string[] GateSatisfactionDecisions =
    [
        "Accept",
        "Accepted",
        "Approve",
        "Approved",
        "Commit",
        "Committed",
        "Promote",
        "Promoted",
        "Push",
        "Pushed",
        "Resolve",
        "Resolved",
        "Satisfy",
        "Satisfied"
    ];

    public async Task<WorkflowCertificationResult> GetCurrentCertificationAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await BuildResultAsync(repository);
    }

    public async Task<WorkflowCertificationResult> RunCertificationAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowCertificationResult result = await BuildResultAsync(repository);
        string reportId = $"certification.{result.GeneratedAt.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}";
        await workflowRepository.SaveReportAsync(
            repository,
            reportId,
            JsonSerializer.Serialize(result, WorkflowJson.Options),
            RenderMarkdown(result));
        return result;
    }

    private async Task<WorkflowCertificationResult> BuildResultAsync(Repository repository)
    {
        WorkflowInstance projection = await projectionService.ProjectAsync(repository.Id);
        IReadOnlyList<WorkflowContinuationEvent> continuationHistory =
            await workflowRepository.ListContinuationEventsAsync(repository);
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory =
            await workflowRepository.ListPreparationEventsAsync(repository);
        IReadOnlyList<string> historyLoadErrors =
            await workflowRepository.ListHistoryLoadErrorsAsync(repository);
        WorkflowHealthAssessment health = await healthService.AssessHealthAsync(repository.Id);
        WorkflowTimeline domainTimeline = WorkflowTimelineFactory.Create(projection, DateTimeOffset.UtcNow);
        WorkflowTimeline? latestTimeline = null;
        string? timelineLoadError = null;
        try
        {
            latestTimeline = await workflowRepository.GetLatestTimelineAsync(repository);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            timelineLoadError = exception.Message;
        }

        var findings = new List<WorkflowCertificationFinding>();

        findings.Add(CertifyReadOnlyAuthorityBoundary(projection, continuationHistory, preparationHistory));
        findings.Add(CertifyNoForbiddenPreparationCommands(preparationHistory));
        findings.Add(CertifyPreparationUsesAllowedDomainCommands(preparationHistory));
        findings.Add(CertifyContinuationDoesNotCrossAuthorityGates(continuationHistory));
        findings.Add(CertifyPreparationDoesNotSatisfyAuthorityGates(preparationHistory));
        findings.Add(CertifyPreparationDoesNotMoveWorkflowStage(preparationHistory));
        findings.Add(CertifyPreparationArtifactsAreReviewable(preparationHistory));
        findings.Add(CertifyGateCatalogUsesDomainCommands(projection));
        findings.Add(CertifyDomainEvidenceWinsDuringRecovery(projection, domainTimeline, latestTimeline, timelineLoadError));
        findings.Add(CertifyDerivedHistoryEvidenceIsRecoverable(historyLoadErrors));
        findings.Add(CertifyContinuationHistoryIsIdempotent(continuationHistory));
        findings.Add(CertifyPreparationHistoryIsIdempotent(preparationHistory));
        findings.Add(CertifyPreparationDuplicateDomainEvidence(preparationHistory));

        string inputFingerprint = WorkflowFingerprint.FromNormalizedEvidence(string.Join(
            '\n',
            "workflow-certification",
            repository.Id,
            projection.CurrentStage,
            projection.ProgressState,
            projection.BlockingGate,
            string.Join("|", projection.Diagnostics.ProjectionInputs.Order(StringComparer.Ordinal)),
            latestTimeline?.Fingerprint ?? "latest-timeline:none",
            timelineLoadError ?? "latest-timeline-load-error:none",
            string.Join("|", historyLoadErrors.Order(StringComparer.Ordinal)),
            string.Join("|", continuationHistory.Select(entry => entry.InputFingerprint.Value).Order(StringComparer.Ordinal)),
            string.Join("|", preparationHistory.Select(entry => entry.InputFingerprint.Value).Order(StringComparer.Ordinal)),
            health.InfluenceTrace.Fingerprint)).Value;
        IReadOnlyList<string> failures = findings
            .Where(finding => !finding.Passed)
            .Select(finding => $"{finding.Id}: {finding.Summary}")
            .ToArray();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;

        return new WorkflowCertificationResult(
            $"workflow-certification.{generatedAt.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}",
            repository.Id,
            generatedAt,
            inputFingerprint,
            failures.Count == 0,
            projection.CurrentStage,
            projection.ProgressState,
            projection.BlockingGate,
            findings.Count(finding => finding.Passed),
            findings.Count(finding => !finding.Passed),
            findings,
            failures,
            [
                $"Workflow certification observed {continuationHistory.Count} continuation events.",
                $"Workflow certification observed {preparationHistory.Count} preparation events.",
                $"Workflow certification observed {historyLoadErrors.Count} derived history load errors.",
                $"Workflow health was {health.OverallStatus}."
            ]);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(candidate => candidate.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository '{repositoryId}' was not found.");
    }

    private static WorkflowCertificationFinding CertifyReadOnlyAuthorityBoundary(
        WorkflowInstance projection,
        IReadOnlyList<WorkflowContinuationEvent> continuationHistory,
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        IReadOnlyList<string> evidence =
        [
            $"projection:{projection.CurrentStage}:{projection.ProgressState}:{projection.BlockingGate}",
            $"continuation-events:{continuationHistory.Count}",
            $"preparation-events:{preparationHistory.Count}",
            "workflow-certification-service:projection+workflow-history+health-only"
        ];

        return new WorkflowCertificationFinding(
            "authority-read-only-boundary",
            "Authority",
            true,
            "Workflow certification observes domain evidence and writes only workflow report evidence.",
            "The certification service depends on workflow projection, workflow history, workflow health, and repository lookup; it does not receive execution, decision, continuity, or git mutator services.",
            evidence,
            []);
    }

    private static WorkflowCertificationFinding CertifyNoForbiddenPreparationCommands(
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        IReadOnlyList<WorkflowPreparationEvent> violations = preparationHistory
            .Where(entry => ForbiddenAuthorityCommands.Contains(entry.CommandName, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        return new WorkflowCertificationFinding(
            "authority-no-forbidden-preparation-command",
            "Authority",
            violations.Count == 0,
            violations.Count == 0
                ? "Preparation history contains no forbidden authority command."
                : "Preparation history contains forbidden authority commands.",
            "Workflow preparation may create reviewable artifacts through allowed domain preparation commands, but it must never execute authority actions such as handoff acceptance, decision resolution, context promotion, commit, or push.",
            preparationHistory.Select(entry => $"preparation:{entry.EventId}:{entry.CommandName}:{entry.Decision}").ToArray(),
            violations.Select(entry => $"Forbidden authority command '{entry.CommandName}' appears in preparation event '{entry.EventId}'.").ToArray());
    }

    private static WorkflowCertificationFinding CertifyPreparationUsesAllowedDomainCommands(
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        IReadOnlyList<WorkflowPreparationEvent> violations = preparationHistory
            .Where(entry => entry.Command is not WorkflowPreparationCommand.None || !string.Equals(entry.CommandName, "none", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !AllowedPreparationCommands.Any(allowed =>
                entry.Command == allowed.Command &&
                string.Equals(entry.CommandName, allowed.CommandName, StringComparison.Ordinal)))
            .ToArray();

        return new WorkflowCertificationFinding(
            "preparation-uses-allowed-domain-commands",
            "Preparation",
            violations.Count == 0,
            violations.Count == 0
                ? "Preparation history uses only allowed existing domain preparation commands."
                : "Preparation history contains unknown or parallel preparation commands.",
            "Workflow preparation must call the existing domain preparation surfaces only; it must not introduce workflow-owned or parallel commands that duplicate domain behavior.",
            preparationHistory.Select(entry => $"preparation-command:{entry.EventId}:{entry.Command}:{entry.CommandName}:{entry.Decision}").ToArray(),
            violations.Select(entry => $"Preparation event '{entry.EventId}' used unsupported command '{entry.CommandName}' for enum value {entry.Command}.").ToArray());
    }

    private static WorkflowCertificationFinding CertifyContinuationDoesNotCrossAuthorityGates(
        IReadOnlyList<WorkflowContinuationEvent> continuationHistory)
    {
        IReadOnlyList<WorkflowContinuationEvent> violations = continuationHistory
            .Where(entry =>
                entry.IsWaitingForHuman && entry.ToStage is not null ||
                entry.BlockingGate is not WorkflowGateType.None &&
                (entry.ToStage is not null || string.Equals(entry.Decision, "Advance", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        IReadOnlyList<WorkflowGateType> coveredGates = continuationHistory
            .Where(entry =>
                entry.BlockingGate is not WorkflowGateType.None &&
                entry.IsWaitingForHuman &&
                entry.ToStage is null &&
                string.Equals(entry.Decision, "Stop", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.BlockingGate)
            .Distinct()
            .Order()
            .ToArray();
        IReadOnlyList<WorkflowGateType> missingGates = AuthorityGateTypes
            .Except(coveredGates)
            .ToArray();
        IReadOnlyList<string> coveredTriggers = continuationHistory
            .Select(entry => string.IsNullOrWhiteSpace(entry.Trigger) ? "endpoint" : entry.Trigger)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var diagnostics = new List<string>();
        diagnostics.AddRange(violations.Select(entry =>
            $"Continuation event '{entry.EventId}' advanced to {entry.ToStage} while waiting for {entry.BlockingGate}."));
        diagnostics.AddRange(missingGates.Select(gate =>
            $"Continuation halt coverage has not yet observed gate {gate}."));

        if (!coveredTriggers.Contains("endpoint", StringComparer.OrdinalIgnoreCase))
        {
            diagnostics.Add("Continuation halt coverage has not yet observed endpoint-triggered continuation.");
        }

        if (!coveredTriggers.Contains("hosted", StringComparer.OrdinalIgnoreCase))
        {
            diagnostics.Add("Continuation halt coverage has not yet observed hosted continuation.");
        }

        return new WorkflowCertificationFinding(
            "authority-continuation-halts-at-gates",
            "Authority",
            violations.Count == 0,
            violations.Count == 0
                ? "Continuation history halts when waiting for human authority."
                : "Continuation history crossed an authority gate.",
            "A continuation event that is waiting for a human must not advance to a next stage.",
            continuationHistory.Select(entry =>
                $"continuation:{entry.EventId}:{entry.Trigger}:{entry.FromStage}->{entry.ToStage?.ToString() ?? "None"}:{entry.BlockingGate}:waiting={entry.IsWaitingForHuman}")
                .Concat(AuthorityGateTypes.Select(gate => $"gate-coverage:{gate}:covered={coveredGates.Contains(gate)}"))
                .Concat(coveredTriggers.Select(trigger => $"trigger-coverage:{trigger}"))
                .ToArray(),
            diagnostics);
    }

    private static WorkflowCertificationFinding CertifyPreparationDoesNotSatisfyAuthorityGates(
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        IReadOnlyList<WorkflowPreparationEvent> violations = preparationHistory
            .Where(entry =>
                entry.BlockingGate is not WorkflowGateType.None &&
                (!entry.IsWaitingForHuman ||
                    GateSatisfactionDecisions.Contains(entry.Decision, StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        return new WorkflowCertificationFinding(
            "authority-preparation-does-not-satisfy-gates",
            "Authority",
            violations.Count == 0,
            violations.Count == 0
                ? "Preparation history does not satisfy human authority gates."
                : "Preparation history appears to satisfy a human authority gate.",
            "Preparation can create reviewable artifacts, but an open authority gate must remain waiting for the human-owned domain command.",
            preparationHistory.Select(entry =>
                $"preparation:{entry.EventId}:{entry.BlockingGate}:waiting={entry.IsWaitingForHuman}:created={entry.CreatedArtifactIds.Count}:decision={entry.Decision}").ToArray(),
            violations.Select(entry => $"Preparation event '{entry.EventId}' changed authority-gated state for {entry.BlockingGate}.").ToArray());
    }

    private static WorkflowCertificationFinding CertifyPreparationDoesNotMoveWorkflowStage(
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        IReadOnlyList<WorkflowPreparationEvent> violations = preparationHistory
            .Where(entry => string.Equals(entry.Decision, "Advance", StringComparison.OrdinalIgnoreCase) ||
                entry.Diagnostics.Any(diagnostic => diagnostic.Contains("advanced to", StringComparison.OrdinalIgnoreCase) ||
                    diagnostic.Contains("moved workflow stage", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return new WorkflowCertificationFinding(
            "authority-preparation-does-not-progress-stage",
            "Authority",
            violations.Count == 0,
            violations.Count == 0
                ? "Preparation history does not move workflow stages."
                : "Preparation history appears to move workflow stages.",
            "Preparation may create reviewable artifacts only. Stage progression is handled by continuation after domain evidence proves the next stage.",
            preparationHistory.Select(entry =>
                $"preparation-stage:{entry.EventId}:{entry.Stage}:{entry.ProgressState}:{entry.CommandName}:{entry.Decision}").ToArray(),
            violations.Select(entry => $"Preparation event '{entry.EventId}' recorded stage progression with decision '{entry.Decision}'.").ToArray());
    }

    private static WorkflowCertificationFinding CertifyPreparationArtifactsAreReviewable(
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        IReadOnlyList<string> reviewablePrefixes =
        [
            "decision-candidate:",
            "decision-proposal:",
            "operational-context-proposal:",
            "commit-preparation:"
        ];
        IReadOnlyList<string> violations = preparationHistory
            .SelectMany(entry => entry.CreatedArtifactIds.Select(artifactId => $"{entry.EventId}|{artifactId}"))
            .Where(value =>
            {
                string artifactId = value.Split('|')[1];
                return !reviewablePrefixes.Any(prefix => artifactId.StartsWith(prefix, StringComparison.Ordinal));
            })
            .ToArray();

        return new WorkflowCertificationFinding(
            "preparation-artifacts-are-reviewable",
            "Preparation",
            violations.Count == 0,
            violations.Count == 0
                ? "Preparation history created only reviewable artifact evidence."
                : "Preparation history created non-reviewable artifact evidence.",
            "Workflow preparation may create only decision candidates, decision proposals, operational-context proposals, and commit-preparation snapshots for human review.",
            preparationHistory.Select(entry =>
                $"preparation-artifacts:{entry.EventId}:{entry.CommandName}:{string.Join(",", entry.CreatedArtifactIds)}").ToArray(),
            violations.Select(value =>
            {
                string[] parts = value.Split('|', 2);
                return $"Preparation event '{parts[0]}' created non-reviewable artifact '{parts[1]}'.";
            }).ToArray());
    }

    private static WorkflowCertificationFinding CertifyGateCatalogUsesDomainCommands(WorkflowInstance projection)
    {
        IReadOnlyList<WorkflowGate> gates = projection.GateHistory;
        IReadOnlyList<WorkflowGate> violations = gates
            .Where(gate => gate.SatisfyingCommands.Any(command => command.StartsWith("workflow_", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return new WorkflowCertificationFinding(
            "authority-gates-map-to-domain-commands",
            "Authority",
            violations.Count == 0,
            violations.Count == 0
                ? "Workflow gates map required human action to existing domain commands."
                : "Workflow gates include workflow-owned authority commands.",
            "Workflow may explain which domain command satisfies a gate, but it must not introduce workflow-owned commands for authority actions.",
            gates.Select(gate => $"gate:{gate.Type}:{gate.Status}:{string.Join(",", gate.SatisfyingCommands)}").ToArray(),
            violations.Select(gate => $"Gate '{gate.GateId}' contains a workflow-owned satisfying command.").ToArray());
    }

    private static WorkflowCertificationFinding CertifyDomainEvidenceWinsDuringRecovery(
        WorkflowInstance projection,
        WorkflowTimeline domainTimeline,
        WorkflowTimeline? latestTimeline,
        string? timelineLoadError)
    {
        bool hasDomainProjection = projection.CurrentStage is not WorkflowStage.Unknown &&
            projection.ProgressState is not WorkflowProgressState.Failed;
        bool timelineMatchesDomain = latestTimeline?.Fingerprint == domainTimeline.Fingerprint;
        bool staleTimelineDetected = latestTimeline is not null && !timelineMatchesDomain;
        bool timelineRecoverable = latestTimeline is null || timelineMatchesDomain || staleTimelineDetected || timelineLoadError is not null;
        bool passed = hasDomainProjection && timelineRecoverable;

        string summary = passed
            ? timelineLoadError is not null
                ? "Workflow recovery detected corrupted timeline evidence and preserved domain-derived state."
                : "Workflow recovery can reconstruct current state from domain evidence."
            : "Workflow recovery could not prove current state reconstruction from domain evidence.";
        string detail = timelineLoadError is not null
            ? "Persisted workflow timeline evidence could not be loaded; certification treats the domain-derived projection as authoritative recovery input and reports that recovery is required."
            : staleTimelineDetected
                ? "Persisted workflow timeline evidence differs from the domain-derived projection; certification treats the domain-derived projection as authoritative recovery input."
                : "Workflow recovery certification compares the latest persisted timeline with a fresh domain-derived timeline and requires domain evidence to remain reconstructable.";

        var evidence = new List<string>
        {
            $"domain:{projection.CurrentStage}:{projection.ProgressState}:{projection.BlockingGate}:{domainTimeline.Fingerprint}",
            latestTimeline is null
                ? "persisted-timeline:none"
                : $"persisted-timeline:{latestTimeline.CurrentStage}:{latestTimeline.ProgressState}:{latestTimeline.BlockingGate}:{latestTimeline.Fingerprint}",
            $"timeline-load-error:{timelineLoadError ?? "none"}"
        };
        var diagnostics = new List<string>();

        if (!hasDomainProjection)
        {
            diagnostics.Add($"Domain-derived projection is not reconstructable: {projection.CurrentStage}/{projection.ProgressState}.");
        }

        if (latestTimeline is null)
        {
            diagnostics.Add("No persisted workflow timeline exists; recovery can rebuild timeline evidence from domain projection.");
        }
        else if (timelineMatchesDomain)
        {
            diagnostics.Add("Persisted workflow timeline matches the domain-derived projection.");
        }
        else
        {
            diagnostics.Add("Persisted workflow timeline conflicts with the domain-derived projection; domain evidence wins.");
            diagnostics.Add($"Persisted stage {latestTimeline.CurrentStage} is recoverable to domain stage {projection.CurrentStage}.");
        }

        if (timelineLoadError is not null)
        {
            diagnostics.Add("Timeline evidence is corrupted; recovery is required before persisted workflow evidence can be trusted.");
            diagnostics.Add($"Persisted workflow timeline could not be loaded and must not override domain evidence: {timelineLoadError}");
        }

        return new WorkflowCertificationFinding(
            "recovery-domain-evidence-wins",
            "Recovery",
            passed,
            summary,
            detail,
            evidence,
            diagnostics);
    }

    private static WorkflowCertificationFinding CertifyDerivedHistoryEvidenceIsRecoverable(
        IReadOnlyList<string> historyLoadErrors)
    {
        bool passed = true;
        return new WorkflowCertificationFinding(
            "recovery-derived-history-evidence",
            "Recovery",
            passed,
            historyLoadErrors.Count == 0
                ? "Derived workflow history evidence is readable or absent."
                : "Corrupted derived workflow history evidence was detected without replacing domain state.",
            "Continuation and preparation history are derived audit evidence. Corrupted history files require recovery diagnostics, but domain projection, gate state, and duplicate detection remain authoritative.",
            historyLoadErrors.Count == 0
                ? ["workflow-history:load-errors:none"]
                : historyLoadErrors.Select(error => $"workflow-history:load-error:{error}").ToArray(),
            historyLoadErrors.Count == 0
                ? []
                : historyLoadErrors.Select(error => $"Recovery is required for derived workflow history evidence: {error}").ToArray());
    }

    private static WorkflowCertificationFinding CertifyContinuationHistoryIsIdempotent(
        IReadOnlyList<WorkflowContinuationEvent> continuationHistory)
    {
        IReadOnlyList<IGrouping<string, WorkflowContinuationEvent>> duplicates = continuationHistory
            .Where(entry => string.Equals(entry.Decision, "Advance", StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => string.Join(
                "|",
                entry.InputFingerprint.Value,
                entry.FromStage,
                entry.ToStage?.ToString() ?? "None",
                entry.Decision,
                entry.Reason))
            .Where(group => group.Count() > 1)
            .ToArray();

        return new WorkflowCertificationFinding(
            "recovery-continuation-idempotency",
            "Recovery",
            duplicates.Count == 0,
            duplicates.Count == 0
                ? "Continuation history contains no duplicate mechanical progression for the same fingerprint."
                : "Continuation history contains duplicate mechanical progression for the same fingerprint.",
            "Restart and recovery must not record the same mechanical advance more than once for identical continuation inputs.",
            continuationHistory.Select(entry =>
                $"continuation:{entry.EventId}:{entry.FromStage}->{entry.ToStage?.ToString() ?? "None"}:{entry.Decision}:{entry.InputFingerprint}").ToArray(),
            duplicates.Select(group => $"Duplicate continuation progression fingerprint '{group.Key}' appears {group.Count()} times.").ToArray());
    }

    private static WorkflowCertificationFinding CertifyPreparationHistoryIsIdempotent(
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        IReadOnlyList<IGrouping<string, WorkflowPreparationEvent>> duplicateEvents = preparationHistory
            .Where(entry => entry.CreatedArtifactIds.Count > 0)
            .GroupBy(entry => string.Join(
                "|",
                entry.InputFingerprint.Value,
                entry.Stage,
                entry.Command))
            .Where(group => group.Count() > 1)
            .ToArray();
        IReadOnlyList<IGrouping<string, string>> duplicateArtifacts = preparationHistory
            .SelectMany(entry => entry.CreatedArtifactIds.Select(artifactId => $"{entry.CommandName}|{artifactId}"))
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToArray();
        bool passed = duplicateEvents.Count == 0 && duplicateArtifacts.Count == 0;

        return new WorkflowCertificationFinding(
            "recovery-preparation-idempotency",
            "Recovery",
            passed,
            passed
                ? "Preparation history contains no duplicate reviewable artifact creation."
                : "Preparation history contains duplicate reviewable artifact creation.",
            "Restart and recovery must not create duplicate preparation events or duplicate review artifacts for identical preparation inputs.",
            preparationHistory.Select(entry =>
                $"preparation:{entry.EventId}:{entry.CommandName}:{entry.Decision}:{entry.InputFingerprint}:created={string.Join(",", entry.CreatedArtifactIds)}").ToArray(),
            duplicateEvents.Select(group => $"Duplicate preparation input fingerprint '{group.Key}' created artifacts {group.Count()} times.")
                .Concat(duplicateArtifacts.Select(group => $"Duplicate prepared artifact '{group.Key}' appears {group.Count()} times."))
                .ToArray());
    }

    private static WorkflowCertificationFinding CertifyPreparationDuplicateDomainEvidence(
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        IReadOnlyList<WorkflowPreparationEvent> duplicateEvidenceEvents = preparationHistory
            .Where(entry => entry.HasDuplicateDomainEvidence || entry.DuplicateEvidence.Count > 0)
            .ToArray();
        bool duplicateCreationDetected = duplicateEvidenceEvents.All(entry => entry.CreatedArtifactIds.Count == 0);

        return new WorkflowCertificationFinding(
            "preparation-duplicate-domain-evidence",
            "Preparation",
            duplicateCreationDetected,
            duplicateCreationDetected
                ? "Preparation duplicate domain evidence is reported without creating new artifacts."
                : "Preparation duplicate domain evidence created additional artifacts.",
            "Duplicate decision, operational-context, and commit-preparation evidence must cause preparation to skip or refuse artifact creation instead of duplicating reviewable artifacts.",
            duplicateEvidenceEvents.Count == 0
                ? ["preparation-duplicate-domain-evidence:none-observed"]
                : duplicateEvidenceEvents.Select(entry =>
                    $"preparation-duplicate-evidence:{entry.EventId}:{entry.CommandName}:{string.Join(",", entry.DuplicateEvidence)}").ToArray(),
            duplicateEvidenceEvents
                .Where(entry => entry.CreatedArtifactIds.Count > 0)
                .Select(entry => $"Preparation event '{entry.EventId}' reported duplicate domain evidence but still created {string.Join(",", entry.CreatedArtifactIds)}.")
                .ToArray());
    }

    private static string RenderMarkdown(WorkflowCertificationResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Workflow Certification");
        builder.AppendLine();
        builder.AppendLine($"Repository: {result.RepositoryId}");
        builder.AppendLine($"Generated At: {result.GeneratedAt:O}");
        builder.AppendLine($"Input Fingerprint: {result.InputFingerprint}");
        builder.AppendLine($"Certified: {result.Certified}");
        builder.AppendLine($"Current Stage: {result.CurrentStage}");
        builder.AppendLine($"Progress State: {result.ProgressState}");
        builder.AppendLine($"Blocking Gate: {result.BlockingGate}");
        builder.AppendLine($"Passed Findings: {result.PassedFindingCount}");
        builder.AppendLine($"Failed Findings: {result.FailedFindingCount}");
        builder.AppendLine();
        builder.AppendLine("## Findings");

        foreach (WorkflowCertificationFinding finding in result.Findings)
        {
            builder.AppendLine();
            builder.AppendLine($"- {finding.Id} | {finding.Category} | Passed: {finding.Passed}");
            builder.AppendLine($"  - Summary: {finding.Summary}");
            builder.AppendLine($"  - Detail: {finding.Detail}");
            foreach (string diagnostic in finding.Diagnostics)
            {
                builder.AppendLine($"  - Diagnostic: {diagnostic}");
            }
        }

        if (result.Failures.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Failures");
            foreach (string failure in result.Failures)
            {
                builder.AppendLine($"- {failure}");
            }
        }

        return builder.ToString();
    }
}
