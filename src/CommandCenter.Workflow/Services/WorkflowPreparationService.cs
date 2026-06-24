using CommandCenter.Core.Repositories;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Persistence;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowPreparationService(
    IRepositoryService repositoryService,
    IWorkflowProjectionService projectionService,
    IWorkflowRepository workflowRepository) : IWorkflowPreparationService
{
    public async Task<WorkflowPreparationEvaluation> EvaluatePreparationAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
        WorkflowTimeline? latestTimeline = await workflowRepository.GetLatestTimelineAsync(repository);
        WorkflowStage stage = latestTimeline?.CurrentStage ?? projection.CurrentStage;
        WorkflowProgressState progressState = latestTimeline?.ProgressState ?? projection.ProgressState;
        WorkflowGateType blockingGate = ResolveBlockingGate(projection, latestTimeline);
        WorkflowPreparationCommand command = ResolveCommand(stage);
        bool hasOpenGate = blockingGate is not WorkflowGateType.None || projection.OpenGates.Count > 0;
        bool isRunnableState = progressState is not WorkflowProgressState.Active
            and not WorkflowProgressState.Blocked
            and not WorkflowProgressState.Failed
            and not WorkflowProgressState.Completed;
        bool canPrepare = !hasOpenGate &&
            isRunnableState &&
            command is not WorkflowPreparationCommand.None;

        string reason = BuildReason(stage, progressState, blockingGate, command, hasOpenGate, isRunnableState, canPrepare);
        WorkflowPreparationFingerprint fingerprint = WorkflowPreparationFingerprint.FromEvaluation(
            projection.RepositoryId,
            stage,
            progressState,
            blockingGate,
            command,
            canPrepare,
            reason,
            projection.Diagnostics.ProjectionInputs
                .Concat([latestTimeline is null ? "latest-timeline:none" : $"latest-timeline:{latestTimeline.Fingerprint}:{latestTimeline.CurrentStage}"])
                .ToArray(),
            projection.OpenGates.Select(gate => gate.GateId).Order(StringComparer.Ordinal).ToArray());

        IReadOnlyList<string> reasoning = BuildReasoning(projection, stage, progressState, latestTimeline, command, reason, canPrepare);
        IReadOnlyList<string> refusalReasons = canPrepare ? [] : [reason];
        var diagnostics = new WorkflowPreparationDiagnostics(
            projection.RepositoryId,
            projection.Diagnostics.ProjectionInputs
                .Concat([latestTimeline is null ? "latest-timeline:none" : $"latest-timeline:{latestTimeline.Fingerprint}:{latestTimeline.CurrentStage}"])
                .ToArray(),
            projection.GateDiagnostics.Reasoning,
            reasoning,
            refusalReasons,
            projection.Diagnostics.Conflicts
                .Concat(projection.GateDiagnostics.Conflicts)
                .ToArray(),
            projection.OpenGates.Count,
            projection.SatisfiedGates.Count,
            fingerprint);

        return new WorkflowPreparationEvaluation(
            projection.RepositoryId,
            stage,
            progressState,
            blockingGate,
            canPrepare,
            hasOpenGate,
            command,
            CommandName(command),
            canPrepare ? "Ready" : "Refused",
            reason,
            fingerprint,
            diagnostics);
    }

    public async Task<WorkflowPreparationEvent> RunPreparationAsync(Guid repositoryId, string trigger = "endpoint")
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowPreparationEvaluation evaluation = await EvaluatePreparationAsync(repositoryId);
        IReadOnlyList<WorkflowPreparationEvent> history = await workflowRepository.ListPreparationEventsAsync(repository);
        WorkflowPreparationEvent? existing = history.FirstOrDefault(preparationEvent =>
            preparationEvent.InputFingerprint == evaluation.Fingerprint &&
            preparationEvent.Stage == evaluation.Stage &&
            preparationEvent.Command == evaluation.Command &&
            preparationEvent.Decision == evaluation.Outcome &&
            preparationEvent.Reason == evaluation.Reason);

        if (existing is not null)
        {
            return existing;
        }

        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var preparationEvent = new WorkflowPreparationEvent(
            repositoryId,
            WorkflowArtifactPaths.PreparationEventId(occurredAt),
            occurredAt,
            string.IsNullOrWhiteSpace(trigger) ? "endpoint" : trigger,
            evaluation.Stage,
            evaluation.ProgressState,
            evaluation.BlockingGate,
            evaluation.Command,
            evaluation.CommandName,
            evaluation.Outcome,
            evaluation.Reason,
            evaluation.Fingerprint,
            evaluation.IsWaitingForHuman,
            [],
            evaluation.Diagnostics.Reasoning
                .Concat(evaluation.Diagnostics.RefusalReasons)
                .Concat(evaluation.Diagnostics.Conflicts)
                .ToArray());

        return await workflowRepository.SavePreparationEventAsync(repository, preparationEvent);
    }

    public async Task<IReadOnlyList<WorkflowPreparationEvent>> GetPreparationHistoryAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await workflowRepository.ListPreparationEventsAsync(repository);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(candidate => candidate.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository '{repositoryId}' was not found.");
    }

    private static WorkflowGateType ResolveBlockingGate(WorkflowInstance projection, WorkflowTimeline? latestTimeline)
    {
        if (projection.BlockingGate is not WorkflowGateType.None)
        {
            return projection.BlockingGate;
        }

        if (projection.OpenGates.Count > 0)
        {
            return projection.OpenGates[0].Type;
        }

        return latestTimeline?.BlockingGate ?? WorkflowGateType.None;
    }

    private static WorkflowPreparationCommand ResolveCommand(WorkflowStage stage) => stage switch
    {
        WorkflowStage.Decision => WorkflowPreparationCommand.GenerateDecisionReviewArtifacts,
        WorkflowStage.OperationalContext => WorkflowPreparationCommand.GenerateOperationalContextProposal,
        WorkflowStage.Commit => WorkflowPreparationCommand.PrepareExecutionCommit,
        _ => WorkflowPreparationCommand.None
    };

    private static string CommandName(WorkflowPreparationCommand command) => command switch
    {
        WorkflowPreparationCommand.GenerateDecisionReviewArtifacts => "decisions_generate_review_artifacts",
        WorkflowPreparationCommand.GenerateOperationalContextProposal => "continuity_generate_operational_context_proposal",
        WorkflowPreparationCommand.PrepareExecutionCommit => "execution_prepare_commit",
        _ => "none"
    };

    private static string BuildReason(
        WorkflowStage stage,
        WorkflowProgressState progressState,
        WorkflowGateType blockingGate,
        WorkflowPreparationCommand command,
        bool hasOpenGate,
        bool isRunnableState,
        bool canPrepare)
    {
        if (hasOpenGate)
        {
            return blockingGate is WorkflowGateType.None
                ? "Preparation refused because an open authority gate is awaiting human action."
                : $"Preparation refused because {blockingGate} is awaiting human action.";
        }

        if (!isRunnableState)
        {
            return $"Preparation refused because workflow progress state is {progressState}.";
        }

        if (command is WorkflowPreparationCommand.None)
        {
            return $"No preparation command is defined for workflow stage {stage}.";
        }

        if (canPrepare)
        {
            return $"Preparation may request {CommandName(command)} for workflow stage {stage}; domain invocation is deferred.";
        }

        return "Preparation refused by workflow evaluation.";
    }

    private static IReadOnlyList<string> BuildReasoning(
        WorkflowInstance projection,
        WorkflowStage stage,
        WorkflowProgressState progressState,
        WorkflowTimeline? latestTimeline,
        WorkflowPreparationCommand command,
        string reason,
        bool canPrepare)
    {
        List<string> reasoning =
        [
            $"Preparation evaluated aggregate workflow projection for repository {projection.RepositoryId}.",
            latestTimeline is null
                ? "No persisted workflow timeline exists; projection stage is used as preparation stage."
                : $"Latest persisted workflow timeline is at {latestTimeline.CurrentStage} with fingerprint {latestTimeline.Fingerprint}.",
            $"Preparation stage is {stage} with progress state {progressState}; domain projection stage is {projection.CurrentStage}.",
            reason
        ];

        if (command is not WorkflowPreparationCommand.None)
        {
            reasoning.Add($"Candidate preparation command is {CommandName(command)}.");
        }

        if (projection.OpenGates.Count > 0)
        {
            reasoning.Add($"Open authority gates refuse preparation: {string.Join(", ", projection.OpenGates.Select(gate => gate.Type).Distinct())}.");
        }

        if (canPrepare)
        {
            reasoning.Add("Preparation evaluation is ready, but this milestone slice does not invoke domain commands.");
        }

        return reasoning;
    }
}
