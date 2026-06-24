using System.Text;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowGateCatalogService(IWorkflowProjectionService projectionService) : IWorkflowGateCatalogService
{
    private static readonly IReadOnlyDictionary<WorkflowGateType, string[]> CommandMap = new Dictionary<WorkflowGateType, string[]>
    {
        [WorkflowGateType.WorkSelection] = ["explicit_human_work_selection"],
        [WorkflowGateType.ExecutionAcceptance] = ["accept_execution_handoff", "reject_execution_handoff"],
        [WorkflowGateType.DecisionResolution] = ["resolve_decision_proposal"],
        [WorkflowGateType.OperationalContextReview] = ["accept_operational_context_proposal", "edit_operational_context_proposal", "reject_operational_context_proposal"],
        [WorkflowGateType.OperationalContextPromotion] = ["promote_operational_context_proposal"],
        [WorkflowGateType.CommitApproval] = ["commit_execution"],
        [WorkflowGateType.PushApproval] = ["push_execution"]
    };

    public WorkflowGateCatalogProjection ProjectGates(WorkflowInstance projection) =>
        BuildProjection(projection);

    public async Task<WorkflowGateCatalogProjection> GetGatesAsync(Guid repositoryId) =>
        BuildProjection(await projectionService.ProjectAsync(repositoryId));

    public async Task<WorkflowGateHistoryProjection> GetGateHistoryAsync(Guid repositoryId)
    {
        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
        WorkflowGateCatalogProjection gates = BuildProjection(projection);
        return new WorkflowGateHistoryProjection(
            repositoryId,
            DateTimeOffset.UtcNow,
            gates.GateHistory,
            RenderGateHistoryMarkdown(repositoryId, gates));
    }

    public static WorkflowGateCatalogProjection BuildProjection(WorkflowInstance projection)
    {
        List<string> reasoning = [];
        List<string> missingEvidence = [];
        List<string> conflicts = [];

        IReadOnlyList<WorkflowGate> satisfied = BuildSatisfiedGates(projection).ToArray();
        WorkflowGate? open = BuildOpenGate(projection, missingEvidence);

        if (open is not null)
        {
            reasoning.Add($"Open gate {open.Type} blocks stage {projection.CurrentStage}.");
        }
        else
        {
            reasoning.Add("No open workflow authority gate is currently blocking progression.");
        }

        foreach (WorkflowGate gate in satisfied)
        {
            reasoning.Add($"Satisfied gate {gate.Type} is derived from {gate.SourceDomain}:{gate.SourceArtifact}.");
        }

        IReadOnlyList<WorkflowGate> openGates = open is null ? [] : [open];
        IReadOnlyList<WorkflowGate> history = satisfied
            .Concat(openGates)
            .OrderBy(gate => gate.CreatedAt)
            .ThenBy(gate => gate.Type)
            .ToArray();

        var diagnostics = new WorkflowGateDiagnostics(
            projection.RepositoryId,
            projection.BlockingGate,
            openGates,
            satisfied,
            CommandMap
                .OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}:{string.Join("|", pair.Value)}")
                .ToArray(),
            reasoning,
            missingEvidence,
            conflicts);

        return new WorkflowGateCatalogProjection(openGates, satisfied, history, diagnostics);
    }

    private static WorkflowGate? BuildOpenGate(WorkflowInstance projection, List<string> missingEvidence)
    {
        if (projection.BlockingGate is WorkflowGateType.None)
        {
            return null;
        }

        string[] commands = CommandsFor(projection.BlockingGate);
        WorkflowTimelineEntry? sourceEntry = projection.Timeline
            .Where(entry => entry.Stage == projection.CurrentStage || MatchesGateSource(projection.BlockingGate, entry.EventType))
            .OrderByDescending(entry => entry.OccurredAt)
            .FirstOrDefault();

        if (sourceEntry is null)
        {
            missingEvidence.Add($"No timeline source evidence was available for open gate {projection.BlockingGate}.");
        }

        DateTimeOffset createdAt = sourceEntry?.OccurredAt ?? DateTimeOffset.UnixEpoch;
        string sourceDomain = sourceEntry?.SourceDomain ?? "workflow";
        string sourceArtifact = sourceEntry?.SourceArtifact ?? projection.RepositoryId.ToString();
        string reason = BuildOpenGateReason(projection);
        var evidence = new WorkflowGateEvidence(
            sourceDomain,
            sourceArtifact,
            reason,
            createdAt,
            WorkflowFingerprint.FromNormalizedEvidence(string.Join('\n', "open-gate-evidence", projection.RepositoryId, projection.BlockingGate, sourceDomain, sourceArtifact, reason)).Value);

        return CreateGate(
            projection.RepositoryId,
            projection.BlockingGate,
            projection.CurrentStage,
            WorkflowGateStatus.Open,
            projection.RequiredHumanAction,
            commands,
            sourceDomain,
            sourceArtifact,
            createdAt,
            null,
            null,
            reason,
            [evidence]);
    }

    private static IEnumerable<WorkflowGate> BuildSatisfiedGates(WorkflowInstance projection)
    {
        foreach (WorkflowTimelineEntry entry in projection.Timeline)
        {
            WorkflowGateType? gateType = entry.EventType switch
            {
                WorkflowTimelineEventType.ExecutionStarted => WorkflowGateType.WorkSelection,
                WorkflowTimelineEventType.ExecutionHandoffAccepted => WorkflowGateType.ExecutionAcceptance,
                WorkflowTimelineEventType.DecisionResolved => WorkflowGateType.DecisionResolution,
                WorkflowTimelineEventType.OperationalContextAccepted => WorkflowGateType.OperationalContextReview,
                WorkflowTimelineEventType.OperationalContextEdited => WorkflowGateType.OperationalContextReview,
                WorkflowTimelineEventType.OperationalContextRejected => WorkflowGateType.OperationalContextReview,
                WorkflowTimelineEventType.OperationalContextPromoted => WorkflowGateType.OperationalContextPromotion,
                WorkflowTimelineEventType.CommitExecuted => WorkflowGateType.CommitApproval,
                WorkflowTimelineEventType.PushExecuted => WorkflowGateType.PushApproval,
                _ => null
            };

            if (gateType is null)
            {
                continue;
            }

            WorkflowGateType type = gateType.Value;
            string[] commands = CommandsFor(type);
            var evidence = new WorkflowGateEvidence(
                entry.SourceDomain,
                entry.SourceArtifact,
                entry.Summary,
                entry.OccurredAt,
                entry.Fingerprint);

            yield return CreateGate(
                projection.RepositoryId,
                type,
                entry.Stage,
                WorkflowGateStatus.Satisfied,
                "Satisfied by authoritative domain evidence.",
                commands,
                entry.SourceDomain,
                entry.SourceArtifact,
                entry.OccurredAt,
                entry.OccurredAt,
                null,
                entry.Summary,
                [evidence]);
        }
    }

    private static WorkflowGate CreateGate(
        Guid repositoryId,
        WorkflowGateType type,
        WorkflowStage stage,
        WorkflowGateStatus status,
        string requiredAction,
        IReadOnlyList<string> satisfyingCommands,
        string sourceDomain,
        string sourceArtifact,
        DateTimeOffset createdAt,
        DateTimeOffset? satisfiedAt,
        string? satisfiedActor,
        string reason,
        IReadOnlyList<WorkflowGateEvidence> evidence)
    {
        string gateId = WorkflowFingerprint.FromNormalizedEvidence(string.Join(
            '\n',
            "gate",
            repositoryId,
            type,
            stage,
            status,
            sourceDomain,
            sourceArtifact,
            createdAt.UtcDateTime.ToString("O"),
            satisfiedAt?.UtcDateTime.ToString("O") ?? "open")).Value;

        return new WorkflowGate(
            gateId,
            type,
            repositoryId,
            stage,
            status,
            requiredAction,
            satisfyingCommands[0],
            satisfyingCommands,
            sourceDomain,
            sourceArtifact,
            createdAt,
            satisfiedAt,
            satisfiedActor,
            reason,
            evidence);
    }

    private static bool MatchesGateSource(WorkflowGateType gateType, WorkflowTimelineEventType eventType) =>
        gateType switch
        {
            WorkflowGateType.ExecutionAcceptance => eventType is WorkflowTimelineEventType.ExecutionCompleted,
            WorkflowGateType.DecisionResolution => eventType is WorkflowTimelineEventType.ExecutionHandoffAccepted,
            WorkflowGateType.OperationalContextReview => eventType is WorkflowTimelineEventType.DecisionResolved,
            WorkflowGateType.OperationalContextPromotion => eventType is WorkflowTimelineEventType.OperationalContextAccepted or WorkflowTimelineEventType.OperationalContextEdited,
            WorkflowGateType.CommitApproval => eventType is WorkflowTimelineEventType.OperationalContextPromoted or WorkflowTimelineEventType.OperationalContextRejected or WorkflowTimelineEventType.ExecutionHandoffAccepted,
            WorkflowGateType.PushApproval => eventType is WorkflowTimelineEventType.CommitExecuted,
            WorkflowGateType.WorkSelection => eventType is WorkflowTimelineEventType.PushExecuted,
            _ => false
        };

    private static string BuildOpenGateReason(WorkflowInstance projection) =>
        projection.BlockingGate switch
        {
            WorkflowGateType.WorkSelection => "Explicit human work selection is required before workflow execution can start.",
            WorkflowGateType.ExecutionAcceptance => "Execution completed and the handoff must be accepted or rejected by a human.",
            WorkflowGateType.DecisionResolution => "Decision evidence is unresolved and must be resolved by a human.",
            WorkflowGateType.OperationalContextReview => "Operational-context proposal evidence is awaiting human review.",
            WorkflowGateType.OperationalContextPromotion => "Operational-context proposal evidence is accepted or edited and awaits human promotion.",
            WorkflowGateType.CommitApproval => "Repository changes are awaiting human commit approval.",
            WorkflowGateType.PushApproval => "Committed execution changes are awaiting human push approval.",
            _ => $"Workflow gate {projection.BlockingGate} requires human action."
        };

    private static string[] CommandsFor(WorkflowGateType gateType) =>
        CommandMap.TryGetValue(gateType, out string[]? commands)
            ? commands
            : throw new InvalidOperationException($"No workflow command mapping exists for gate {gateType}.");

    private static string RenderGateHistoryMarkdown(Guid repositoryId, WorkflowGateCatalogProjection gates)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Workflow Gate History");
        builder.AppendLine();
        builder.AppendLine($"Repository: {repositoryId}");
        builder.AppendLine($"Generated At: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine();
        builder.AppendLine("## Gates");

        if (gates.GateHistory.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("- None");
            return builder.ToString();
        }

        foreach (WorkflowGate gate in gates.GateHistory)
        {
            builder.AppendLine();
            builder.AppendLine($"- {gate.CreatedAt:O} | {gate.Type} | {gate.Status} | {gate.Reason}");
            builder.AppendLine($"  - Commands: {string.Join(", ", gate.SatisfyingCommands)}");
            builder.AppendLine($"  - Source: {gate.SourceDomain}:{gate.SourceArtifact}");
        }

        return builder.ToString();
    }
}
