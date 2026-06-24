using CommandCenter.Continuity.Abstractions;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowProjectionService(
    IRepositoryService repositoryService,
    IExecutionSessionService executionSessionService,
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
        RepositoryExecutionState executionState = await executionSessionService.GetRepositoryStateAsync(repository.Id);
        ExecutionSessionSummary? session = await executionSessionService.GetRepositorySessionSummaryAsync(repository.Id);
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        IReadOnlyList<OperationalContextProposal> proposals = await operationalContextProposalStore.ListAsync(repository);
        RepositoryGitStatus gitStatus = await gitService.GetStatusAsync(repository);

        return new ProjectionEvidence(repository, executionState, session, decisions, proposals, gitStatus);
    }

    private static ProjectionChoice ChooseProjection(ProjectionEvidence evidence)
    {
        List<string> reasoning = [];
        List<string> unknownStates = [];
        List<string> conflicts = [];

        ExecutionSessionSummary? session = evidence.Session;
        RepositoryExecutionState executionState = evidence.ExecutionState;

        if (session is not null && session.RepositoryState != executionState)
        {
            conflicts.Add($"Execution repository state is {executionState}, but latest session summary reports {session.RepositoryState}.");
        }

        if (executionState is RepositoryExecutionState.Failed ||
            session?.State is ExecutionSessionState.Failed)
        {
            reasoning.Add("Execution evidence reports a failed state.");
            return Choice(WorkflowStage.Failed, WorkflowProgressState.Failed, WorkflowGateType.None, "Review the failed execution session.", reasoning, unknownStates, conflicts);
        }

        if (executionState is RepositoryExecutionState.Cancelled ||
            session?.State is ExecutionSessionState.Cancelled)
        {
            reasoning.Add("Execution evidence reports a cancelled state.");
            return Choice(WorkflowStage.Blocked, WorkflowProgressState.Blocked, WorkflowGateType.WorkSelection, "Select the next work item to execute.", reasoning, unknownStates, conflicts);
        }

        if (session?.PushedAt is not null)
        {
            reasoning.Add("Latest execution session has push evidence.");
            return Choice(WorkflowStage.Completed, WorkflowProgressState.Completed, WorkflowGateType.WorkSelection, "Select the next work item.", reasoning, unknownStates, conflicts);
        }

        if (session?.CommittedAt is not null || executionState is RepositoryExecutionState.AwaitingPush)
        {
            reasoning.Add("Commit evidence exists and push evidence is absent.");
            return Choice(WorkflowStage.Push, WorkflowProgressState.AwaitingGate, WorkflowGateType.PushApproval, "Review and execute push through the existing git push command.", reasoning, unknownStates, conflicts);
        }

        if (executionState is RepositoryExecutionState.AwaitingCommit)
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

        if (executionState is RepositoryExecutionState.AwaitingAcceptance)
        {
            reasoning.Add("Execution completed and is awaiting handoff acceptance.");
            return Choice(WorkflowStage.Handoff, WorkflowProgressState.AwaitingGate, WorkflowGateType.ExecutionAcceptance, "Review and accept or reject the execution handoff.", reasoning, unknownStates, conflicts);
        }

        if (executionState is RepositoryExecutionState.Executing ||
            session?.State is ExecutionSessionState.Executing or ExecutionSessionState.Created)
        {
            reasoning.Add("Execution is active.");
            return Choice(WorkflowStage.Execution, WorkflowProgressState.Active, WorkflowGateType.None, "Wait for execution to complete.", reasoning, unknownStates, conflicts);
        }

        if (session is null)
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
            $"execution-state:{evidence.ExecutionState}",
            evidence.Session is null ? "execution-session:none" : $"execution-session:{evidence.Session.SessionId}:{evidence.Session.State}:{evidence.Session.RepositoryState}",
            $"decisions:{evidence.Decisions.Count}:{string.Join(",", evidence.Decisions.OrderBy(decision => decision.Id.Value).Select(decision => $"{decision.Id.Value}:{decision.State}"))}",
            $"operational-context-proposals:{evidence.Proposals.Count}:{string.Join(",", evidence.Proposals.OrderBy(proposal => proposal.ProposalId).Select(proposal => $"{proposal.ProposalId}:{proposal.Status}"))}",
            $"git:{evidence.GitStatus.Branch}:ahead={evidence.GitStatus.AheadCount}:behind={evidence.GitStatus.BehindCount}:clean={evidence.GitStatus.DirtyState.IsClean}"
        ];

        return inputs;
    }

    private static IReadOnlyList<WorkflowTimelineEntry> BuildTimeline(ProjectionEvidence evidence)
    {
        List<WorkflowTimelineEntry> entries = [];
        ExecutionSessionSummary? session = evidence.Session;

        if (session?.StartedAt is DateTimeOffset startedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.ExecutionStarted,
                WorkflowStage.Execution,
                startedAt,
                "Execution session started.",
                "execution",
                session.SessionId.ToString()));
        }

        if (session?.CompletedAt is DateTimeOffset completedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.ExecutionCompleted,
                WorkflowStage.Handoff,
                completedAt,
                "Execution session completed.",
                "execution",
                session.SessionId.ToString()));
        }

        if (session?.AcceptedAt is DateTimeOffset acceptedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.ExecutionHandoffAccepted,
                WorkflowStage.Handoff,
                acceptedAt,
                "Execution handoff accepted.",
                "execution",
                session.SessionId.ToString()));
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

        if (session?.CommittedAt is DateTimeOffset committedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.CommitExecuted,
                WorkflowStage.Commit,
                committedAt,
                "Execution changes committed.",
                "git",
                session.CommitSha ?? session.SessionId.ToString()));
        }

        if (session?.PushedAt is DateTimeOffset pushedAt)
        {
            entries.Add(CreateTimelineEntry(
                WorkflowTimelineEventType.PushExecuted,
                WorkflowStage.Push,
                pushedAt,
                "Execution commit pushed.",
                "git",
                session.PushedCommitSha ?? session.CommitSha ?? session.SessionId.ToString()));
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
        RepositoryExecutionState ExecutionState,
        ExecutionSessionSummary? Session,
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
