using CommandCenter.Core.Repositories;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Persistence;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowContinuationService(
    IRepositoryService repositoryService,
    IWorkflowProjectionService projectionService,
    IWorkflowRepository workflowRepository) : IWorkflowContinuationService
{
    public async Task<WorkflowContinuationEvaluation> EvaluateContinuationAsync(Guid repositoryId)
    {
        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
        WorkflowTransitionResult? transition = projection.ValidTransitions.FirstOrDefault();
        bool hasOpenGate = projection.OpenGates.Count > 0 || projection.BlockingGate is not WorkflowGateType.None;
        bool isTerminal = projection.CurrentStage is WorkflowStage.Completed or WorkflowStage.Failed or WorkflowStage.Blocked;
        bool canAdvance = !hasOpenGate &&
            !isTerminal &&
            projection.ProgressState is not WorkflowProgressState.Active and not WorkflowProgressState.Failed and not WorkflowProgressState.Blocked &&
            transition is not null;

        string stopReason = BuildStopReason(projection, transition, hasOpenGate, isTerminal);
        WorkflowStage? toStage = canAdvance ? transition!.Transition.ToStage : null;
        WorkflowContinuationFingerprint fingerprint = WorkflowContinuationFingerprint.FromEvaluation(
            projection.RepositoryId,
            projection.CurrentStage,
            toStage,
            projection.ProgressState,
            projection.BlockingGate,
            canAdvance,
            stopReason,
            projection.Diagnostics.ProjectionInputs,
            projection.OpenGates.Select(gate => gate.GateId).Order(StringComparer.Ordinal).ToArray());

        IReadOnlyList<string> reasoning = BuildReasoning(projection, transition, canAdvance, stopReason);
        var diagnostics = new WorkflowContinuationDiagnostics(
            projection.RepositoryId,
            projection.Diagnostics.ProjectionInputs,
            projection.Diagnostics.StateMachine.Reasoning,
            projection.GateDiagnostics.Reasoning,
            projection.CompletionEvaluation.Evidence,
            reasoning,
            canAdvance ? [] : [stopReason],
            projection.Diagnostics.Conflicts
                .Concat(projection.GateDiagnostics.Conflicts)
                .Concat(projection.CompletionEvaluation.Diagnostics)
                .ToArray(),
            projection.OpenGates.Count,
            projection.SatisfiedGates.Count,
            fingerprint);

        return new WorkflowContinuationEvaluation(
            projection.RepositoryId,
            projection.CurrentStage,
            toStage,
            projection.ProgressState,
            projection.BlockingGate,
            canAdvance,
            hasOpenGate,
            projection.CompletionEvaluation.IsComplete,
            projection.RequiredHumanAction,
            canAdvance ? "Advance" : "Stop",
            stopReason,
            fingerprint,
            transition,
            projection.CompletionEvaluation,
            diagnostics);
    }

    public async Task<WorkflowContinuationEvent> RunContinuationAsync(Guid repositoryId, string trigger = "endpoint")
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowContinuationEvaluation evaluation = await EvaluateContinuationAsync(repositoryId);
        IReadOnlyList<WorkflowContinuationEvent> history = await workflowRepository.ListContinuationEventsAsync(repository);
        WorkflowContinuationEvent? existing = history.FirstOrDefault(continuationEvent =>
            continuationEvent.InputFingerprint == evaluation.Fingerprint &&
            continuationEvent.FromStage == evaluation.FromStage &&
            continuationEvent.ToStage == evaluation.ToStage &&
            continuationEvent.Decision == evaluation.Outcome &&
            continuationEvent.Reason == evaluation.StopReason);

        if (existing is not null)
        {
            return existing;
        }

        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var continuationEvent = new WorkflowContinuationEvent(
            repositoryId,
            WorkflowArtifactPaths.ContinuationEventId(occurredAt),
            occurredAt,
            string.IsNullOrWhiteSpace(trigger) ? "endpoint" : trigger,
            evaluation.FromStage,
            evaluation.ToStage,
            evaluation.ProgressState,
            evaluation.BlockingGate,
            evaluation.Outcome,
            evaluation.StopReason,
            evaluation.Fingerprint,
            evaluation.IsWaitingForHuman,
            evaluation.IsComplete,
            evaluation.RequiredHumanAction,
            evaluation.Diagnostics.Reasoning
                .Concat(evaluation.Diagnostics.StopReasons)
                .Concat(evaluation.Diagnostics.Conflicts)
                .ToArray());

        return await workflowRepository.SaveContinuationEventAsync(repository, continuationEvent);
    }

    public async Task<IReadOnlyList<WorkflowContinuationEvent>> GetContinuationHistoryAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await workflowRepository.ListContinuationEventsAsync(repository);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(candidate => candidate.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository '{repositoryId}' was not found.");
    }

    private static string BuildStopReason(
        WorkflowInstance projection,
        WorkflowTransitionResult? transition,
        bool hasOpenGate,
        bool isTerminal)
    {
        if (hasOpenGate)
        {
            return projection.BlockingGate is WorkflowGateType.None
                ? "An open workflow gate is awaiting human action."
                : $"Workflow is waiting for human action at {projection.BlockingGate}.";
        }

        if (projection.ProgressState is WorkflowProgressState.Active)
        {
            return "Workflow execution is still active.";
        }

        if (projection.ProgressState is WorkflowProgressState.Failed)
        {
            return "Workflow is failed and cannot continue mechanically.";
        }

        if (projection.ProgressState is WorkflowProgressState.Blocked)
        {
            return "Workflow is blocked and cannot continue mechanically.";
        }

        if (isTerminal)
        {
            return $"Workflow is terminal at {projection.CurrentStage}.";
        }

        if (transition is null)
        {
            return $"No valid mechanical transition is available from {projection.CurrentStage}.";
        }

        return $"Transition {transition.Transition.FromStage} -> {transition.Transition.ToStage} can advance mechanically.";
    }

    private static IReadOnlyList<string> BuildReasoning(
        WorkflowInstance projection,
        WorkflowTransitionResult? transition,
        bool canAdvance,
        string stopReason)
    {
        List<string> reasoning =
        [
            $"Continuation evaluated aggregate workflow projection for repository {projection.RepositoryId}.",
            $"Current stage is {projection.CurrentStage} with progress state {projection.ProgressState}.",
            stopReason
        ];

        if (canAdvance && transition is not null)
        {
            reasoning.Add($"Valid state-machine transition selected: {transition.Transition.FromStage} -> {transition.Transition.ToStage}.");
        }

        if (projection.OpenGates.Count > 0)
        {
            reasoning.Add($"Open authority gates stop continuation: {string.Join(", ", projection.OpenGates.Select(gate => gate.Type).Distinct())}.");
        }

        if (projection.CompletionEvaluation.IsComplete)
        {
            reasoning.Add(projection.CompletionEvaluation.CompletionReason);
        }

        return reasoning;
    }
}
