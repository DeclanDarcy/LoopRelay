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
        WorkflowHealthAssessment health = await healthService.AssessHealthAsync(repository.Id);
        var findings = new List<WorkflowCertificationFinding>();

        findings.Add(CertifyReadOnlyAuthorityBoundary(projection, continuationHistory, preparationHistory));
        findings.Add(CertifyNoForbiddenPreparationCommands(preparationHistory));
        findings.Add(CertifyContinuationDoesNotCrossAuthorityGates(continuationHistory));
        findings.Add(CertifyPreparationDoesNotSatisfyAuthorityGates(preparationHistory));
        findings.Add(CertifyGateCatalogUsesDomainCommands(projection));

        string inputFingerprint = WorkflowFingerprint.FromNormalizedEvidence(string.Join(
            '\n',
            "workflow-certification",
            repository.Id,
            projection.CurrentStage,
            projection.ProgressState,
            projection.BlockingGate,
            string.Join("|", projection.Diagnostics.ProjectionInputs.Order(StringComparer.Ordinal)),
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

    private static WorkflowCertificationFinding CertifyContinuationDoesNotCrossAuthorityGates(
        IReadOnlyList<WorkflowContinuationEvent> continuationHistory)
    {
        IReadOnlyList<WorkflowContinuationEvent> violations = continuationHistory
            .Where(entry => entry.IsWaitingForHuman && entry.ToStage is not null)
            .ToArray();

        return new WorkflowCertificationFinding(
            "authority-continuation-halts-at-gates",
            "Authority",
            violations.Count == 0,
            violations.Count == 0
                ? "Continuation history halts when waiting for human authority."
                : "Continuation history crossed an authority gate.",
            "A continuation event that is waiting for a human must not advance to a next stage.",
            continuationHistory.Select(entry =>
                $"continuation:{entry.EventId}:{entry.FromStage}->{entry.ToStage?.ToString() ?? "None"}:{entry.BlockingGate}:waiting={entry.IsWaitingForHuman}").ToArray(),
            violations.Select(entry => $"Continuation event '{entry.EventId}' advanced to {entry.ToStage} while waiting for {entry.BlockingGate}.").ToArray());
    }

    private static WorkflowCertificationFinding CertifyPreparationDoesNotSatisfyAuthorityGates(
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        IReadOnlyList<WorkflowPreparationEvent> violations = preparationHistory
            .Where(entry =>
                entry.BlockingGate is not WorkflowGateType.None &&
                !entry.IsWaitingForHuman &&
                (entry.CreatedArtifactIds.Count > 0 || string.Equals(entry.Decision, "Advance", StringComparison.OrdinalIgnoreCase)))
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
