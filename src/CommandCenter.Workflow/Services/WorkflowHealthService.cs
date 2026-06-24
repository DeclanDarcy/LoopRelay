using CommandCenter.Core.Repositories;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowHealthService(
    IRepositoryService repositoryService,
    IWorkflowProjectionService projectionService,
    IWorkflowRepository workflowRepository) : IWorkflowHealthService
{
    public async Task<WorkflowInfluenceTrace> TraceInfluenceAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
        WorkflowTimeline? latestTimeline = await workflowRepository.GetLatestTimelineAsync(repository);
        IReadOnlyList<WorkflowContinuationEvent> continuationHistory = await workflowRepository.ListContinuationEventsAsync(repository);
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory = await workflowRepository.ListPreparationEventsAsync(repository);
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;

        IReadOnlyList<string> evidencePaths = BuildEvidencePaths(projection, latestTimeline, continuationHistory, preparationHistory);
        IReadOnlyList<string> stageInfluences = BuildStageInfluences(projection, latestTimeline);
        IReadOnlyList<string> progressionInfluences = BuildProgressionInfluences(projection, continuationHistory);
        IReadOnlyList<string> preparationInfluences = BuildPreparationInfluences(preparationHistory);
        IReadOnlyList<string> gateInfluences = BuildGateInfluences(projection);
        IReadOnlyList<string> blockingInfluences = BuildBlockingInfluences(projection);
        IReadOnlyList<string> conflicts = projection.Diagnostics.Conflicts
            .Concat(projection.GateDiagnostics.Conflicts)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        string normalized = string.Join(
            '\n',
            "influence-trace",
            repositoryId,
            projection.CurrentStage,
            projection.ProgressState,
            projection.BlockingGate,
            string.Join("|", evidencePaths),
            string.Join("|", stageInfluences),
            string.Join("|", progressionInfluences),
            string.Join("|", preparationInfluences),
            string.Join("|", gateInfluences),
            string.Join("|", blockingInfluences),
            string.Join("|", conflicts));
        string fingerprint = WorkflowFingerprint.FromNormalizedEvidence(normalized).Value;

        return new WorkflowInfluenceTrace(
            repositoryId,
            generatedAt,
            projection.CurrentStage,
            projection.ProgressState,
            projection.BlockingGate,
            evidencePaths,
            stageInfluences,
            progressionInfluences,
            preparationInfluences,
            gateInfluences,
            blockingInfluences,
            conflicts,
            fingerprint);
    }

    public async Task<WorkflowHealthAssessment> AssessHealthAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
        WorkflowTimeline? latestTimeline = await workflowRepository.GetLatestTimelineAsync(repository);
        IReadOnlyList<WorkflowContinuationEvent> continuationHistory = await workflowRepository.ListContinuationEventsAsync(repository);
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory = await workflowRepository.ListPreparationEventsAsync(repository);
        WorkflowInfluenceTrace trace = await TraceInfluenceAsync(repositoryId);

        WorkflowHealthDimension projectionHealth = AssessProjectionHealth(projection);
        WorkflowHealthDimension recoveryHealth = AssessRecoveryHealth(projection, latestTimeline);
        WorkflowHealthDimension gateHealth = AssessGateHealth(projection);
        WorkflowHealthDimension continuationHealth = AssessContinuationHealth(projection, continuationHistory);
        WorkflowHealthDimension preparationHealth = AssessPreparationHealth(projection, preparationHistory);
        IReadOnlyList<WorkflowHealthDimension> dimensions =
        [
            projectionHealth,
            recoveryHealth,
            gateHealth,
            continuationHealth,
            preparationHealth
        ];

        string overall = dimensions.Any(dimension => string.Equals(dimension.Status, "Blocked", StringComparison.Ordinal))
            ? "Blocked"
            : dimensions.Any(dimension => string.Equals(dimension.Status, "Degraded", StringComparison.Ordinal))
                ? "Degraded"
                : "Healthy";

        return new WorkflowHealthAssessment(
            repositoryId,
            DateTimeOffset.UtcNow,
            overall,
            dimensions,
            trace,
            dimensions.SelectMany(dimension => dimension.Diagnostics).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(candidate => candidate.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository '{repositoryId}' was not found.");
    }

    private static IReadOnlyList<string> BuildEvidencePaths(
        WorkflowInstance projection,
        WorkflowTimeline? latestTimeline,
        IReadOnlyList<WorkflowContinuationEvent> continuationHistory,
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        List<string> paths = [.. projection.Diagnostics.ProjectionInputs];
        paths.Add(latestTimeline is null
            ? "workflow-timeline:none"
            : $"workflow-timeline:{latestTimeline.Fingerprint}:{latestTimeline.CurrentStage}");
        paths.AddRange(projection.Timeline.Select(entry => $"timeline-entry:{entry.SourceDomain}:{entry.SourceArtifact}:{entry.EventType}"));
        paths.AddRange(projection.GateHistory.Select(gate => $"gate:{gate.Type}:{gate.Status}:{gate.SourceDomain}:{gate.SourceArtifact}"));
        paths.AddRange(continuationHistory.Select(entry => $"continuation:{entry.EventId}:{entry.Decision}:{entry.InputFingerprint}"));
        paths.AddRange(preparationHistory.Select(entry => $"preparation:{entry.EventId}:{entry.CommandName}:{entry.InputFingerprint}"));
        return paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> BuildStageInfluences(WorkflowInstance projection, WorkflowTimeline? latestTimeline)
    {
        List<string> influences =
        [
            $"Projection selected {projection.CurrentStage} from aggregate domain evidence.",
            $"Progress state is {projection.ProgressState}.",
            $"Blocking gate is {projection.BlockingGate}."
        ];
        influences.AddRange(projection.Diagnostics.Reasoning);
        influences.Add(latestTimeline is null
            ? "No persisted workflow timeline influenced the coordinator stage."
            : $"Latest persisted workflow timeline reports {latestTimeline.CurrentStage} with fingerprint {latestTimeline.Fingerprint}.");
        return influences.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> BuildProgressionInfluences(
        WorkflowInstance projection,
        IReadOnlyList<WorkflowContinuationEvent> continuationHistory)
    {
        List<string> influences =
        [
            $"State machine exposed {projection.ValidTransitions.Count} valid transitions and {projection.BlockedTransitions.Count} blocked transitions."
        ];
        influences.AddRange(projection.ValidTransitions.Select(transition =>
            $"Valid transition: {transition.Transition.FromStage} -> {transition.Transition.ToStage}."));
        influences.AddRange(projection.BlockedTransitions.Select(transition =>
            $"Blocked transition: {transition.Transition.FromStage} -> {transition.Transition.ToStage}: {transition.Reason}"));
        WorkflowContinuationEvent? latest = continuationHistory.OrderByDescending(entry => entry.OccurredAt).FirstOrDefault();
        influences.Add(latest is null
            ? "No persisted continuation event influenced progression."
            : $"Latest continuation event {latest.EventId} {latest.Decision}: {latest.Reason}");
        return influences.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> BuildPreparationInfluences(IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        WorkflowPreparationEvent? latest = preparationHistory.OrderByDescending(entry => entry.OccurredAt).FirstOrDefault();
        if (latest is null)
        {
            return ["No persisted preparation event influenced reviewable artifact preparation."];
        }

        List<string> influences =
        [
            $"Latest preparation event {latest.EventId} used {latest.CommandName} and decided {latest.Decision}: {latest.Reason}"
        ];
        influences.AddRange(latest.CreatedArtifactIds.Select(artifactId => $"Created reviewable artifact evidence: {artifactId}."));
        influences.AddRange(latest.DuplicateEvidence.Select(duplicate => $"Duplicate domain evidence: {duplicate}."));
        return influences.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> BuildGateInfluences(WorkflowInstance projection)
    {
        List<string> influences = [];
        influences.AddRange(projection.OpenGates.Select(gate =>
            $"Open gate {gate.Type} at {gate.Stage}: {gate.RequiredAction}"));
        influences.AddRange(projection.SatisfiedGates.Select(gate =>
            $"Satisfied gate {gate.Type} from {gate.SourceDomain}:{gate.SourceArtifact}."));
        influences.AddRange(projection.GateDiagnostics.Reasoning);
        return influences.Count == 0
            ? ["No workflow gate evidence influenced the current projection."]
            : influences.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> BuildBlockingInfluences(WorkflowInstance projection)
    {
        List<string> influences = [];
        influences.AddRange(projection.BlockedTransitions.Select(transition =>
            $"{transition.BlockingCondition}: {transition.Reason}"));
        influences.AddRange(projection.Diagnostics.UnknownStates.Select(state => $"Unknown state evidence: {state}."));
        influences.AddRange(projection.Diagnostics.Conflicts.Select(conflict => $"Conflict: {conflict}."));
        influences.AddRange(projection.GateDiagnostics.Conflicts.Select(conflict => $"Gate conflict: {conflict}."));
        if (projection.BlockingGate is not WorkflowGateType.None)
        {
            influences.Add($"Current blocking gate {projection.BlockingGate} requires: {projection.RequiredHumanAction}");
        }

        return influences.Count == 0
            ? ["No blocking evidence is present."]
            : influences.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static WorkflowHealthDimension AssessProjectionHealth(WorkflowInstance projection)
    {
        if (projection.Diagnostics.Conflicts.Count > 0 || projection.Diagnostics.UnknownStates.Count > 0)
        {
            return new WorkflowHealthDimension(
                "Projection",
                "Degraded",
                "Projection contains conflicting or unknown evidence.",
                projection.Diagnostics.ProjectionInputs,
                projection.Diagnostics.Conflicts.Concat(projection.Diagnostics.UnknownStates).ToArray());
        }

        return new WorkflowHealthDimension(
            "Projection",
            "Healthy",
            $"Projection selected {projection.CurrentStage} from domain evidence.",
            projection.Diagnostics.ProjectionInputs,
            projection.Diagnostics.Reasoning);
    }

    private static WorkflowHealthDimension AssessRecoveryHealth(WorkflowInstance projection, WorkflowTimeline? latestTimeline)
    {
        if (latestTimeline is null)
        {
            return new WorkflowHealthDimension(
                "Recovery",
                "Healthy",
                "No persisted workflow timeline exists; projection remains recoverable from domain evidence.",
                projection.Diagnostics.ProjectionInputs,
                []);
        }

        if (latestTimeline.RepositoryId != projection.RepositoryId)
        {
            return new WorkflowHealthDimension(
                "Recovery",
                "Blocked",
                "Latest workflow timeline belongs to a different repository.",
                [$"workflow-timeline:{latestTimeline.Fingerprint}"],
                ["Repository ownership mismatch."]);
        }

        return new WorkflowHealthDimension(
            "Recovery",
            "Healthy",
            "Latest persisted workflow timeline is repository-owned derived evidence.",
            [$"workflow-timeline:{latestTimeline.Fingerprint}:{latestTimeline.CurrentStage}"],
            []);
    }

    private static WorkflowHealthDimension AssessGateHealth(WorkflowInstance projection)
    {
        if (projection.GateDiagnostics.Conflicts.Count > 0)
        {
            return new WorkflowHealthDimension(
                "Gates",
                "Degraded",
                "Gate catalog reports conflicting evidence.",
                projection.GateHistory.Select(gate => $"{gate.Type}:{gate.Status}:{gate.SourceArtifact}").ToArray(),
                projection.GateDiagnostics.Conflicts);
        }

        if (projection.OpenGates.Count > 0)
        {
            return new WorkflowHealthDimension(
                "Gates",
                "Blocked",
                "At least one human authority gate is open.",
                projection.OpenGates.Select(gate => $"{gate.Type}:{gate.RequiredAction}").ToArray(),
                projection.GateDiagnostics.Reasoning);
        }

        return new WorkflowHealthDimension(
            "Gates",
            "Healthy",
            "No open authority gate is blocking workflow progression.",
            projection.SatisfiedGates.Select(gate => $"{gate.Type}:{gate.SourceArtifact}").ToArray(),
            projection.GateDiagnostics.Reasoning);
    }

    private static WorkflowHealthDimension AssessContinuationHealth(
        WorkflowInstance projection,
        IReadOnlyList<WorkflowContinuationEvent> continuationHistory)
    {
        WorkflowContinuationEvent? latest = continuationHistory.OrderByDescending(entry => entry.OccurredAt).FirstOrDefault();
        if (latest?.Diagnostics.Any(diagnostic => diagnostic.Contains("conflict", StringComparison.OrdinalIgnoreCase)) is true)
        {
            return new WorkflowHealthDimension(
                "Continuation",
                "Degraded",
                "Latest continuation event contains conflict diagnostics.",
                [$"continuation:{latest.EventId}:{latest.InputFingerprint}"],
                latest.Diagnostics);
        }

        if (projection.ProgressState is WorkflowProgressState.Active)
        {
            return new WorkflowHealthDimension(
                "Continuation",
                "Healthy",
                "Workflow is active; continuation is waiting for domain evidence.",
                projection.Diagnostics.ProjectionInputs,
                []);
        }

        return new WorkflowHealthDimension(
            "Continuation",
            "Healthy",
            latest is null
                ? "No continuation event has been recorded yet; current state is derived from projection."
                : $"Latest continuation event decided {latest.Decision}.",
            latest is null ? projection.Diagnostics.ProjectionInputs : [$"continuation:{latest.EventId}:{latest.InputFingerprint}"],
            latest?.Diagnostics ?? []);
    }

    private static WorkflowHealthDimension AssessPreparationHealth(
        WorkflowInstance projection,
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory)
    {
        WorkflowPreparationEvent? latest = preparationHistory.OrderByDescending(entry => entry.OccurredAt).FirstOrDefault();
        if (latest?.Diagnostics.Any(diagnostic => diagnostic.Contains("conflict", StringComparison.OrdinalIgnoreCase)) is true)
        {
            return new WorkflowHealthDimension(
                "Preparation",
                "Degraded",
                "Latest preparation event contains conflict diagnostics.",
                [$"preparation:{latest.EventId}:{latest.InputFingerprint}"],
                latest.Diagnostics);
        }

        return new WorkflowHealthDimension(
            "Preparation",
            "Healthy",
            latest is null
                ? "No preparation event has been recorded yet; no duplicate preparation evidence is implied."
                : $"Latest preparation event decided {latest.Decision}.",
            latest is null ? projection.Diagnostics.ProjectionInputs : [$"preparation:{latest.EventId}:{latest.InputFingerprint}"],
            latest?.Diagnostics ?? []);
    }
}
