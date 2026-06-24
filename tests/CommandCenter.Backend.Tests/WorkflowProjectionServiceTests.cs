using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Core.Artifacts;
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
using CommandCenter.Workflow.Persistence;
using CommandCenter.Workflow.Primitives;
using CommandCenter.Workflow.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CommandCenter.Backend.Tests;

public sealed class WorkflowProjectionServiceTests
{
    [Fact]
    public void StateMachineExposesCanonicalGraph()
    {
        var service = new WorkflowStateMachineService();

        IReadOnlyList<WorkflowTransition> graph = service.GetCanonicalGraph();

        Assert.Equal(
            [
                WorkflowStage.WorkSelection,
                WorkflowStage.Execution,
                WorkflowStage.Handoff,
                WorkflowStage.Decision,
                WorkflowStage.OperationalContext,
                WorkflowStage.Commit,
                WorkflowStage.Push
            ],
            graph.Select(transition => transition.FromStage).ToArray());
        Assert.Equal(WorkflowStage.Completed, graph.Last().ToStage);
    }

    [Fact]
    public void StateMachineAcceptsValidCanonicalTransition()
    {
        var service = new WorkflowStateMachineService();

        WorkflowTransitionResult result = service.ValidateTransition(
            WorkflowStage.Handoff,
            WorkflowStage.Decision,
            WorkflowProgressState.Ready,
            WorkflowGateType.None,
            "No human action required.");

        Assert.True(result.IsValid);
        Assert.False(result.IsBlocked);
    }

    [Fact]
    public void StateMachineRejectsInvalidTransitionWithExplanation()
    {
        var service = new WorkflowStateMachineService();

        WorkflowTransitionResult result = service.ValidateTransition(
            WorkflowStage.Execution,
            WorkflowStage.Commit,
            WorkflowProgressState.Ready,
            WorkflowGateType.None,
            "No human action required.");

        Assert.False(result.IsValid);
        Assert.True(result.IsBlocked);
        Assert.Equal(WorkflowBlockingCondition.UnknownState, result.BlockingCondition);
        Assert.Contains("No canonical transition", result.Reason);
    }

    [Fact]
    public async Task ExecutionAwaitingAcceptanceMapsToHandoffAcceptanceGate()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingAcceptance;
        fixture.Session = new ExecutionSessionSummary
        {
            SessionId = Guid.NewGuid(),
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.AwaitingAcceptance,
            StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z")
        };

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.Handoff, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.ExecutionAcceptance, projection.BlockingGate);
        Assert.Equal(WorkflowProgressState.AwaitingGate, projection.ProgressState);
        WorkflowGate openGate = Assert.Single(projection.OpenGates);
        Assert.Equal(WorkflowGateType.ExecutionAcceptance, openGate.Type);
        Assert.Equal(WorkflowGateStatus.Open, openGate.Status);
        Assert.Contains("accept_execution_handoff", openGate.SatisfyingCommands);
        Assert.Contains("reject_execution_handoff", openGate.SatisfyingCommands);
        Assert.Equal([WorkflowStage.Decision], projection.NextPossibleStages);
        Assert.Contains(projection.BlockedTransitions, transition => transition.BlockingCondition == WorkflowBlockingCondition.PendingHandoffAcceptance);
        Assert.Contains(projection.Timeline, entry => entry.EventType == WorkflowTimelineEventType.ExecutionCompleted);
        Assert.Contains(projection.Diagnostics.Reasoning, reason => reason.Contains("handoff acceptance", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OpenDecisionMapsToDecisionResolutionGate()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateDecision(fixture.Repository.Id, "DEC-0001", DecisionState.Open));

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.Decision, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.DecisionResolution, projection.BlockingGate);
        Assert.Equal(WorkflowProgressState.AwaitingGate, projection.ProgressState);
        Assert.Contains(projection.BlockedTransitions, transition => transition.BlockingCondition == WorkflowBlockingCondition.UnresolvedDecision);
    }

    [Fact]
    public async Task PendingOperationalContextMapsToContextReviewGate()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Proposals.Add(new OperationalContextProposal
        {
            ProposalId = "ctx-0001",
            RepositoryId = fixture.Repository.Id,
            GeneratedAt = DateTimeOffset.Parse("2026-06-23T11:00:00Z"),
            Status = OperationalContextProposalStatus.Pending
        });

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.OperationalContext, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.OperationalContextReview, projection.BlockingGate);
        WorkflowGate openGate = Assert.Single(projection.OpenGates);
        Assert.Equal(WorkflowGateType.OperationalContextReview, openGate.Type);
        Assert.Contains("accept_operational_context_proposal", openGate.SatisfyingCommands);
        Assert.Contains("edit_operational_context_proposal", openGate.SatisfyingCommands);
        Assert.Contains("reject_operational_context_proposal", openGate.SatisfyingCommands);
        Assert.Contains(projection.BlockedTransitions, transition => transition.BlockingCondition == WorkflowBlockingCondition.PendingContextReview);
    }

    [Fact]
    public async Task AcceptedOperationalContextBlocksCommitUntilPromotion()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Proposals.Add(new OperationalContextProposal
        {
            ProposalId = "ctx-0001",
            RepositoryId = fixture.Repository.Id,
            GeneratedAt = DateTimeOffset.Parse("2026-06-23T11:00:00Z"),
            Status = OperationalContextProposalStatus.Accepted
        });

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.OperationalContext, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.OperationalContextPromotion, projection.BlockingGate);
        Assert.Contains(projection.BlockedTransitions, transition => transition.BlockingCondition == WorkflowBlockingCondition.PendingContextPromotion);
    }

    [Fact]
    public async Task AwaitingCommitMapsToCommitApprovalGate()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.Commit, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.CommitApproval, projection.BlockingGate);
        WorkflowGate openGate = Assert.Single(projection.OpenGates);
        Assert.Equal(WorkflowGateType.CommitApproval, openGate.Type);
        Assert.Equal("commit_execution", openGate.SatisfyingCommand);
        Assert.Contains(projection.BlockedTransitions, transition => transition.BlockingCondition == WorkflowBlockingCondition.PendingCommitApproval);
    }

    [Fact]
    public async Task DirtyGitStateMapsToCommitApprovalGate()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.GitStatus = new RepositoryGitStatus
        {
            Branch = "main",
            DirtyState = new RepositoryDirtyState
            {
                IsClean = false,
                ModifiedPaths = ["src/file.cs"]
            },
            CapturedAt = DateTimeOffset.Parse("2026-06-23T12:00:00Z")
        };

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.Commit, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.CommitApproval, projection.BlockingGate);
    }

    [Fact]
    public async Task AwaitingPushBlocksCompletionUntilPushApproval()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = AwaitingPushCommittedSession();

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.Push, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.PushApproval, projection.BlockingGate);
        Assert.Contains(projection.BlockedTransitions, transition => transition.BlockingCondition == WorkflowBlockingCondition.PendingPushApproval);
    }

    [Fact]
    public async Task PushedSessionMapsToCompletedAndIsDeterministic()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = PushedSession();

        WorkflowInstance first = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);
        WorkflowInstance second = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.Completed, first.CurrentStage);
        Assert.Equal(WorkflowGateType.WorkSelection, first.BlockingGate);
        Assert.Equal(first.CurrentStage, second.CurrentStage);
        Assert.Equal(first.ProgressState, second.ProgressState);
        Assert.Equal(first.BlockingGate, second.BlockingGate);
        Assert.Empty(first.NextPossibleStages);
        Assert.Equal(first.Diagnostics.ProjectionInputs, second.Diagnostics.ProjectionInputs);
        Assert.Equal(first.Timeline, second.Timeline);
        Assert.Contains(first.Timeline, entry => entry.EventType == WorkflowTimelineEventType.CommitExecuted);
        Assert.Contains(first.Timeline, entry => entry.EventType == WorkflowTimelineEventType.PushExecuted);
        Assert.Contains(first.SatisfiedGates, gate => gate.Type == WorkflowGateType.ExecutionAcceptance);
        Assert.Contains(first.SatisfiedGates, gate => gate.Type == WorkflowGateType.CommitApproval);
        Assert.Contains(first.SatisfiedGates, gate => gate.Type == WorkflowGateType.PushApproval);
    }

    [Fact]
    public async Task GateCatalogMapsEveryAuthorityGateToExistingCommandName()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = PushedSession();

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Contains(projection.GateDiagnostics.GateCommandMap, entry => entry == "WorkSelection:explicit_human_work_selection");
        Assert.Contains(projection.GateDiagnostics.GateCommandMap, entry => entry == "ExecutionAcceptance:accept_execution_handoff|reject_execution_handoff");
        Assert.Contains(projection.GateDiagnostics.GateCommandMap, entry => entry == "DecisionResolution:resolve_decision_proposal");
        Assert.Contains(projection.GateDiagnostics.GateCommandMap, entry => entry == "OperationalContextReview:accept_operational_context_proposal|edit_operational_context_proposal|reject_operational_context_proposal");
        Assert.Contains(projection.GateDiagnostics.GateCommandMap, entry => entry == "OperationalContextPromotion:promote_operational_context_proposal");
        Assert.Contains(projection.GateDiagnostics.GateCommandMap, entry => entry == "CommitApproval:commit_execution");
        Assert.Contains(projection.GateDiagnostics.GateCommandMap, entry => entry == "PushApproval:push_execution");
    }

    [Fact]
    public async Task GateCatalogSatisfiedGatesComeFromDomainEvidence()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateResolvedDecision(fixture.Repository.Id, "DEC-0001"));
        fixture.Proposals.Add(new OperationalContextProposal
        {
            ProposalId = "ctx-0001",
            RepositoryId = fixture.Repository.Id,
            GeneratedAt = DateTimeOffset.Parse("2026-06-23T11:00:00Z"),
            Status = OperationalContextProposalStatus.Promoted,
            Review = new OperationalContextReview
            {
                ProposalId = "ctx-0001",
                ReviewedAt = DateTimeOffset.Parse("2026-06-23T11:10:00Z")
            },
            Promotion = new OperationalContextPromotion
            {
                ProposalId = "ctx-0001",
                PromotedAt = DateTimeOffset.Parse("2026-06-23T11:20:00Z")
            }
        });

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Contains(projection.SatisfiedGates, gate => gate is { Type: WorkflowGateType.ExecutionAcceptance, SourceDomain: "execution" });
        Assert.Contains(projection.SatisfiedGates, gate => gate is { Type: WorkflowGateType.DecisionResolution, SourceDomain: "decisions" });
        Assert.Contains(projection.SatisfiedGates, gate => gate is { Type: WorkflowGateType.OperationalContextReview, SourceDomain: "continuity" });
        Assert.Contains(projection.SatisfiedGates, gate => gate is { Type: WorkflowGateType.OperationalContextPromotion, SourceDomain: "continuity" });
        Assert.All(projection.SatisfiedGates, gate => Assert.Equal(WorkflowGateStatus.Satisfied, gate.Status));
    }

    [Fact]
    public async Task WorkflowEndpointReturnsProjection()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Ready;
        await using WebApplication app = Program.CreateApp(
            [],
            services => fixture.ReplaceServices(services));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow");
        WorkflowInstance? projection = await response.Content.ReadFromJsonAsync<WorkflowInstance>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(projection);
        Assert.Equal(WorkflowStage.WorkSelection, projection.CurrentStage);
    }

    [Fact]
    public async Task WorkflowTransitionsEndpointReturnsStateMachineDiagnostics()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);
        await using WebApplication app = Program.CreateApp(
            [],
            services => fixture.ReplaceServices(services));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/transitions");
        WorkflowStateMachineDiagnostics? diagnostics = await response.Content.ReadFromJsonAsync<WorkflowStateMachineDiagnostics>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(diagnostics);
        Assert.Equal(WorkflowStage.Commit, diagnostics.CurrentStage);
        Assert.Contains(diagnostics.BlockedTransitions, transition => transition.BlockingCondition == WorkflowBlockingCondition.PendingCommitApproval);
    }

    [Fact]
    public async Task WorkflowGatesEndpointReturnsGateCatalogProjection()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = AwaitingPushCommittedSession();
        await using WebApplication app = Program.CreateApp(
            [],
            services => fixture.ReplaceServices(services));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/gates");
        WorkflowGateCatalogProjection? gates = await response.Content.ReadFromJsonAsync<WorkflowGateCatalogProjection>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(gates);
        WorkflowGate openGate = Assert.Single(gates.OpenGates);
        Assert.Equal(WorkflowGateType.PushApproval, openGate.Type);
        Assert.Equal("push_execution", openGate.SatisfyingCommand);
        Assert.Contains(gates.Diagnostics.Reasoning, reason => reason.Contains("Open gate PushApproval", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WorkflowRepositorySavesLoadsListsAndFindsLatestTimeline()
    {
        TestFixture fixture = TestFixture.Create();
        var store = new MemoryArtifactStore();
        var repository = new FileSystemWorkflowRepository(store);
        WorkflowTimeline older = CreateTimeline(fixture.Repository.Id, DateTimeOffset.Parse("2026-06-23T10:00:00Z"), WorkflowStage.Execution);
        WorkflowTimeline newer = CreateTimeline(fixture.Repository.Id, DateTimeOffset.Parse("2026-06-23T11:00:00Z"), WorkflowStage.Handoff);

        await repository.SaveTimelineAsync(fixture.Repository, newer);
        await repository.SaveTimelineAsync(fixture.Repository, older);

        WorkflowTimeline? loaded = await repository.LoadTimelineAsync(fixture.Repository, "workflow.20260623T110000.0000000Z");
        IReadOnlyList<WorkflowTimeline> listed = await repository.ListTimelinesAsync(fixture.Repository);
        WorkflowTimeline? latest = await repository.GetLatestTimelineAsync(fixture.Repository);

        Assert.NotNull(loaded);
        Assert.Equal(newer.Fingerprint, loaded.Fingerprint);
        Assert.Equal([older.GeneratedAt, newer.GeneratedAt], listed.Select(timeline => timeline.GeneratedAt).ToArray());
        Assert.NotNull(latest);
        Assert.Equal(newer.Fingerprint, latest.Fingerprint);
        await repository.SaveReportAsync(fixture.Repository, "repository.20260623T110000Z", "{\"ok\":true}", "# Report");
        Assert.True(await store.ExistsAsync(WorkflowArtifactPaths.Resolve(fixture.Repository, WorkflowArtifactPaths.TimelineMarkdown("workflow.20260623T110000.0000000Z"))));
        Assert.True(await store.ExistsAsync(WorkflowArtifactPaths.Resolve(fixture.Repository, WorkflowArtifactPaths.ReportJson("repository.20260623T110000Z"))));
        Assert.True(await store.ExistsAsync(WorkflowArtifactPaths.Resolve(fixture.Repository, WorkflowArtifactPaths.ReportMarkdown("repository.20260623T110000Z"))));
    }

    [Fact]
    public async Task RecoveryRebuildsMissingWorkflowArtifactsFromDomainProjection()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingAcceptance;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingAcceptance);
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var recovery = fixture.CreateRecoveryService(workflowRepository);

        WorkflowRecoveryResult result = await recovery.RecoverCurrentWorkflowAsync(fixture.Repository.Id);
        WorkflowTimeline? latest = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.True(result.Diagnostics.Rebuilt);
        Assert.Equal(WorkflowStage.Handoff, result.Timeline.CurrentStage);
        Assert.NotNull(latest);
        Assert.Equal(result.Timeline.Fingerprint, latest.Fingerprint);
    }

    [Fact]
    public async Task RecoveryRebuildsCorruptWorkflowArtifactsFromDomainProjection()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);
        var store = new MemoryArtifactStore();
        string corruptPath = WorkflowArtifactPaths.Resolve(
            fixture.Repository,
            WorkflowArtifactPaths.TimelineJson("workflow.20260623T100000.0000000Z"));
        await store.WriteAsync(corruptPath, "{ not valid json");
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var recovery = fixture.CreateRecoveryService(workflowRepository);

        WorkflowRecoveryResult result = await recovery.RecoverCurrentWorkflowAsync(fixture.Repository.Id);

        Assert.True(result.Diagnostics.Rebuilt);
        Assert.Contains(result.Diagnostics.Diagnostics, diagnostic => diagnostic.Contains("could not be loaded", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(WorkflowStage.Commit, result.Timeline.CurrentStage);
    }

    [Fact]
    public async Task WorkflowFingerprintsAreStableAndDetectDivergence()
    {
        WorkflowTimeline first = CreateTimeline(Guid.Parse("11111111-1111-1111-1111-111111111111"), DateTimeOffset.Parse("2026-06-23T10:00:00Z"), WorkflowStage.Commit);
        WorkflowTimeline second = CreateTimeline(Guid.Parse("11111111-1111-1111-1111-111111111111"), DateTimeOffset.Parse("2026-06-23T11:00:00Z"), WorkflowStage.Commit);
        WorkflowTimeline diverged = CreateTimeline(Guid.Parse("11111111-1111-1111-1111-111111111111"), DateTimeOffset.Parse("2026-06-23T10:00:00Z"), WorkflowStage.Push);

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.NotEqual(first.Fingerprint, diverged.Fingerprint);
    }

    [Fact]
    public async Task DeletingWorkflowArtifactsDoesNotChangeDomainProjection()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = AwaitingPushCommittedSession();
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var recovery = fixture.CreateRecoveryService(workflowRepository);
        WorkflowInstance before = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);
        WorkflowRecoveryResult recovered = await recovery.RecoverCurrentWorkflowAsync(fixture.Repository.Id);
        string timelineId = WorkflowArtifactPaths.TimelineId(recovered.Timeline.GeneratedAt);

        await store.DeleteAsync(WorkflowArtifactPaths.Resolve(fixture.Repository, WorkflowArtifactPaths.TimelineJson(timelineId)));
        await store.DeleteAsync(WorkflowArtifactPaths.Resolve(fixture.Repository, WorkflowArtifactPaths.TimelineMarkdown(timelineId)));
        WorkflowInstance after = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(before.CurrentStage, after.CurrentStage);
        Assert.Equal(before.BlockingGate, after.BlockingGate);
        Assert.Equal(before.ValidTransitions, after.ValidTransitions);
    }

    [Fact]
    public async Task StartupRecoveryRestoresWorkflowEvidenceWithoutDomainMutation()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingAcceptance;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingAcceptance);
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var hosted = new WorkflowRecoveryHostedService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateRecoveryService(workflowRepository));

        await hosted.StartAsync(CancellationToken.None);

        WorkflowTimeline? latest = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);
        Assert.NotNull(latest);
        Assert.Equal(WorkflowStage.Handoff, latest.CurrentStage);
    }

    private static ExecutionSessionSummary CompletedAcceptedSession(
        RepositoryExecutionState repositoryState = RepositoryExecutionState.Accepted) => new()
    {
        SessionId = Guid.NewGuid(),
        State = ExecutionSessionState.Completed,
        RepositoryState = repositoryState,
        StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
        CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
        AcceptedAt = DateTimeOffset.Parse("2026-06-23T10:15:00Z")
    };

    private static ExecutionSessionSummary AwaitingPushCommittedSession() => new()
    {
        SessionId = Guid.NewGuid(),
        State = ExecutionSessionState.Completed,
        RepositoryState = RepositoryExecutionState.AwaitingPush,
        StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
        CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
        AcceptedAt = DateTimeOffset.Parse("2026-06-23T10:15:00Z"),
        CommittedAt = DateTimeOffset.Parse("2026-06-23T12:00:00Z"),
        CommitSha = "abc123"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static ExecutionSessionSummary PushedSession() => new()
    {
        SessionId = Guid.NewGuid(),
        State = ExecutionSessionState.Completed,
        RepositoryState = RepositoryExecutionState.AwaitingPush,
        StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
        CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
        AcceptedAt = DateTimeOffset.Parse("2026-06-23T10:15:00Z"),
        CommittedAt = DateTimeOffset.Parse("2026-06-23T12:00:00Z"),
        CommitSha = "abc123",
        PushedAt = DateTimeOffset.Parse("2026-06-23T12:10:00Z"),
        PushedCommitSha = "abc123"
    };

    private static Decision CreateDecision(Guid repositoryId, string id, DecisionState state) =>
        new(
            new DecisionId(id),
            state,
            DecisionClassification.Operational,
            "Workflow decision",
            "Context",
            new DecisionMetadata(repositoryId, DateTimeOffset.Parse("2026-06-23T10:00:00Z"), DateTimeOffset.Parse("2026-06-23T10:00:00Z")),
            null,
            [],
            [],
            []);

    private static Decision CreateResolvedDecision(Guid repositoryId, string id) =>
        CreateDecision(repositoryId, id, DecisionState.Resolved) with
        {
            Resolution = new DecisionResolution(
                DecisionOutcome.Accepted,
                "option-1",
                "Resolved for workflow test.",
                "human",
                false,
                DateTimeOffset.Parse("2026-06-23T10:30:00Z"),
                [])
        };

    private static WorkflowTimeline CreateTimeline(Guid repositoryId, DateTimeOffset generatedAt, WorkflowStage currentStage)
    {
        WorkflowTimelineEntry entry = new(
            WorkflowTimelineEventType.ExecutionCompleted,
            WorkflowStage.Handoff,
            DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
            "Execution session completed.",
            "execution",
            "session-1",
            WorkflowFingerprint.ForEntry(
                WorkflowTimelineEventType.ExecutionCompleted,
                WorkflowStage.Handoff,
                DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
                "Execution session completed.",
                "execution",
                "session-1").Value);
        string fingerprint = WorkflowFingerprint.ForTimeline(
            repositoryId,
            currentStage,
            WorkflowStage.Handoff,
            WorkflowProgressState.AwaitingGate,
            WorkflowGateType.CommitApproval,
            [entry],
            [WorkflowBlockingCondition.PendingCommitApproval]).Value;

        return new WorkflowTimeline(
            repositoryId,
            currentStage,
            WorkflowStage.Handoff,
            WorkflowProgressState.AwaitingGate,
            WorkflowGateType.CommitApproval,
            generatedAt,
            [entry],
            fingerprint);
    }

    private sealed class TestFixture
    {
        public Repository Repository { get; } = new()
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            Path = "C:\\repo"
        };

        public RepositoryExecutionState ExecutionState { get; set; } = RepositoryExecutionState.Ready;

        public ExecutionSessionSummary? Session { get; set; }

        public List<Decision> Decisions { get; } = [];

        public List<OperationalContextProposal> Proposals { get; } = [];

        public RepositoryGitStatus GitStatus { get; set; } = new()
        {
            Branch = "main",
            DirtyState = new RepositoryDirtyState { IsClean = true },
            CapturedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z")
        };

        public static TestFixture Create() => new();

        public WorkflowProjectionService CreateService() =>
            new(
                new RepositoryServiceStub(Repository),
                new ExecutionSessionServiceStub(this),
                new DecisionRepositoryStub(this),
                new OperationalContextProposalStoreStub(this),
                new GitServiceStub(this),
                new WorkflowStateMachineService());

        public WorkflowRecoveryService CreateRecoveryService(IWorkflowRepository workflowRepository) =>
            new(new RepositoryServiceStub(Repository), CreateService(), workflowRepository);

        public void ReplaceServices(IServiceCollection services)
        {
            services.RemoveAll<IRepositoryService>();
            services.RemoveAll<IArtifactStore>();
            services.RemoveAll<IExecutionSessionService>();
            services.RemoveAll<IDecisionRepository>();
            services.RemoveAll<IOperationalContextProposalStore>();
            services.RemoveAll<IGitService>();
            services.RemoveAll<IWorkflowStateMachineService>();
            services.RemoveAll<IWorkflowProjectionService>();
            services.RemoveAll<IWorkflowGateCatalogService>();
            services.RemoveAll<IWorkflowRepository>();
            services.RemoveAll<IWorkflowRecoveryService>();
            services.RemoveAll<IHostedService>();

            services.AddSingleton<IRepositoryService>(new RepositoryServiceStub(Repository));
            services.AddSingleton<IArtifactStore, MemoryArtifactStore>();
            services.AddSingleton<IExecutionSessionService>(new ExecutionSessionServiceStub(this));
            services.AddSingleton<IDecisionRepository>(new DecisionRepositoryStub(this));
            services.AddSingleton<IOperationalContextProposalStore>(new OperationalContextProposalStoreStub(this));
            services.AddSingleton<IGitService>(new GitServiceStub(this));
            services.AddSingleton<IWorkflowRepository, FileSystemWorkflowRepository>();
            services.AddSingleton<IWorkflowStateMachineService, WorkflowStateMachineService>();
            services.AddSingleton<IWorkflowProjectionService, WorkflowProjectionService>();
            services.AddSingleton<IWorkflowGateCatalogService, WorkflowGateCatalogService>();
            services.AddSingleton<IWorkflowRecoveryService, WorkflowRecoveryService>();
        }
    }

    private sealed class RepositoryServiceStub(Repository repository) : IRepositoryService
    {
        public Task<IReadOnlyList<Repository>> GetAllAsync() => Task.FromResult<IReadOnlyList<Repository>>([repository]);

        public Task<Repository> RegisterAsync(string repositoryPath) => throw new NotSupportedException("Mutating repository methods are not used by workflow projection.");

        public Task RemoveAsync(Guid repositoryId) => throw new NotSupportedException("Mutating repository methods are not used by workflow projection.");
    }

    private sealed class ExecutionSessionServiceStub(TestFixture fixture) : IExecutionSessionService
    {
        public Task RecoverAsync() => Task.CompletedTask;

        public Task<RepositoryExecutionState> GetRepositoryStateAsync(Guid repositoryId) => Task.FromResult(fixture.ExecutionState);

        public Task<ExecutionSessionSummary?> GetActiveSessionAsync(Guid repositoryId) => Task.FromResult<ExecutionSessionSummary?>(null);

        public Task<ExecutionSessionSummary?> GetRepositorySessionSummaryAsync(Guid repositoryId) => Task.FromResult(fixture.Session);

        public Task<IReadOnlyList<ExecutionSessionSummary>> GetRepositorySessionHistoryAsync(Guid repositoryId, int limit = 10) =>
            Task.FromResult<IReadOnlyList<ExecutionSessionSummary>>(fixture.Session is null ? [] : [fixture.Session]);

        public Task<ExecutionSessionSummary> StartAsync(Guid repositoryId, ExecutionStartRequest request) => throw new NotSupportedException("Mutating execution methods are not used by workflow projection.");

        public Task<ExecutionSession?> GetSessionAsync(Guid sessionId) => Task.FromResult<ExecutionSession?>(null);

        public Task<ExecutionSessionSummary> AcceptAsync(Guid sessionId, ExecutionAcceptanceRequest request) => throw new NotSupportedException("Mutating execution methods are not used by workflow projection.");

        public Task<ExecutionSessionSummary> RejectAsync(Guid sessionId, ExecutionAcceptanceRequest request) => throw new NotSupportedException("Mutating execution methods are not used by workflow projection.");

        public Task<CommitPreparation> PrepareCommitAsync(Guid sessionId) => throw new NotSupportedException("Mutating execution methods are not used by workflow projection.");

        public Task<ExecutionSessionSummary> CommitAsync(Guid sessionId, CommitRequest request) => throw new NotSupportedException("Mutating execution methods are not used by workflow projection.");

        public Task<ExecutionSessionSummary> PushAsync(Guid sessionId, PushRequest request) => throw new NotSupportedException("Mutating execution methods are not used by workflow projection.");
    }

    private sealed class DecisionRepositoryStub(TestFixture fixture) : IDecisionRepository
    {
        public Task<DecisionId> AllocateDecisionIdAsync(Repository repository) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<string> AllocateCandidateIdAsync(Repository repository) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<string> AllocateProposalIdAsync(Repository repository) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<string> AllocateProposalRevisionIdAsync(Repository repository, string proposalId) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<string> AllocatePackageVersionIdAsync(Repository repository, string proposalId) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<string> AllocateRefinementArtifactIdAsync(Repository repository, string proposalId) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<string> AllocateReviewNoteIdAsync(Repository repository, string proposalId) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<Decision>> ListDecisionsAsync(Repository repository) => Task.FromResult<IReadOnlyList<Decision>>(fixture.Decisions);
        public Task<Decision?> GetDecisionAsync(Repository repository, DecisionId decisionId) => Task.FromResult(fixture.Decisions.FirstOrDefault(decision => decision.Id == decisionId));
        public Task<Decision> SaveDecisionAsync(Repository repository, Decision decision) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionCandidate>> ListCandidatesAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionCandidate>>([]);
        public Task<DecisionCandidate?> GetCandidateAsync(Repository repository, string candidateId) => Task.FromResult<DecisionCandidate?>(null);
        public Task<DecisionCandidate> SaveCandidateAsync(Repository repository, DecisionCandidate candidate) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionProposal>> ListProposalsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionProposal>>([]);
        public Task<DecisionProposal?> GetProposalAsync(Repository repository, string proposalId) => Task.FromResult<DecisionProposal?>(null);
        public Task<DecisionProposal> SaveProposalAsync(Repository repository, DecisionProposal proposal) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Repository repository, string proposalId) => Task.FromResult<IReadOnlyList<DecisionProposalRevision>>([]);
        public Task<DecisionProposalRevision> SaveProposalRevisionAsync(Repository repository, DecisionProposalRevision revision) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionPackageVersion>> ListPackageVersionsAsync(Repository repository, string proposalId) => Task.FromResult<IReadOnlyList<DecisionPackageVersion>>([]);
        public Task<DecisionPackageVersion?> GetPackageVersionAsync(Repository repository, string proposalId, string packageId) => Task.FromResult<DecisionPackageVersion?>(null);
        public Task<DecisionPackageVersion> SavePackageVersionAsync(Repository repository, DecisionPackageVersion packageVersion) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionRefinementArtifact>> ListRefinementArtifactsAsync(Repository repository, string proposalId) => Task.FromResult<IReadOnlyList<DecisionRefinementArtifact>>([]);
        public Task<DecisionRefinementArtifact> SaveRefinementArtifactAsync(Repository repository, DecisionRefinementArtifact refinementArtifact) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<DecisionReviewStatus?> GetReviewStatusAsync(Repository repository, string proposalId) => Task.FromResult<DecisionReviewStatus?>(null);
        public Task<DecisionReviewStatus> SaveReviewStatusAsync(Repository repository, DecisionReviewStatus reviewStatus) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionReviewNote>> ListReviewNotesAsync(Repository repository, string proposalId) => Task.FromResult<IReadOnlyList<DecisionReviewNote>>([]);
        public Task<DecisionReviewNote> SaveReviewNoteAsync(Repository repository, DecisionReviewNote note) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<DecisionAssimilationRecommendation?> GetAssimilationRecommendationAsync(Repository repository, DecisionId decisionId) => Task.FromResult<DecisionAssimilationRecommendation?>(null);
        public Task<DecisionAssimilationRecommendation> SaveAssimilationRecommendationAsync(Repository repository, DecisionAssimilationRecommendation recommendation) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionGovernanceReport>> ListGovernanceReportsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionGovernanceReport>>([]);
        public Task<DecisionGovernanceReport> SaveGovernanceReportAsync(Repository repository, DecisionGovernanceReport report) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionCertificationReport>> ListCertificationReportsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionCertificationReport>>([]);
        public Task<DecisionCertificationReport> SaveCertificationReportAsync(Repository repository, DecisionCertificationReport report) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionGenerationCertificationReport>> ListGenerationCertificationReportsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionGenerationCertificationReport>>([]);
        public Task<DecisionGenerationCertificationReport> SaveGenerationCertificationReportAsync(Repository repository, DecisionGenerationCertificationReport report) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionQualityAssessment>> ListQualityAssessmentsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionQualityAssessment>>([]);
        public Task<DecisionQualityAssessment> SaveQualityAssessmentAsync(Repository repository, DecisionQualityAssessment assessment) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionQualityReport>> ListQualityReportsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionQualityReport>>([]);
        public Task<DecisionQualityReport> SaveQualityReportAsync(Repository repository, DecisionQualityReport report) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionQualityTrend>> ListQualityTrendsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionQualityTrend>>([]);
        public Task<DecisionQualityTrend> SaveQualityTrendAsync(Repository repository, DecisionQualityTrend trend) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
    }

    private sealed class OperationalContextProposalStoreStub(TestFixture fixture) : IOperationalContextProposalStore
    {
        public Task<OperationalContextProposal> SaveAsync(Repository repository, OperationalContextProposal proposal, string generatedContent) => throw new NotSupportedException("Mutating context methods are not used by workflow projection.");
        public Task<IReadOnlyList<OperationalContextProposal>> ListAsync(Repository repository, bool includeContent = false) => Task.FromResult<IReadOnlyList<OperationalContextProposal>>(fixture.Proposals);
        public Task<OperationalContextProposal?> GetAsync(Repository repository, string proposalId, bool includeContent = false) => Task.FromResult(fixture.Proposals.FirstOrDefault(proposal => proposal.ProposalId == proposalId));
        public Task<OperationalContextProposal> UpdateAsync(Repository repository, OperationalContextProposal proposal, string? editedContent = null, bool includeContent = false) => throw new NotSupportedException("Mutating context methods are not used by workflow projection.");
        public Task SupersedePendingAsync(Repository repository) => throw new NotSupportedException("Mutating context methods are not used by workflow projection.");
    }

    private sealed class GitServiceStub(TestFixture fixture) : IGitService
    {
        public Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository) => throw new NotSupportedException("Snapshot reads are not used by workflow projection.");
        public Task<RepositoryGitStatus> GetStatusAsync(Repository repository) => Task.FromResult(fixture.GitStatus);
        public Task<CommitPreparation> PrepareCommitAsync(Repository repository, ExecutionSession session) => throw new NotSupportedException("Mutating git methods are not used by workflow projection.");
        public Task<CommitStatusSnapshot> GetCommitStatusSnapshotAsync(Repository repository) => throw new NotSupportedException("Commit snapshots are not used by workflow projection.");
        public Task<CommitResult> CommitAsync(Repository repository, string message, IReadOnlyList<string> selectedPaths, string preparationSnapshotId) => throw new NotSupportedException("Mutating git methods are not used by workflow projection.");
        public Task<PushResult> PushAsync(Repository repository, string? commitSha) => throw new NotSupportedException("Mutating git methods are not used by workflow projection.");
    }
}
