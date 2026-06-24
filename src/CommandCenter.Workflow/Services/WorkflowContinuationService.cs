using CommandCenter.Core.Repositories;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Persistence;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowContinuationService(
    IRepositoryService repositoryService,
    IWorkflowProjectionService projectionService,
    IWorkflowStateMachineService stateMachineService,
    IWorkflowRepository workflowRepository) : IWorkflowContinuationService
{
    public async Task<WorkflowContinuationEvaluation> EvaluateContinuationAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
        WorkflowTimeline? latestTimeline = await workflowRepository.GetLatestTimelineAsync(repository);
        WorkflowStage fromStage = latestTimeline?.CurrentStage ?? projection.CurrentStage;
        WorkflowProgressState progressState = latestTimeline?.ProgressState ?? projection.ProgressState;
        WorkflowGateType blockingGate = latestTimeline?.BlockingGate ?? projection.BlockingGate;
        WorkflowStateMachineDiagnostics stateMachine = stateMachineService.Evaluate(
            repositoryId,
            fromStage,
            progressState,
            blockingGate,
            projection.RequiredHumanAction);
        WorkflowTransitionResult? transition = stateMachine.ValidTransitions.SingleOrDefault();
        bool hasOpenGate = blockingGate is not WorkflowGateType.None;
        bool isTerminal = fromStage is WorkflowStage.Completed or WorkflowStage.Failed or WorkflowStage.Blocked;
        bool domainEvidenceSupportsTransition = transition is not null &&
            ProjectionHasReachedOrPassed(projection.CurrentStage, transition.Transition.ToStage);
        bool canAdvance = !hasOpenGate &&
            !isTerminal &&
            progressState is not WorkflowProgressState.Active and not WorkflowProgressState.Failed and not WorkflowProgressState.Blocked &&
            transition is not null &&
            stateMachine.ValidTransitions.Count == 1 &&
            domainEvidenceSupportsTransition;

        string stopReason = BuildStopReason(
            projection,
            fromStage,
            progressState,
            blockingGate,
            transition,
            hasOpenGate,
            isTerminal,
            domainEvidenceSupportsTransition,
            stateMachine.ValidTransitions.Count);
        WorkflowStage? toStage = canAdvance ? transition!.Transition.ToStage : null;
        WorkflowContinuationFingerprint fingerprint = WorkflowContinuationFingerprint.FromEvaluation(
            projection.RepositoryId,
            fromStage,
            toStage,
            progressState,
            blockingGate,
            canAdvance,
            stopReason,
            projection.Diagnostics.ProjectionInputs
                .Concat([latestTimeline is null ? "latest-timeline:none" : $"latest-timeline:{latestTimeline.Fingerprint}:{latestTimeline.CurrentStage}"])
                .ToArray(),
            projection.OpenGates.Select(gate => gate.GateId).Order(StringComparer.Ordinal).ToArray());

        IReadOnlyList<string> reasoning = BuildReasoning(
            projection,
            fromStage,
            progressState,
            transition,
            latestTimeline,
            canAdvance,
            stopReason);
        var diagnostics = new WorkflowContinuationDiagnostics(
            projection.RepositoryId,
            projection.Diagnostics.ProjectionInputs
                .Concat([latestTimeline is null ? "latest-timeline:none" : $"latest-timeline:{latestTimeline.Fingerprint}:{latestTimeline.CurrentStage}"])
                .ToArray(),
            stateMachine.Reasoning,
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
            fromStage,
            toStage,
            progressState,
            blockingGate,
            canAdvance,
            hasOpenGate || (!canAdvance && projection.OpenGates.Count > 0),
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

        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
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

        WorkflowContinuationEvent saved = await workflowRepository.SaveContinuationEventAsync(repository, continuationEvent);
        if (evaluation.CanAdvanceMechanically && evaluation.ToStage is WorkflowStage toStage)
        {
            WorkflowTimeline timeline = WorkflowTimelineFactory.Create(
                projection,
                occurredAt,
                toStage,
                evaluation.FromStage);
            await workflowRepository.SaveTimelineAsync(repository, timeline);
        }

        return saved;
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
        WorkflowStage fromStage,
        WorkflowProgressState progressState,
        WorkflowGateType blockingGate,
        WorkflowTransitionResult? transition,
        bool hasOpenGate,
        bool isTerminal,
        bool domainEvidenceSupportsTransition,
        int validTransitionCount)
    {
        if (hasOpenGate)
        {
            return blockingGate is WorkflowGateType.None
                ? "An open workflow gate is awaiting human action."
                : $"Workflow is waiting for human action at {blockingGate}.";
        }

        if (progressState is WorkflowProgressState.Active)
        {
            return "Workflow execution is still active.";
        }

        if (progressState is WorkflowProgressState.Failed)
        {
            return "Workflow is failed and cannot continue mechanically.";
        }

        if (progressState is WorkflowProgressState.Blocked)
        {
            return "Workflow is blocked and cannot continue mechanically.";
        }

        if (isTerminal)
        {
            return $"Workflow is terminal at {fromStage}.";
        }

        if (validTransitionCount > 1)
        {
            return $"Multiple mechanical transitions are available from {fromStage}; continuation requires exactly one.";
        }

        if (transition is null)
        {
            return $"No valid mechanical transition is available from {fromStage}.";
        }

        if (!domainEvidenceSupportsTransition)
        {
            return $"Domain evidence has not yet reached {transition.Transition.ToStage}; current projection is {projection.CurrentStage}.";
        }

        return $"Transition {transition.Transition.FromStage} -> {transition.Transition.ToStage} can advance mechanically.";
    }

    private static IReadOnlyList<string> BuildReasoning(
        WorkflowInstance projection,
        WorkflowStage fromStage,
        WorkflowProgressState progressState,
        WorkflowTransitionResult? transition,
        WorkflowTimeline? latestTimeline,
        bool canAdvance,
        string stopReason)
    {
        List<string> reasoning =
        [
            $"Continuation evaluated aggregate workflow projection for repository {projection.RepositoryId}.",
            latestTimeline is null
                ? "No persisted workflow timeline exists; projection stage is used as coordinator stage."
                : $"Latest persisted workflow timeline is at {latestTimeline.CurrentStage} with fingerprint {latestTimeline.Fingerprint}.",
            $"Coordinator stage is {fromStage} with progress state {progressState}; domain projection stage is {projection.CurrentStage}.",
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

    private static bool ProjectionHasReachedOrPassed(WorkflowStage projectionStage, WorkflowStage requiredStage)
    {
        int projectionIndex = StageIndex(projectionStage);
        int requiredIndex = StageIndex(requiredStage);
        return projectionIndex >= 0 &&
            requiredIndex >= 0 &&
            projectionIndex >= requiredIndex;
    }

    private static int StageIndex(WorkflowStage stage) => stage switch
    {
        WorkflowStage.WorkSelection => 0,
        WorkflowStage.Execution => 1,
        WorkflowStage.Handoff => 2,
        WorkflowStage.Decision => 3,
        WorkflowStage.OperationalContext => 4,
        WorkflowStage.Commit => 5,
        WorkflowStage.Push => 6,
        WorkflowStage.Completed => 7,
        _ => -1
    };
}
