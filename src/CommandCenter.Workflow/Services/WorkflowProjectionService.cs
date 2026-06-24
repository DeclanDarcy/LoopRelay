using CommandCenter.Continuity.Abstractions;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowProjectionService(
    IRepositoryService repositoryService,
    IWorkflowExecutionService workflowExecutionService,
    IDecisionRepository decisionRepository,
    IOperationalContextProposalStore operationalContextProposalStore,
    IGitService gitService,
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
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        IReadOnlyList<OperationalContextProposal> proposals = await operationalContextProposalStore.ListAsync(repository);
        RepositoryGitStatus gitStatus = await gitService.GetStatusAsync(repository);

        return new ProjectionEvidence(repository, execution, decisions, proposals, gitStatus);
    }

    private static ProjectionChoice ChooseProjection(ProjectionEvidence evidence)
    {
        List<string> reasoning = [];
        List<string> unknownStates = [];
        List<string> conflicts = [];

        WorkflowExecutionProjection execution = evidence.Execution;
        WorkflowExecutionStatus executionStatus = execution.Status;

        conflicts.AddRange(execution.Diagnostics.Conflicts);

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

        if (execution.PushedAt is not null)
        {
            reasoning.Add("Latest execution session has push evidence.");
            return Choice(WorkflowStage.Completed, WorkflowProgressState.Completed, WorkflowGateType.WorkSelection, "Select the next work item.", reasoning, unknownStates, conflicts);
        }

        if (execution.CommittedAt is not null)
        {
            reasoning.Add("Commit evidence exists and push evidence is absent.");
            return Choice(WorkflowStage.Push, WorkflowProgressState.AwaitingGate, WorkflowGateType.PushApproval, "Review and execute push through the existing git push command.", reasoning, unknownStates, conflicts);
        }

        if (execution.HasChanges)
        {
            reasoning.Add("Execution has been accepted and is awaiting commit.");
            return Choice(WorkflowStage.Commit, WorkflowProgressState.AwaitingGate, WorkflowGateType.CommitApproval, "Review and execute commit through the existing git commit command.", reasoning, unknownStates, conflicts);
        }

        OperationalContextProposal? currentContextProposal = evidence.Proposals
            .Where(proposal => proposal.Status is OperationalContextProposalStatus.Pending or OperationalContextProposalStatus.Edited or OperationalContextProposalStatus.Accepted)
            .OrderByDescending(proposal => proposal.GeneratedAt)
            .FirstOrDefault();

        if (currentContextProposal is not null)
        {
            if (currentContextProposal.Status is OperationalContextProposalStatus.Pending or OperationalContextProposalStatus.Edited)
            {
                reasoning.Add($"Operational-context proposal {currentContextProposal.ProposalId} is awaiting review.");
                return Choice(WorkflowStage.OperationalContext, WorkflowProgressState.AwaitingGate, WorkflowGateType.OperationalContextReview, "Review the operational-context proposal through the existing continuity command.", reasoning, unknownStates, conflicts);
            }

            reasoning.Add($"Operational-context proposal {currentContextProposal.ProposalId} is accepted and awaiting promotion.");
            return Choice(WorkflowStage.OperationalContext, WorkflowProgressState.AwaitingGate, WorkflowGateType.OperationalContextPromotion, "Promote the accepted operational-context proposal through the existing continuity command.", reasoning, unknownStates, conflicts);
        }

        if (evidence.Decisions.Any(decision => decision.State is DecisionState.Open or DecisionState.UnderReview))
        {
            reasoning.Add("At least one decision is open or under review.");
            return Choice(WorkflowStage.Decision, WorkflowProgressState.AwaitingGate, WorkflowGateType.DecisionResolution, "Resolve outstanding decisions through the existing decisions workflow.", reasoning, unknownStates, conflicts);
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

        if (!evidence.GitStatus.DirtyState.IsClean)
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
            $"decisions:{evidence.Decisions.Count}:{string.Join(",", evidence.Decisions.OrderBy(decision => decision.Id.Value).Select(decision => $"{decision.Id.Value}:{decision.State}"))}",
            $"operational-context-proposals:{evidence.Proposals.Count}:{string.Join(",", evidence.Proposals.OrderBy(proposal => proposal.ProposalId).Select(proposal => $"{proposal.ProposalId}:{proposal.Status}"))}",
            $"git:{evidence.GitStatus.Branch}:ahead={evidence.GitStatus.AheadCount}:behind={evidence.GitStatus.BehindCount}:clean={evidence.GitStatus.DirtyState.IsClean}"
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

        entries.AddRange(evidence.Decisions
            .Where(decision => decision.State is DecisionState.Resolved && decision.Resolution is not null)
            .Select(decision => CreateTimelineEntry(
                WorkflowTimelineEventType.DecisionResolved,
                WorkflowStage.Decision,
                decision.Resolution!.ResolvedAt,
                $"Decision {decision.Id.Value} resolved.",
                "decisions",
                decision.Id.Value)));

        entries.AddRange(evidence.Proposals
            .Where(proposal => proposal.Review.ReviewedAt is not null &&
                proposal.Status is OperationalContextProposalStatus.Accepted or OperationalContextProposalStatus.Edited or OperationalContextProposalStatus.Rejected or OperationalContextProposalStatus.Promoted)
            .Select(proposal => CreateTimelineEntry(
                WorkflowTimelineEventType.OperationalContextReviewed,
                WorkflowStage.OperationalContext,
                proposal.Review.ReviewedAt!.Value,
                $"Operational-context proposal {proposal.ProposalId} reviewed.",
                "continuity",
                proposal.ProposalId)));

        entries.AddRange(evidence.Proposals
            .Where(proposal => proposal.Status is OperationalContextProposalStatus.Promoted && proposal.Promotion.PromotedAt is not null)
            .Select(proposal => CreateTimelineEntry(
                WorkflowTimelineEventType.OperationalContextPromoted,
                WorkflowStage.OperationalContext,
                proposal.Promotion.PromotedAt!.Value,
                $"Operational-context proposal {proposal.ProposalId} promoted.",
                "continuity",
                proposal.ProposalId)));

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
        IReadOnlyList<Decision> Decisions,
        IReadOnlyList<OperationalContextProposal> Proposals,
        RepositoryGitStatus GitStatus);

    private sealed record ProjectionChoice(
        WorkflowStage Stage,
        WorkflowProgressState ProgressState,
        WorkflowGateType Gate,
        string RequiredHumanAction,
        IReadOnlyList<string> Reasoning,
        IReadOnlyList<string> UnknownStates,
        IReadOnlyList<string> Conflicts);
}
