using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Continuity.Abstractions;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Persistence;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowPreparationService(
    IRepositoryService repositoryService,
    IWorkflowProjectionService projectionService,
    IWorkflowRepository workflowRepository,
    IDecisionDiscoveryService? decisionDiscoveryService = null,
    IOperationalContextGenerationService? operationalContextGenerationService = null,
    IExecutionSessionService? executionSessionService = null,
    IDecisionGenerationService? decisionGenerationService = null) : IWorkflowPreparationService
{
    public async Task<WorkflowPreparationEvaluation> EvaluatePreparationAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
        WorkflowTimeline? latestTimeline = await workflowRepository.GetLatestTimelineAsync(repository);
        WorkflowStage stage = latestTimeline?.CurrentStage ?? projection.CurrentStage;
        WorkflowProgressState progressState = latestTimeline?.ProgressState ?? projection.ProgressState;
        WorkflowGateType blockingGate = ResolveBlockingGate(projection, latestTimeline);
        WorkflowPreparationCommand command = ResolveCommand(stage, projection);
        bool hasOpenGate = blockingGate is not WorkflowGateType.None;
        bool isCommitPreparationGate = command is WorkflowPreparationCommand.PrepareExecutionCommit &&
            blockingGate is WorkflowGateType.CommitApproval;
        bool isDecisionProposalGate = command is WorkflowPreparationCommand.GenerateDecisionProposal &&
            blockingGate is WorkflowGateType.DecisionResolution;
        bool isRunnableState = progressState is not WorkflowProgressState.Active
            and not WorkflowProgressState.Blocked
            and not WorkflowProgressState.Failed
            and not WorkflowProgressState.Completed;
        IReadOnlyList<string> duplicateEvidence = DetectDuplicateEvidence(projection, command);
        bool hasDuplicateDomainEvidence = duplicateEvidence.Count > 0;
        bool canPrepare = (!hasOpenGate || isCommitPreparationGate || isDecisionProposalGate) &&
            isRunnableState &&
            command is not WorkflowPreparationCommand.None &&
            !hasDuplicateDomainEvidence;

        string reason = BuildReason(
            stage,
            progressState,
            blockingGate,
            command,
            hasOpenGate,
            isRunnableState,
            hasDuplicateDomainEvidence,
            duplicateEvidence,
            canPrepare);
        WorkflowPreparationFingerprint fingerprint = WorkflowPreparationFingerprint.FromEvaluation(
            projection.RepositoryId,
            stage,
            progressState,
            blockingGate,
            command,
            canPrepare,
            reason,
            duplicateEvidence,
            projection.Diagnostics.ProjectionInputs
                .Concat([latestTimeline is null ? "latest-timeline:none" : $"latest-timeline:{latestTimeline.Fingerprint}:{latestTimeline.CurrentStage}"])
                .ToArray(),
            projection.OpenGates.Select(gate => gate.GateId).Order(StringComparer.Ordinal).ToArray());

        IReadOnlyList<string> reasoning = BuildReasoning(
            projection,
            stage,
            progressState,
            latestTimeline,
            command,
            reason,
            duplicateEvidence,
            canPrepare);
        IReadOnlyList<string> refusalReasons = canPrepare ? [] : [reason];
        var diagnostics = new WorkflowPreparationDiagnostics(
            projection.RepositoryId,
            projection.Diagnostics.ProjectionInputs
                .Concat([latestTimeline is null ? "latest-timeline:none" : $"latest-timeline:{latestTimeline.Fingerprint}:{latestTimeline.CurrentStage}"])
                .ToArray(),
            projection.GateDiagnostics.Reasoning,
            reasoning,
            refusalReasons,
            duplicateEvidence,
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
            hasDuplicateDomainEvidence,
            command,
            CommandName(command),
            BuildOutcome(canPrepare, blockingGate, hasOpenGate, hasDuplicateDomainEvidence),
            reason,
            duplicateEvidence,
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
            preparationEvent.HasDuplicateDomainEvidence == evaluation.HasDuplicateDomainEvidence);

        if (existing is not null)
        {
            return existing;
        }

        IReadOnlyList<string> createdArtifactIds = evaluation.CanPrepare
            ? await RunAllowedPreparationCommandAsync(evaluation)
            : [];
        string outcome = createdArtifactIds.Count > 0 ? "Created" : evaluation.Outcome;
        string reason = createdArtifactIds.Count > 0
            ? $"{evaluation.CommandName} created reviewable artifacts: {string.Join(", ", createdArtifactIds)}."
            : evaluation.Reason;
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
            outcome,
            reason,
            evaluation.Fingerprint,
            evaluation.IsWaitingForHuman,
            evaluation.HasDuplicateDomainEvidence,
            createdArtifactIds,
            evaluation.DuplicateEvidence,
            evaluation.Diagnostics.Reasoning
                .Concat(evaluation.Diagnostics.RefusalReasons)
                .Concat(createdArtifactIds.Select(artifactId => $"Created artifact: {artifactId}."))
                .Concat(evaluation.Diagnostics.DuplicateEvidence)
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

    private async Task<IReadOnlyList<string>> RunAllowedPreparationCommandAsync(WorkflowPreparationEvaluation evaluation)
    {
        return evaluation.Command switch
        {
            WorkflowPreparationCommand.DiscoverDecisionCandidates => await RunDecisionDiscoveryPreparationAsync(evaluation.RepositoryId),
            WorkflowPreparationCommand.GenerateDecisionProposal => await RunDecisionProposalPreparationAsync(evaluation.RepositoryId),
            WorkflowPreparationCommand.GenerateOperationalContextProposal => await RunOperationalContextProposalPreparationAsync(evaluation.RepositoryId),
            WorkflowPreparationCommand.PrepareExecutionCommit => await RunCommitPreparationAsync(evaluation.RepositoryId),
            _ => []
        };
    }

    private async Task<IReadOnlyList<string>> RunDecisionDiscoveryPreparationAsync(Guid repositoryId)
    {
        if (decisionDiscoveryService is null)
        {
            return [];
        }

        DecisionDiscoveryResult result = await decisionDiscoveryService.DiscoverAsync(repositoryId);
        return result.Candidates
            .Select(candidate => $"decision-candidate:{candidate.Id}")
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyList<string>> RunDecisionProposalPreparationAsync(Guid repositoryId)
    {
        if (decisionGenerationService is null)
        {
            return [];
        }

        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
        string candidateId = projection.CurrentDecision.CandidateId
            ?? throw new InvalidOperationException("Decision proposal preparation requires a promoted decision candidate.");
        DecisionProposal proposal = await decisionGenerationService.GenerateProposalAsync(repositoryId, candidateId);
        return [$"decision-proposal:{proposal.Id}"];
    }

    private async Task<IReadOnlyList<string>> RunOperationalContextProposalPreparationAsync(Guid repositoryId)
    {
        if (operationalContextGenerationService is null)
        {
            return [];
        }

        var proposal = await operationalContextGenerationService.GenerateAsync(repositoryId);
        return string.IsNullOrWhiteSpace(proposal.ProposalId)
            ? []
            : [$"operational-context-proposal:{proposal.ProposalId}"];
    }

    private async Task<IReadOnlyList<string>> RunCommitPreparationAsync(Guid repositoryId)
    {
        if (executionSessionService is null)
        {
            return [];
        }

        WorkflowInstance projection = await projectionService.ProjectAsync(repositoryId);
        Guid sessionId = projection.CurrentExecution.ExecutionId
            ?? throw new InvalidOperationException("Commit preparation requires an execution session.");
        CommitPreparation preparation = await executionSessionService.PrepareCommitAsync(sessionId);
        string artifactId = string.IsNullOrWhiteSpace(preparation.StatusSnapshot.Id)
            ? preparation.Id.ToString()
            : preparation.StatusSnapshot.Id;

        return [$"commit-preparation:{artifactId}"];
    }

    private static WorkflowGateType ResolveBlockingGate(WorkflowInstance projection, WorkflowTimeline? latestTimeline)
    {
        if (latestTimeline is not null &&
            latestTimeline.CurrentStage != projection.CurrentStage)
        {
            return latestTimeline.BlockingGate;
        }

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

    private static WorkflowPreparationCommand ResolveCommand(WorkflowStage stage, WorkflowInstance? projection = null) => stage switch
    {
        WorkflowStage.Decision when IsPromotedCandidate(projection?.CurrentDecision) => WorkflowPreparationCommand.GenerateDecisionProposal,
        WorkflowStage.Decision => WorkflowPreparationCommand.DiscoverDecisionCandidates,
        WorkflowStage.OperationalContext => WorkflowPreparationCommand.GenerateOperationalContextProposal,
        WorkflowStage.Commit => WorkflowPreparationCommand.PrepareExecutionCommit,
        _ => WorkflowPreparationCommand.None
    };

    private static string CommandName(WorkflowPreparationCommand command) => command switch
    {
        WorkflowPreparationCommand.DiscoverDecisionCandidates => "decisions_discover_candidates",
        WorkflowPreparationCommand.GenerateDecisionProposal => "decisions_generate_proposal",
        WorkflowPreparationCommand.GenerateOperationalContextProposal => "continuity_generate_operational_context_proposal",
        WorkflowPreparationCommand.PrepareExecutionCommit => "execution_prepare_commit",
        _ => "none"
    };

    private static IReadOnlyList<string> DetectDuplicateEvidence(
        WorkflowInstance projection,
        WorkflowPreparationCommand command)
    {
        List<string> duplicates = [];

        switch (command)
        {
            case WorkflowPreparationCommand.DiscoverDecisionCandidates:
                if (!string.IsNullOrWhiteSpace(projection.CurrentDecision.CandidateId))
                {
                    duplicates.Add($"decision-candidate:{projection.CurrentDecision.CandidateId}");
                }

                if (!string.IsNullOrWhiteSpace(projection.CurrentDecision.ProposalId))
                {
                    duplicates.Add($"decision-proposal:{projection.CurrentDecision.ProposalId}");
                }

                if (!string.IsNullOrWhiteSpace(projection.CurrentDecision.PackageId))
                {
                    duplicates.Add($"decision-package:{projection.CurrentDecision.PackageId}");
                }

                break;

            case WorkflowPreparationCommand.GenerateDecisionProposal:
                if (!string.IsNullOrWhiteSpace(projection.CurrentDecision.ProposalId))
                {
                    duplicates.Add($"decision-proposal:{projection.CurrentDecision.ProposalId}");
                }

                if (!string.IsNullOrWhiteSpace(projection.CurrentDecision.PackageId))
                {
                    duplicates.Add($"decision-package:{projection.CurrentDecision.PackageId}");
                }

                break;

            case WorkflowPreparationCommand.GenerateOperationalContextProposal:
                if (!string.IsNullOrWhiteSpace(projection.CurrentOperationalContext.ProposalId))
                {
                    duplicates.Add($"operational-context-proposal:{projection.CurrentOperationalContext.ProposalId}");
                }

                if (!string.IsNullOrWhiteSpace(projection.CurrentOperationalContext.SourceDecisionId))
                {
                    duplicates.Add($"operational-context-decision-link:{projection.CurrentOperationalContext.SourceDecisionId}");
                }

                if (!string.IsNullOrWhiteSpace(projection.CurrentOperationalContext.SourceExecutionId))
                {
                    duplicates.Add($"operational-context-execution-link:{projection.CurrentOperationalContext.SourceExecutionId}");
                }

                if (projection.OperationalContextDiagnostics.LinkageSignals.Any(signal =>
                    signal.Contains("assimilation recommendation exists", StringComparison.OrdinalIgnoreCase)))
                {
                    duplicates.Add("operational-context-assimilation");
                }

                break;

            case WorkflowPreparationCommand.PrepareExecutionCommit:
                duplicates.AddRange(projection.CurrentGit.Diagnostics.IncludedEvidence
                    .Where(input => input.Contains(":preparation=", StringComparison.Ordinal) &&
                        !input.EndsWith(":preparation=none", StringComparison.Ordinal))
                    .Select(input => $"commit-preparation:{input.Split(":preparation=", StringSplitOptions.None).Last()}"));

                if (!string.IsNullOrWhiteSpace(projection.CurrentGit.CommitId) ||
                    projection.CurrentGit.CommitTimestamp is not null ||
                    projection.CurrentGit.CommitStatus is WorkflowGitStatus.Committed or WorkflowGitStatus.Pushed)
                {
                    duplicates.Add($"prepared-commit:{projection.CurrentGit.CommitId ?? projection.CurrentGit.CommitStatus.ToString()}");
                }

                break;
        }

        return duplicates
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildOutcome(
        bool canPrepare,
        WorkflowGateType blockingGate,
        bool hasOpenGate,
        bool hasDuplicateDomainEvidence)
    {
        if (canPrepare)
        {
            return "Allowed";
        }

        if (hasDuplicateDomainEvidence &&
            blockingGate is not WorkflowGateType.OperationalContextReview and not WorkflowGateType.OperationalContextPromotion)
        {
            return "Duplicate";
        }

        return hasOpenGate ? "Refused" : "Skipped";
    }

    private static string BuildReason(
        WorkflowStage stage,
        WorkflowProgressState progressState,
        WorkflowGateType blockingGate,
        WorkflowPreparationCommand command,
        bool hasOpenGate,
        bool isRunnableState,
        bool hasDuplicateDomainEvidence,
        IReadOnlyList<string> duplicateEvidence,
        bool canPrepare)
    {
        if (hasOpenGate &&
            !((command is WorkflowPreparationCommand.PrepareExecutionCommit &&
                    blockingGate is WorkflowGateType.CommitApproval) ||
                (command is WorkflowPreparationCommand.GenerateDecisionProposal &&
                    blockingGate is WorkflowGateType.DecisionResolution)))
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

        if (hasDuplicateDomainEvidence)
        {
            return $"Preparation skipped because equivalent domain evidence already exists: {string.Join(", ", duplicateEvidence)}.";
        }

        if (canPrepare)
        {
            if (command is WorkflowPreparationCommand.PrepareExecutionCommit &&
                blockingGate is WorkflowGateType.CommitApproval)
            {
                return "Preparation may request execution_prepare_commit to create commit-review evidence; CommitApproval remains a human authority gate.";
            }

            if (command is WorkflowPreparationCommand.GenerateDecisionProposal &&
                blockingGate is WorkflowGateType.DecisionResolution)
            {
                return "Preparation may request decisions_generate_proposal to create decision-review evidence; DecisionResolution remains a human authority gate.";
            }

            return $"Preparation may request {CommandName(command)} for workflow stage {stage}.";
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
        IReadOnlyList<string> duplicateEvidence,
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
            reasoning.Add(canPrepare
                ? $"Open authority gates remain unsatisfied after preparation: {string.Join(", ", projection.OpenGates.Select(gate => gate.Type).Distinct())}."
                : $"Open authority gates refuse preparation: {string.Join(", ", projection.OpenGates.Select(gate => gate.Type).Distinct())}.");
        }

        if (duplicateEvidence.Count > 0)
        {
            reasoning.Add($"Equivalent domain evidence already exists: {string.Join(", ", duplicateEvidence)}.");
        }

        if (canPrepare)
        {
            reasoning.Add("Preparation evaluation is ready for an allowed domain command.");
        }

        return reasoning;
    }

    private static bool IsPromotedCandidate(WorkflowDecisionProjection? decision) =>
        string.Equals(decision?.CandidateState, DecisionCandidateState.Promoted.ToString(), StringComparison.Ordinal);
}
