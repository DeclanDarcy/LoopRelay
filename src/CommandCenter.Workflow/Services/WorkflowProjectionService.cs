using CommandCenter.Core.Repositories;
using CommandCenter.Execution.Models;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowProjectionService(
    IRepositoryService repositoryService,
    IWorkflowExecutionService workflowExecutionService,
    IWorkflowHandoffService workflowHandoffService,
    IWorkflowDecisionService workflowDecisionService,
    IWorkflowOperationalContextService workflowOperationalContextService,
    IWorkflowGitService workflowGitService,
    IWorkflowStateMachineService stateMachineService) : IWorkflowProjectionService
{
    public async Task<WorkflowInstance> ProjectAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        ProjectionEvidence evidence = await LoadEvidenceAsync(repository);
        IReadOnlyList<WorkflowTimelineEntry> timeline = BuildTimeline(evidence);
        ProjectionChoice choice = ChooseProjection(evidence);
        WorkflowStateMachineDiagnostics stateMachine = stateMachineService.Evaluate(
            repository.Id,
            choice.Stage,
            choice.ProgressState,
            choice.Gate,
            choice.RequiredHumanAction);

        var diagnostics = new WorkflowProjectionDiagnostics(
            repository.Id,
            BuildInputs(evidence),
            choice.Stage,
            choice.Gate,
            stateMachine.CandidateStages,
            stateMachine.ValidTransitions,
            stateMachine.BlockedTransitions,
            stateMachine,
            choice.Reasoning,
            choice.UnknownStates,
            choice.Conflicts);

        var preliminary = new WorkflowInstance(
            repository.Id,
            choice.Stage,
            choice.ProgressState,
            choice.Gate,
            choice.RequiredHumanAction,
            evidence.Execution,
            evidence.Execution.Status,
            evidence.Execution.IsExecutionEligible,
            evidence.Execution.Failure,
            evidence.Execution.Diagnostics,
            evidence.Handoff,
            evidence.Handoff.Status,
            evidence.Handoff.Validation,
            evidence.Handoff.Diagnostics,
            evidence.Decision,
            evidence.Decision.Status,
            evidence.Decision.IsResolutionEligible,
            evidence.Decision.IsGovernanceBlocked,
            evidence.Decision.Diagnostics,
            evidence.OperationalContext,
            evidence.OperationalContext.Status,
            evidence.OperationalContext.IsReviewEligible,
            evidence.OperationalContext.IsPromotionEligible,
            evidence.OperationalContext.IsCommitEligible,
            evidence.OperationalContext.Diagnostics,
            evidence.Git,
            evidence.Git.CommitStatus,
            evidence.Git.PushStatus,
            evidence.Git.HasPendingChanges,
            evidence.Git.HasUnpushedChanges,
            evidence.Git.Completion,
            evidence.Git.Diagnostics,
            stateMachine.CandidateStages,
            stateMachine.ValidTransitions,
            stateMachine.BlockedTransitions,
            timeline,
            [],
            [],
            [],
            new WorkflowGateDiagnostics(repository.Id, choice.Gate, [], [], [], [], [], []),
            diagnostics);

        WorkflowGateCatalogProjection gates = WorkflowGateCatalogService.BuildProjection(preliminary);

        return preliminary with
        {
            OpenGates = gates.OpenGates,
            SatisfiedGates = gates.SatisfiedGates,
            GateHistory = gates.GateHistory,
            GateDiagnostics = gates.Diagnostics
        };
    }

    public async Task<WorkflowProjectionDiagnostics> GetDiagnosticsAsync(Guid repositoryId) =>
        (await ProjectAsync(repositoryId)).Diagnostics;

    public async Task<IReadOnlyList<WorkflowTimelineEntry>> GetTimelineAsync(Guid repositoryId) =>
        (await ProjectAsync(repositoryId)).Timeline;

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(candidate => candidate.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<ProjectionEvidence> LoadEvidenceAsync(Repository repository)
    {
        WorkflowExecutionProjection execution = await workflowExecutionService.ProjectExecutionAsync(repository.Id);
        WorkflowHandoffProjection handoff = await workflowHandoffService.ProjectHandoffAsync(repository.Id);
        WorkflowDecisionProjection decision = await workflowDecisionService.ProjectDecisionAsync(repository.Id);
        WorkflowOperationalContextProjection operationalContext = await workflowOperationalContextService.ProjectOperationalContextAsync(repository.Id, decision, execution);
        WorkflowGitProjection git = await workflowGitService.ProjectGitAsync(repository.Id, execution, operationalContext);

        return new ProjectionEvidence(repository, execution, handoff, decision, operationalContext, git);
    }

    private static ProjectionChoice ChooseProjection(ProjectionEvidence evidence)
    {
        List<string> reasoning = [];
        List<string> unknownStates = [];
        List<string> conflicts = [];

        WorkflowExecutionProjection execution = evidence.Execution;
        WorkflowExecutionStatus executionStatus = execution.Status;

        conflicts.AddRange(execution.Diagnostics.Conflicts);
        conflicts.AddRange(evidence.Handoff.Diagnostics.Conflicts);
        conflicts.AddRange(evidence.Decision.Diagnostics.Conflicts);
        conflicts.AddRange(evidence.OperationalContext.Diagnostics.Conflicts);
        conflicts.AddRange(evidence.Git.Diagnostics.Conflicts);

        if (executionStatus is WorkflowExecutionStatus.Failed)
        {
            reasoning.Add("Execution projection reports a failed state.");
            return Choice(WorkflowStage.Failed, WorkflowProgressState.Failed, WorkflowGateType.None, "Review the failed execution session.", reasoning, unknownStates, conflicts);
        }

        if (executionStatus is WorkflowExecutionStatus.Cancelled)
        {
            reasoning.Add("Execution projection reports a cancelled state.");
            return Choice(WorkflowStage.Blocked, WorkflowProgressState.Blocked, WorkflowGateType.WorkSelection, "Select the next work item to execute.", reasoning, unknownStates, conflicts);
        }

        if (evidence.Handoff.Status is WorkflowHandoffStatus.Invalid)
        {
            reasoning.Add("Authoritative handoff evidence is invalid.");
            return Choice(WorkflowStage.Handoff, WorkflowProgressState.Blocked, WorkflowGateType.ExecutionAcceptance, "Review the invalid execution handoff through the existing execution handoff workflow.", reasoning, unknownStates, conflicts);
        }

        if (evidence.Handoff.Status is WorkflowHandoffStatus.Rejected)
        {
            reasoning.Add("Execution handoff was rejected.");
            return Choice(WorkflowStage.Blocked, WorkflowProgressState.Blocked, WorkflowGateType.WorkSelection, "Select the next work item to execute.", reasoning, unknownStates, conflicts);
        }

        if (executionStatus is WorkflowExecutionStatus.AwaitingAcceptance)
        {
            reasoning.Add("Execution completed and is awaiting handoff acceptance.");
            return Choice(WorkflowStage.Handoff, WorkflowProgressState.AwaitingGate, WorkflowGateType.ExecutionAcceptance, "Review and accept or reject the execution handoff.", reasoning, unknownStates, conflicts);
        }

        if (executionStatus is WorkflowExecutionStatus.Running)
        {
            reasoning.Add("Execution is active.");
            return Choice(WorkflowStage.Execution, WorkflowProgressState.Active, WorkflowGateType.None, "Wait for execution to complete.", reasoning, unknownStates, conflicts);
        }

        if (executionStatus is WorkflowExecutionStatus.NotStarted)
        {
            reasoning.Add("No execution session evidence exists.");
            return Choice(WorkflowStage.WorkSelection, WorkflowProgressState.WaitingForHuman, WorkflowGateType.WorkSelection, "Select work to execute.", reasoning, unknownStates, conflicts);
        }

        if (!evidence.Decision.IsResolutionEligible)
        {
            if (evidence.Decision.IsGovernanceBlocked)
            {
                reasoning.Add("Decision governance evidence reports a blocked status.");
                return Choice(WorkflowStage.Decision, WorkflowProgressState.Blocked, WorkflowGateType.DecisionResolution, "Resolve decision governance blockers through the existing decisions workflow.", reasoning, unknownStates, conflicts);
            }

            reasoning.Add($"Decision workflow status is {evidence.Decision.Status}.");
            return Choice(WorkflowStage.Decision, WorkflowProgressState.AwaitingGate, WorkflowGateType.DecisionResolution, "Resolve outstanding decisions through the existing decisions workflow.", reasoning, unknownStates, conflicts);
        }

        if (!evidence.OperationalContext.IsCommitEligible)
        {
            if (!evidence.OperationalContext.IsReviewEligible)
            {
                reasoning.Add($"Operational-context workflow status is {evidence.OperationalContext.Status}.");
                return Choice(WorkflowStage.OperationalContext, WorkflowProgressState.AwaitingGate, WorkflowGateType.OperationalContextReview, "Review the operational-context proposal through the existing continuity command.", reasoning, unknownStates, conflicts);
            }

            reasoning.Add($"Operational-context workflow status is {evidence.OperationalContext.Status}.");
            return Choice(WorkflowStage.OperationalContext, WorkflowProgressState.AwaitingGate, WorkflowGateType.OperationalContextPromotion, "Promote the accepted operational-context proposal through the existing continuity command.", reasoning, unknownStates, conflicts);
        }

        if (evidence.Git.Completion.IsComplete)
        {
            reasoning.Add(evidence.Git.Completion.CompletionReason);
            return Choice(WorkflowStage.Completed, WorkflowProgressState.Completed, WorkflowGateType.WorkSelection, "Select the next work item.", reasoning, unknownStates, conflicts);
        }

        if (evidence.Git.IsPushRequired)
        {
            reasoning.Add("Git workflow projection reports changes awaiting push.");
            return Choice(WorkflowStage.Push, WorkflowProgressState.AwaitingGate, WorkflowGateType.PushApproval, "Review and execute push through the existing git push command.", reasoning, unknownStates, conflicts);
        }

        if (evidence.Git.IsCommitRequired)
        {
            reasoning.Add("Git workflow projection reports changes awaiting commit.");
            return Choice(WorkflowStage.Commit, WorkflowProgressState.AwaitingGate, WorkflowGateType.CommitApproval, "Review and execute commit through the existing git commit command.", reasoning, unknownStates, conflicts);
        }

        if (evidence.Git.HasPendingChanges)
        {
            reasoning.Add("Repository has uncommitted git changes without an awaiting-commit execution state.");
            return Choice(WorkflowStage.Commit, WorkflowProgressState.AwaitingGate, WorkflowGateType.CommitApproval, "Review repository changes before commit.", reasoning, unknownStates, conflicts);
        }

        reasoning.Add("Known evidence does not indicate active or pending workflow work.");
        return Choice(WorkflowStage.WorkSelection, WorkflowProgressState.WaitingForHuman, WorkflowGateType.WorkSelection, "Select work to execute.", reasoning, unknownStates, conflicts);
    }

    private static ProjectionChoice Choice(
        WorkflowStage stage,
        WorkflowProgressState progressState,
        WorkflowGateType gate,
        string requiredHumanAction,
        IReadOnlyList<string> reasoning,
        IReadOnlyList<string> unknownStates,
        IReadOnlyList<string> conflicts) =>
        new(stage, progressState, gate, requiredHumanAction, reasoning, unknownStates, conflicts);

    private static IReadOnlyList<string> BuildInputs(ProjectionEvidence evidence)
    {
        List<string> inputs =
        [
            $"repository:{evidence.Repository.Id}",
            $"execution:{evidence.Execution.ExecutionId?.ToString() ?? "none"}:{evidence.Execution.Status}:handoff={evidence.Execution.HasHandoff}:changes={evidence.Execution.HasChanges}",
            $"handoff:{evidence.Handoff.ExecutionId?.ToString() ?? "none"}:{evidence.Handoff.Status}:path={evidence.Handoff.HandoffPath ?? "none"}:valid={evidence.Handoff.Validation.IsValid}",
            $"decision:{evidence.Decision.DecisionId ?? "none"}:{evidence.Decision.Status}:eligible={evidence.Decision.IsResolutionEligible}:governanceBlocked={evidence.Decision.IsGovernanceBlocked}",
            $"operational-context:{evidence.OperationalContext.ProposalId ?? "none"}:{evidence.OperationalContext.Status}:reviewEligible={evidence.OperationalContext.IsReviewEligible}:promotionEligible={evidence.OperationalContext.IsPromotionEligible}:commitEligible={evidence.OperationalContext.IsCommitEligible}",
            $"git:{evidence.Git.Branch}:commit={evidence.Git.CommitStatus}:push={evidence.Git.PushStatus}:pending={evidence.Git.HasPendingChanges}:unpushed={evidence.Git.HasUnpushedChanges}:complete={evidence.Git.Completion.IsComplete}"
        ];

        return inputs;
    }

    private static IReadOnlyList<WorkflowTimelineEntry> BuildTimeline(ProjectionEvidence evidence)
    {
        List<WorkflowTimelineEntry> entries = [];
        WorkflowExecutionProjection execution = evidence.Execution;

        if (execution.StartedAt is DateTimeOffset startedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.ExecutionStarted,
                WorkflowStage.Execution,
                startedAt,
                "Execution session started.",
                "execution",
                execution.ExecutionId?.ToString() ?? evidence.Repository.Id.ToString()));
        }

        if (execution.CompletedAt is DateTimeOffset completedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.ExecutionCompleted,
                WorkflowStage.Handoff,
                completedAt,
                "Execution session completed.",
                "execution",
                execution.ExecutionId?.ToString() ?? evidence.Repository.Id.ToString()));
        }

        if (evidence.Handoff.CreatedAt is DateTimeOffset handoffCreatedAt &&
            evidence.Handoff.HandoffPath is not null &&
            evidence.Handoff.Status is not WorkflowHandoffStatus.Missing)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.HandoffCreated,
                WorkflowStage.Handoff,
                handoffCreatedAt,
                $"Execution handoff created at {evidence.Handoff.HandoffPath}.",
                "execution",
                evidence.Handoff.HandoffId ?? evidence.Handoff.HandoffPath));
        }

        if (evidence.Handoff.Status is WorkflowHandoffStatus.Pending or WorkflowHandoffStatus.Accepted or WorkflowHandoffStatus.Rejected &&
            evidence.Handoff.CreatedAt is DateTimeOffset handoffValidatedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.HandoffValidated,
                WorkflowStage.Handoff,
                handoffValidatedAt,
                "Execution handoff validated by Execution-owned evidence.",
                "execution",
                evidence.Handoff.HandoffId ?? evidence.Handoff.HandoffPath ?? execution.ExecutionId?.ToString() ?? evidence.Repository.Id.ToString()));
        }

        if (evidence.Handoff.Status is WorkflowHandoffStatus.Invalid &&
            evidence.Handoff.CreatedAt is DateTimeOffset handoffInvalidAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.HandoffInvalid,
                WorkflowStage.Handoff,
                handoffInvalidAt,
                $"Execution handoff invalid: {string.Join("; ", evidence.Handoff.Validation.Failures)}",
                "execution",
                evidence.Handoff.HandoffId ?? evidence.Handoff.HandoffPath ?? execution.ExecutionId?.ToString() ?? evidence.Repository.Id.ToString()));
        }

        if (execution.FailedAt is DateTimeOffset failedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.ExecutionFailed,
                WorkflowStage.Execution,
                failedAt,
                $"Execution session failed: {execution.FailureReason ?? "Unknown failure."}",
                "execution",
                execution.ExecutionId?.ToString() ?? evidence.Repository.Id.ToString()));
        }

        if (execution.Status is WorkflowExecutionStatus.Cancelled && execution.StartedAt is DateTimeOffset cancelledAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.ExecutionCancelled,
                WorkflowStage.Execution,
                cancelledAt,
                $"Execution session cancelled: {execution.FailureReason ?? "Cancelled."}",
                "execution",
                execution.ExecutionId?.ToString() ?? evidence.Repository.Id.ToString()));
        }

        if (execution.AcceptedAt is DateTimeOffset acceptedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.ExecutionHandoffAccepted,
                WorkflowStage.Handoff,
                acceptedAt,
                "Execution handoff accepted.",
                "execution",
                execution.ExecutionId?.ToString() ?? evidence.Repository.Id.ToString()));
        }

        if (execution.RejectedAt is DateTimeOffset rejectedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.ExecutionHandoffRejected,
                WorkflowStage.Handoff,
                rejectedAt,
                "Execution handoff rejected.",
                "execution",
                execution.ExecutionId?.ToString() ?? evidence.Repository.Id.ToString()));
        }

        WorkflowDecisionProjection decision = evidence.Decision;
        if (decision.CreatedAt is DateTimeOffset decisionCreatedAt && decision.DecisionId is not null)
        {
            WorkflowTimelineEventType eventType = decision.Status switch
            {
                WorkflowDecisionStatus.Discovered => WorkflowTimelineEventType.DecisionDiscovered,
                WorkflowDecisionStatus.Generated => WorkflowTimelineEventType.DecisionGenerated,
                WorkflowDecisionStatus.UnderReview => WorkflowTimelineEventType.DecisionReviewed,
                WorkflowDecisionStatus.AwaitingResolution => WorkflowTimelineEventType.DecisionRefined,
                WorkflowDecisionStatus.Archived => WorkflowTimelineEventType.DecisionArchived,
                WorkflowDecisionStatus.Superseded => WorkflowTimelineEventType.DecisionSuperseded,
                _ => WorkflowTimelineEventType.DecisionGenerated
            };

            if (decision.Status is not WorkflowDecisionStatus.Missing and not WorkflowDecisionStatus.Resolved)
            {
                entries.Add(CreateTimelineEntry(
                    eventType,
                    WorkflowStage.Decision,
                    decisionCreatedAt,
                    $"Decision {decision.DecisionId} projected as {decision.Status}.",
                    "decisions",
                    decision.DecisionId));
            }
        }

        if (decision.ResolvedAt is DateTimeOffset decisionResolvedAt && decision.DecisionId is not null)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.DecisionResolved,
                WorkflowStage.Decision,
                decisionResolvedAt,
                $"Decision {decision.DecisionId} resolved.",
                "decisions",
                decision.DecisionId));
        }

        WorkflowOperationalContextProjection operationalContext = evidence.OperationalContext;
        if (operationalContext.CreatedAt is DateTimeOffset contextCreatedAt &&
            operationalContext.ProposalId is not null &&
            operationalContext.Status is not WorkflowOperationalContextStatus.NoContextRequired)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.OperationalContextProposed,
                WorkflowStage.OperationalContext,
                contextCreatedAt,
                $"Operational-context proposal {operationalContext.ProposalId} projected as {operationalContext.Status}.",
                "continuity",
                operationalContext.ProposalId));
        }

        if (operationalContext.ReviewedAt is DateTimeOffset contextReviewedAt &&
            operationalContext.ProposalId is not null)
        {
            WorkflowTimelineEventType eventType = operationalContext.Status switch
            {
                WorkflowOperationalContextStatus.Edited => WorkflowTimelineEventType.OperationalContextEdited,
                WorkflowOperationalContextStatus.Rejected => WorkflowTimelineEventType.OperationalContextRejected,
                WorkflowOperationalContextStatus.Archived => WorkflowTimelineEventType.OperationalContextArchived,
                WorkflowOperationalContextStatus.Promoted or WorkflowOperationalContextStatus.ReadyForPromotion => WorkflowTimelineEventType.OperationalContextAccepted,
                _ => WorkflowTimelineEventType.OperationalContextReviewed
            };

            entries.Add(CreateTimelineEntry(
                eventType,
                WorkflowStage.OperationalContext,
                contextReviewedAt,
                $"Operational-context proposal {operationalContext.ProposalId} reviewed as {operationalContext.Status}.",
                "continuity",
                operationalContext.ProposalId));
        }

        if (operationalContext.PromotedAt is DateTimeOffset contextPromotedAt &&
            operationalContext.ProposalId is not null)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.OperationalContextPromoted,
                WorkflowStage.OperationalContext,
                contextPromotedAt,
                $"Operational-context proposal {operationalContext.ProposalId} promoted.",
                "continuity",
                operationalContext.ProposalId));
        }

        if (evidence.Git.CommitStatus is WorkflowGitStatus.AwaitingCommit &&
            execution.AcceptedAt is DateTimeOffset commitPreparedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.CommitPrepared,
                WorkflowStage.Commit,
                commitPreparedAt,
                "Execution changes are ready for commit review.",
                "git",
                execution.ExecutionId?.ToString() ?? evidence.Repository.Id.ToString()));
        }

        if (execution.CommittedAt is DateTimeOffset committedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.CommitExecuted,
                WorkflowStage.Commit,
                committedAt,
                "Execution changes committed.",
                "git",
                execution.CommitSha ?? execution.ExecutionId?.ToString() ?? evidence.Repository.Id.ToString()));
        }

        if (evidence.Git.PushStatus is WorkflowGitStatus.AwaitingPush &&
            execution.CommittedAt is DateTimeOffset pushPreparedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.PushApproved,
                WorkflowStage.Push,
                pushPreparedAt,
                "Execution commit is ready for push review.",
                "git",
                execution.CommitSha ?? execution.ExecutionId?.ToString() ?? evidence.Repository.Id.ToString()));
        }

        if (execution.PushedAt is DateTimeOffset pushedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.PushExecuted,
                WorkflowStage.Push,
                pushedAt,
                "Execution commit pushed.",
                "git",
                execution.PushedCommitSha ?? execution.CommitSha ?? execution.ExecutionId?.ToString() ?? evidence.Repository.Id.ToString()));
        }

        return entries
            .OrderBy(entry => entry.OccurredAt)
            .ThenBy(entry => entry.EventType)
            .ThenBy(entry => entry.SourceArtifact, StringComparer.Ordinal)
            .ToArray();
    }

    private static WorkflowTimelineEntry CreateTimelineEntry(
        WorkflowTimelineEventType eventType,
        WorkflowStage stage,
        DateTimeOffset occurredAt,
        string summary,
        string sourceDomain,
        string sourceArtifact) =>
        new(
            eventType,
            stage,
            occurredAt,
            summary,
            sourceDomain,
            sourceArtifact,
            WorkflowFingerprint.ForEntry(eventType, stage, occurredAt, summary, sourceDomain, sourceArtifact).Value);

    private sealed record ProjectionEvidence(
        Repository Repository,
        WorkflowExecutionProjection Execution,
        WorkflowHandoffProjection Handoff,
        WorkflowDecisionProjection Decision,
        WorkflowOperationalContextProjection OperationalContext,
        WorkflowGitProjection Git);

    private sealed record ProjectionChoice(
        WorkflowStage Stage,
        WorkflowProgressState ProgressState,
        WorkflowGateType Gate,
        string RequiredHumanAction,
        IReadOnlyList<string> Reasoning,
        IReadOnlyList<string> UnknownStates,
        IReadOnlyList<string> Conflicts);
}
