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
using Microsoft.Extensions.Options;

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
    public async Task WorkflowExecutionServiceProjectsRunningExecutionWithoutMutators()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Executing;
        fixture.Session = new ExecutionSessionSummary
        {
            SessionId = Guid.NewGuid(),
            State = ExecutionSessionState.Executing,
            RepositoryState = RepositoryExecutionState.Executing,
            StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
            ProviderName = "codex"
        };

        WorkflowExecutionProjection execution = await fixture.CreateExecutionService().ProjectExecutionAsync(fixture.Repository.Id);
        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
        Assert.False(execution.HasHandoff);
        Assert.False(execution.HasChanges);
        Assert.False(execution.IsExecutionEligible);
        Assert.Equal(WorkflowStage.Execution, projection.CurrentStage);
        Assert.Equal(WorkflowProgressState.Active, projection.ProgressState);
        Assert.Contains(projection.Timeline, entry => entry.EventType == WorkflowTimelineEventType.ExecutionStarted);
    }

    [Fact]
    public async Task WorkflowExecutionServiceProjectsCompletedExecutionEvidence()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();

        WorkflowExecutionProjection execution = await fixture.CreateExecutionService().ProjectExecutionAsync(fixture.Repository.Id);
        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowExecutionStatus.Completed, execution.Status);
        Assert.True(execution.HasHandoff);
        Assert.False(execution.HasChanges);
        Assert.Equal(fixture.Session.SessionId, projection.CurrentExecution.ExecutionId);
        Assert.Equal(WorkflowExecutionStatus.Completed, projection.ExecutionStatus);
        Assert.Contains(projection.Timeline, entry => entry.EventType == WorkflowTimelineEventType.ExecutionHandoffAccepted);
    }

    [Fact]
    public async Task FailedExecutionBlocksWorkflowWithExecutionFailureDiagnostics()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Failed;
        fixture.Session = new ExecutionSessionSummary
        {
            SessionId = Guid.NewGuid(),
            State = ExecutionSessionState.Failed,
            RepositoryState = RepositoryExecutionState.Failed,
            StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
            LastActivityAt = DateTimeOffset.Parse("2026-06-23T10:03:00Z"),
            FailureReason = "Provider exited with code 1."
        };

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.Failed, projection.CurrentStage);
        Assert.Equal(WorkflowProgressState.Failed, projection.ProgressState);
        Assert.Equal(WorkflowExecutionStatus.Failed, projection.ExecutionStatus);
        Assert.NotNull(projection.ExecutionFailure);
        Assert.Equal("Provider exited with code 1.", projection.ExecutionFailure.Reason);
        Assert.Contains(projection.Timeline, entry => entry.EventType == WorkflowTimelineEventType.ExecutionFailed);
    }

    [Fact]
    public async Task CancelledExecutionBlocksWorkflowAtWorkSelection()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Cancelled;
        fixture.Session = new ExecutionSessionSummary
        {
            SessionId = Guid.NewGuid(),
            State = ExecutionSessionState.Cancelled,
            RepositoryState = RepositoryExecutionState.Cancelled,
            StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
            FailureReason = "Cancelled by user."
        };

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.Blocked, projection.CurrentStage);
        Assert.Equal(WorkflowProgressState.Blocked, projection.ProgressState);
        Assert.Equal(WorkflowGateType.WorkSelection, projection.BlockingGate);
        Assert.Equal(WorkflowExecutionStatus.Cancelled, projection.ExecutionStatus);
        Assert.Contains(projection.Timeline, entry => entry.EventType == WorkflowTimelineEventType.ExecutionCancelled);
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
        Assert.Equal(WorkflowDecisionStatus.AwaitingResolution, projection.DecisionStatus);
        Assert.False(projection.IsDecisionResolutionEligible);
        Assert.Contains(projection.BlockedTransitions, transition => transition.BlockingCondition == WorkflowBlockingCondition.UnresolvedDecision);
    }

    [Fact]
    public async Task ResolvedDecisionClosesDecisionGateAndAllowsOperationalContext()
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
            Status = OperationalContextProposalStatus.Pending
        });

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.OperationalContext, projection.CurrentStage);
        Assert.Equal(WorkflowDecisionStatus.Resolved, projection.DecisionStatus);
        Assert.True(projection.IsDecisionResolutionEligible);
        Assert.Contains(projection.SatisfiedGates, gate => gate.Type == WorkflowGateType.DecisionResolution);
        Assert.Contains(projection.Timeline, entry => entry.EventType == WorkflowTimelineEventType.DecisionResolved);
    }

    [Fact]
    public async Task DecisionServiceProjectsDiscoveredGeneratedAndArchivedStates()
    {
        TestFixture discoveredFixture = TestFixture.Create();
        discoveredFixture.Candidates.Add(CreateCandidate(discoveredFixture.Repository.Id, "cand-0001"));

        WorkflowDecisionProjection discovered =
            await discoveredFixture.CreateDecisionService().ProjectDecisionAsync(discoveredFixture.Repository.Id);

        Assert.Equal(WorkflowDecisionStatus.Discovered, discovered.Status);
        Assert.Equal("cand-0001", discovered.CandidateId);
        Assert.False(discovered.IsResolutionEligible);

        TestFixture generatedFixture = TestFixture.Create();
        generatedFixture.DecisionProposals.Add(CreateProposal(generatedFixture.Repository.Id, "proposal-0001", "cand-0001"));

        WorkflowDecisionProjection generated =
            await generatedFixture.CreateDecisionService().ProjectDecisionAsync(generatedFixture.Repository.Id);

        Assert.Equal(WorkflowDecisionStatus.Generated, generated.Status);
        Assert.Equal("proposal-0001", generated.ProposalId);
        Assert.False(generated.IsResolutionEligible);

        TestFixture archivedFixture = TestFixture.Create();
        archivedFixture.Decisions.Add(CreateDecision(archivedFixture.Repository.Id, "DEC-0001", DecisionState.Archived));

        WorkflowDecisionProjection archived =
            await archivedFixture.CreateDecisionService().ProjectDecisionAsync(archivedFixture.Repository.Id);

        Assert.Equal(WorkflowDecisionStatus.Archived, archived.Status);
        Assert.True(archived.IsResolutionEligible);
    }

    [Fact]
    public async Task SupersededDecisionFollowsResolvedReplacementAuthority()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        Decision superseded = CreateResolvedDecision(fixture.Repository.Id, "DEC-0001") with
        {
            State = DecisionState.Superseded
        };
        Decision replacement = CreateResolvedDecision(fixture.Repository.Id, "DEC-0002") with
        {
            Relationships =
            [
                new DecisionRelationship(
                    new DecisionId("DEC-0002"),
                    new DecisionId("DEC-0001"),
                    DecisionRelationshipType.Supersedes,
                    "Replacement authority.")
            ]
        };
        fixture.Decisions.Add(superseded);
        fixture.Decisions.Add(replacement);

        WorkflowDecisionProjection decision = await fixture.CreateDecisionService().ProjectDecisionAsync(fixture.Repository.Id);
        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowDecisionStatus.Resolved, decision.Status);
        Assert.True(decision.IsResolutionEligible);
        Assert.Contains(decision.Diagnostics.SupersessionSignals, signal => signal.Contains("DEC-0002", StringComparison.Ordinal));
        Assert.NotEqual(WorkflowStage.Decision, projection.CurrentStage);
    }

    [Fact]
    public async Task GovernanceBlockedDecisionBlocksWorkflowFromDecisionDomainEvidence()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateResolvedDecision(fixture.Repository.Id, "DEC-0001"));
        fixture.GovernanceReports.Add(new DecisionGovernanceReport(
            "gov-0001",
            fixture.Repository.Id,
            DateTimeOffset.Parse("2026-06-23T10:45:00Z"),
            "input",
            DecisionHealthAssessment.Blocked,
            new DecisionGovernanceSummary(1, 1, 0, 1, 0, 1, 1),
            [
                new DecisionGovernanceFinding(
                    "finding-1",
                    DecisionGovernanceCategory.AuthorityBoundary,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Blocked by decisions domain",
                    "Decision governance marked this as blocking.",
                    [],
                    ["DEC-0001"],
                    [],
                    [])
            ],
            []));

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.Decision, projection.CurrentStage);
        Assert.Equal(WorkflowProgressState.Blocked, projection.ProgressState);
        Assert.Equal(WorkflowGateType.DecisionResolution, projection.BlockingGate);
        Assert.True(projection.IsDecisionGovernanceBlocked);
        Assert.Contains(projection.DecisionDiagnostics.GovernanceSignals, signal => signal.Contains("Blocked", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DecisionQualityAndCertificationSignalsSurfaceAsDiagnostics()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateResolvedDecision(fixture.Repository.Id, "DEC-0001"));
        fixture.QualityAssessments.Add(new DecisionQualityAssessment(
            "quality-0001",
            fixture.Repository.Id,
            "DEC-0001",
            DateTimeOffset.Parse("2026-06-23T10:50:00Z"),
            DecisionQualityRating.Mixed,
            61,
            [
                new DecisionQualitySignal(
                    "quality-signal-1",
                    fixture.Repository.Id,
                    "DEC-0001",
                    "Recommendation Stability",
                    QualitySignalDirection.Negative,
                    QualitySignalSeverity.Medium,
                    "Recommendation changed late.",
                    "The selected option changed after review.",
                    [])
            ],
            [
                new HumanAuthoringBurdenSignal(
                    "burden-1",
                    fixture.Repository.Id,
                    "DEC-0001",
                    HumanAuthoringBurden.MajorRefinement,
                    "proposal",
                    "Human review required substantial rewrite.",
                    [])
            ],
            []));
        fixture.CertificationReports.Add(new DecisionCertificationReport(
            "cert-0001",
            fixture.Repository.Id,
            DateTimeOffset.Parse("2026-06-23T10:55:00Z"),
            "input",
            new DecisionLifecycleCertificationResult(DecisionLifecycleCertificationResultKind.Failed, 2, 1),
            DecisionHealthAssessment.AdvisoryFindings,
            [],
            [],
            []));

        WorkflowDecisionProjection decision = await fixture.CreateDecisionService().ProjectDecisionAsync(fixture.Repository.Id);

        Assert.Equal("MajorRefinement", decision.HumanAuthoringBurden);
        Assert.Equal("Mixed:61", decision.QualityStatus);
        Assert.Equal("Failed", decision.CertificationStatus);
        Assert.Contains(decision.Diagnostics.QualitySignals, signal => signal.Contains("Recommendation Stability", StringComparison.Ordinal));
        Assert.Contains(decision.Diagnostics.CertificationSignals, signal => signal.Contains("failed=1", StringComparison.OrdinalIgnoreCase));
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
        Assert.Equal(WorkflowOperationalContextStatus.UnderReview, projection.OperationalContextStatus);
        Assert.False(projection.IsOperationalContextReviewEligible);
        Assert.False(projection.IsOperationalContextCommitEligible);
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
        Assert.Equal(WorkflowOperationalContextStatus.ReadyForPromotion, projection.OperationalContextStatus);
        Assert.True(projection.IsOperationalContextReviewEligible);
        Assert.False(projection.IsOperationalContextPromotionEligible);
        Assert.False(projection.IsOperationalContextCommitEligible);
        Assert.Contains(projection.BlockedTransitions, transition => transition.BlockingCondition == WorkflowBlockingCondition.PendingContextPromotion);
    }

    [Fact]
    public async Task OperationalContextServiceProjectsEditedRejectedPromotedAndNoContextRequired()
    {
        TestFixture editedFixture = TestFixture.Create();
        editedFixture.ExecutionState = RepositoryExecutionState.Accepted;
        editedFixture.Session = CompletedAcceptedSession();
        editedFixture.Proposals.Add(new OperationalContextProposal
        {
            ProposalId = "ctx-edited",
            RepositoryId = editedFixture.Repository.Id,
            GeneratedAt = DateTimeOffset.Parse("2026-06-23T11:00:00Z"),
            Status = OperationalContextProposalStatus.Edited,
            Review = new OperationalContextReview
            {
                ProposalId = "ctx-edited",
                ReviewState = OperationalContextReviewState.Edited,
                ReviewedAt = DateTimeOffset.Parse("2026-06-23T11:10:00Z")
            }
        });

        WorkflowInstance editedProjection = await editedFixture.CreateService().ProjectAsync(editedFixture.Repository.Id);

        Assert.Equal(WorkflowOperationalContextStatus.Edited, editedProjection.OperationalContextStatus);
        Assert.Equal(WorkflowGateType.OperationalContextPromotion, editedProjection.BlockingGate);
        Assert.Contains(editedProjection.Timeline, entry => entry.EventType == WorkflowTimelineEventType.OperationalContextEdited);

        TestFixture rejectedFixture = TestFixture.Create();
        rejectedFixture.ExecutionState = RepositoryExecutionState.Accepted;
        rejectedFixture.Session = CompletedAcceptedSession();
        rejectedFixture.Proposals.Add(new OperationalContextProposal
        {
            ProposalId = "ctx-rejected",
            RepositoryId = rejectedFixture.Repository.Id,
            GeneratedAt = DateTimeOffset.Parse("2026-06-23T11:00:00Z"),
            Status = OperationalContextProposalStatus.Rejected,
            Review = new OperationalContextReview
            {
                ProposalId = "ctx-rejected",
                ReviewState = OperationalContextReviewState.Rejected,
                ReviewedAt = DateTimeOffset.Parse("2026-06-23T11:10:00Z")
            }
        });

        WorkflowInstance rejectedProjection = await rejectedFixture.CreateService().ProjectAsync(rejectedFixture.Repository.Id);

        Assert.Equal(WorkflowOperationalContextStatus.Rejected, rejectedProjection.OperationalContextStatus);
        Assert.True(rejectedProjection.IsOperationalContextCommitEligible);
        Assert.Equal(WorkflowStage.Completed, rejectedProjection.CurrentStage);
        Assert.Equal(WorkflowGitStatus.NoChangesProduced, rejectedProjection.GitCommitStatus);
        Assert.Contains(rejectedProjection.SatisfiedGates, gate => gate.Type == WorkflowGateType.OperationalContextReview);
        Assert.Contains(rejectedProjection.Timeline, entry => entry.EventType == WorkflowTimelineEventType.OperationalContextRejected);

        TestFixture promotedFixture = TestFixture.Create();
        promotedFixture.ExecutionState = RepositoryExecutionState.Accepted;
        promotedFixture.Session = CompletedAcceptedSession();
        promotedFixture.Proposals.Add(new OperationalContextProposal
        {
            ProposalId = "ctx-promoted",
            RepositoryId = promotedFixture.Repository.Id,
            GeneratedAt = DateTimeOffset.Parse("2026-06-23T11:00:00Z"),
            Status = OperationalContextProposalStatus.Promoted,
            Review = new OperationalContextReview
            {
                ProposalId = "ctx-promoted",
                ReviewState = OperationalContextReviewState.Accepted,
                ReviewedAt = DateTimeOffset.Parse("2026-06-23T11:10:00Z")
            },
            Promotion = new OperationalContextPromotion
            {
                ProposalId = "ctx-promoted",
                PromotedAt = DateTimeOffset.Parse("2026-06-23T11:20:00Z")
            }
        });

        WorkflowInstance promotedProjection = await promotedFixture.CreateService().ProjectAsync(promotedFixture.Repository.Id);

        Assert.Equal(WorkflowOperationalContextStatus.Promoted, promotedProjection.OperationalContextStatus);
        Assert.True(promotedProjection.IsOperationalContextCommitEligible);
        Assert.Contains(promotedProjection.SatisfiedGates, gate => gate.Type == WorkflowGateType.OperationalContextPromotion);
        Assert.Contains(promotedProjection.Timeline, entry => entry.EventType == WorkflowTimelineEventType.OperationalContextPromoted);

        TestFixture noContextFixture = TestFixture.Create();
        noContextFixture.ExecutionState = RepositoryExecutionState.Accepted;
        noContextFixture.Session = CompletedAcceptedSession();

        WorkflowOperationalContextProjection noContext =
            await noContextFixture.CreateOperationalContextService().ProjectOperationalContextAsync(
                noContextFixture.Repository.Id,
                await noContextFixture.CreateDecisionService().ProjectDecisionAsync(noContextFixture.Repository.Id),
                await noContextFixture.CreateExecutionService().ProjectExecutionAsync(noContextFixture.Repository.Id));

        Assert.Equal(WorkflowOperationalContextStatus.NoContextRequired, noContext.Status);
        Assert.True(noContext.IsCommitEligible);
        Assert.Contains(noContext.Diagnostics.Reasoning, reason => reason.Contains("no context update is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OperationalContextProjectionLinksResolvedDecisionWhenAssimilationEvidenceMatchesProposal()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        Decision decision = CreateResolvedDecision(fixture.Repository.Id, "DEC-0001");
        fixture.Decisions.Add(decision);
        fixture.AssimilationRecommendations.Add(CreateAssimilationRecommendation(fixture.Repository.Id, decision, "decision-fingerprint"));
        fixture.Proposals.Add(new OperationalContextProposal
        {
            ProposalId = "ctx-0001",
            RepositoryId = fixture.Repository.Id,
            GeneratedAt = DateTimeOffset.Parse("2026-06-23T11:00:00Z"),
            Status = OperationalContextProposalStatus.Pending,
            InputFingerprints =
            [
                new OperationalContextInputFingerprint
                {
                    Name = "decision assimilation DEC-0001",
                    RelativePath = ".agents/decisions/assimilation/DEC-0001/recommendation.md",
                    Present = true,
                    Hash = "decision-fingerprint"
                }
            ]
        });

        WorkflowOperationalContextProjection context =
            await fixture.CreateOperationalContextService().ProjectOperationalContextAsync(
                fixture.Repository.Id,
                await fixture.CreateDecisionService().ProjectDecisionAsync(fixture.Repository.Id),
                await fixture.CreateExecutionService().ProjectExecutionAsync(fixture.Repository.Id));

        Assert.Equal("DEC-0001", context.SourceDecisionId);
        Assert.Equal(fixture.Session.SessionId.ToString(), context.SourceExecutionId);
        Assert.Contains(context.Diagnostics.LinkageSignals, signal => signal.Contains("DEC-0001", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AwaitingCommitMapsToCommitApprovalGate()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);

        WorkflowGitProjection git = await fixture.CreateGitWorkflowService().ProjectGitAsync(
            fixture.Repository.Id,
            await fixture.CreateExecutionService().ProjectExecutionAsync(fixture.Repository.Id),
            await fixture.CreateOperationalContextService().ProjectOperationalContextAsync(
                fixture.Repository.Id,
                await fixture.CreateDecisionService().ProjectDecisionAsync(fixture.Repository.Id),
                await fixture.CreateExecutionService().ProjectExecutionAsync(fixture.Repository.Id)));
        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowGitStatus.AwaitingCommit, git.CommitStatus);
        Assert.Equal(WorkflowGitStatus.NotReady, git.PushStatus);
        Assert.True(git.IsCommitGateOpen);
        Assert.False(git.Completion.IsComplete);
        Assert.Equal(WorkflowStage.Commit, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.CommitApproval, projection.BlockingGate);
        Assert.Equal(WorkflowGitStatus.AwaitingCommit, projection.GitCommitStatus);
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

        WorkflowGitProjection git = await fixture.CreateGitWorkflowService().ProjectGitAsync(
            fixture.Repository.Id,
            await fixture.CreateExecutionService().ProjectExecutionAsync(fixture.Repository.Id),
            await fixture.CreateOperationalContextService().ProjectOperationalContextAsync(
                fixture.Repository.Id,
                await fixture.CreateDecisionService().ProjectDecisionAsync(fixture.Repository.Id),
                await fixture.CreateExecutionService().ProjectExecutionAsync(fixture.Repository.Id)));
        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowGitStatus.Committed, git.CommitStatus);
        Assert.Equal(WorkflowGitStatus.AwaitingPush, git.PushStatus);
        Assert.True(git.IsPushGateOpen);
        Assert.False(git.Completion.IsComplete);
        Assert.Equal(WorkflowStage.Push, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.PushApproval, projection.BlockingGate);
        Assert.Equal(WorkflowGitStatus.AwaitingPush, projection.GitPushStatus);
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
        Assert.Equal(WorkflowGitStatus.Pushed, first.GitPushStatus);
        Assert.True(first.CompletionEvaluation.IsComplete);
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
    public async Task AcceptedCleanExecutionCompletesAsNoChangesProduced()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();

        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.Completed, projection.CurrentStage);
        Assert.Equal(WorkflowGitStatus.NoChangesProduced, projection.GitCommitStatus);
        Assert.Equal(WorkflowGitStatus.NoChangesProduced, projection.GitPushStatus);
        Assert.True(projection.CompletionEvaluation.IsComplete);
        Assert.Contains("no changes", projection.CompletionEvaluation.CompletionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WorkflowGitEndpointReturnsGitProjection()
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
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/git");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        WorkflowGitProjection? git = await response.Content.ReadFromJsonAsync<WorkflowGitProjection>(JsonOptions);
        Assert.NotNull(git);
        Assert.Equal(WorkflowGitStatus.Committed, git.CommitStatus);
        Assert.Equal(WorkflowGitStatus.AwaitingPush, git.PushStatus);
        Assert.False(git.Completion.IsComplete);
    }

    [Fact]
    public async Task ContinuationEvaluationStopsAtOpenAuthorityGate()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = AwaitingPushCommittedSession();

        WorkflowContinuationEvaluation evaluation =
            await fixture.CreateContinuationService().EvaluateContinuationAsync(fixture.Repository.Id);

        Assert.False(evaluation.CanAdvanceMechanically);
        Assert.True(evaluation.IsWaitingForHuman);
        Assert.Equal(WorkflowStage.Push, evaluation.FromStage);
        Assert.Null(evaluation.ToStage);
        Assert.Equal(WorkflowGateType.PushApproval, evaluation.BlockingGate);
        Assert.Contains("PushApproval", evaluation.StopReason, StringComparison.Ordinal);
        Assert.Contains(evaluation.Diagnostics.Reasoning, reason => reason.Contains("Open authority gates stop continuation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ContinuationEvaluationIsDeterministicForIdenticalWorkflowState()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);

        WorkflowContinuationEvaluation first =
            await fixture.CreateContinuationService().EvaluateContinuationAsync(fixture.Repository.Id);
        WorkflowContinuationEvaluation second =
            await fixture.CreateContinuationService().EvaluateContinuationAsync(fixture.Repository.Id);

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.CanAdvanceMechanically, second.CanAdvanceMechanically);
        Assert.Equal(first.FromStage, second.FromStage);
        Assert.Equal(first.ToStage, second.ToStage);
        Assert.Equal(first.StopReason, second.StopReason);
    }

    [Fact]
    public async Task ContinuationEvaluationDoesNotAutoSelectWorkAfterCompletion()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();

        WorkflowContinuationEvaluation evaluation =
            await fixture.CreateContinuationService().EvaluateContinuationAsync(fixture.Repository.Id);

        Assert.True(evaluation.IsComplete);
        Assert.False(evaluation.CanAdvanceMechanically);
        Assert.True(evaluation.IsWaitingForHuman);
        Assert.Equal(WorkflowStage.Completed, evaluation.FromStage);
        Assert.Equal(WorkflowGateType.WorkSelection, evaluation.BlockingGate);
        Assert.Contains("WorkSelection", evaluation.StopReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkflowContinuationEvaluationEndpointReturnsEvaluation()
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
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/continuation/evaluation");
        WorkflowContinuationEvaluation? evaluation = await response.Content.ReadFromJsonAsync<WorkflowContinuationEvaluation>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(evaluation);
        Assert.Equal(WorkflowStage.Push, evaluation.FromStage);
        Assert.False(evaluation.CanAdvanceMechanically);
        Assert.Equal(WorkflowGateType.PushApproval, evaluation.BlockingGate);
    }

    [Fact]
    public async Task WorkflowContinuationRunEndpointPersistsContinuationEvent()
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
        HttpResponseMessage runResponse = await client.PostAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/continuation/run", null);
        WorkflowContinuationEvent? continuationEvent = await runResponse.Content.ReadFromJsonAsync<WorkflowContinuationEvent>(JsonOptions);
        HttpResponseMessage historyResponse = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/continuation/history");
        WorkflowContinuationEvent[]? history = await historyResponse.Content.ReadFromJsonAsync<WorkflowContinuationEvent[]>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);
        Assert.NotNull(continuationEvent);
        Assert.Equal(WorkflowStage.Push, continuationEvent.FromStage);
        Assert.Equal(WorkflowGateType.PushApproval, continuationEvent.BlockingGate);
        Assert.Equal("Stop", continuationEvent.Decision);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        WorkflowContinuationEvent persisted = Assert.Single(history ?? []);
        Assert.Equal(continuationEvent.EventId, persisted.EventId);
        Assert.Equal(continuationEvent.InputFingerprint, persisted.InputFingerprint);
    }

    [Theory]
    [MemberData(nameof(AuthorityGatePreparationCases))]
    public async Task PreparationEvaluationRefusesEveryOpenAuthorityGate(
        WorkflowGateType expectedGate,
        string scenario)
    {
        TestFixture fixture = TestFixture.Create();
        ArrangeAuthorityGateScenario(fixture, scenario);

        WorkflowPreparationEvaluation evaluation =
            await fixture.CreatePreparationService().EvaluatePreparationAsync(fixture.Repository.Id);

        Assert.False(evaluation.CanPrepare);
        Assert.True(evaluation.IsWaitingForHuman);
        Assert.Equal(expectedGate, evaluation.BlockingGate);
        Assert.Equal("Refused", evaluation.Outcome);
        Assert.Contains(expectedGate.ToString(), evaluation.Reason, StringComparison.Ordinal);
        Assert.Contains(evaluation.Diagnostics.RefusalReasons, reason => reason.Contains(expectedGate.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreparationEvaluationIsDeterministicForIdenticalWorkflowState()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);

        WorkflowPreparationEvaluation first =
            await fixture.CreatePreparationService().EvaluatePreparationAsync(fixture.Repository.Id);
        WorkflowPreparationEvaluation second =
            await fixture.CreatePreparationService().EvaluatePreparationAsync(fixture.Repository.Id);

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.CanPrepare, second.CanPrepare);
        Assert.Equal(first.Stage, second.Stage);
        Assert.Equal(first.Command, second.Command);
        Assert.Equal(first.Reason, second.Reason);
    }

    [Fact]
    public async Task PreparationRunDoesNotDuplicateIdenticalFingerprint()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = AwaitingPushCommittedSession();
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository);

        WorkflowPreparationEvent first = await service.RunPreparationAsync(fixture.Repository.Id);
        WorkflowPreparationEvent second = await service.RunPreparationAsync(fixture.Repository.Id);
        IReadOnlyList<WorkflowPreparationEvent> history = await workflowRepository.ListPreparationEventsAsync(fixture.Repository);

        Assert.Equal(first.EventId, second.EventId);
        Assert.Equal(first.InputFingerprint, second.InputFingerprint);
        Assert.Single(history);
        Assert.Equal(WorkflowGateType.PushApproval, first.BlockingGate);
        Assert.Empty(first.CreatedArtifactIds);
    }

    [Fact]
    public async Task PreparationAfterRestartDoesNotDuplicateIdenticalFingerprint()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        var firstService = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository);
        WorkflowPreparationEvent first = await firstService.RunPreparationAsync(fixture.Repository.Id);
        var restartedService = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository);

        WorkflowPreparationEvent restarted = await restartedService.RunPreparationAsync(fixture.Repository.Id);
        IReadOnlyList<WorkflowPreparationEvent> history = await workflowRepository.ListPreparationEventsAsync(fixture.Repository);

        Assert.Equal(first.EventId, restarted.EventId);
        Assert.Equal(first.InputFingerprint, restarted.InputFingerprint);
        Assert.Single(history);
        Assert.Empty(restarted.CreatedArtifactIds);
    }

    [Fact]
    public async Task WorkflowPreparationEndpointsReturnEvaluationRunAndHistory()
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
        HttpResponseMessage evaluationResponse = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/preparation/evaluation");
        WorkflowPreparationEvaluation? evaluation = await evaluationResponse.Content.ReadFromJsonAsync<WorkflowPreparationEvaluation>(JsonOptions);
        HttpResponseMessage runResponse = await client.PostAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/preparation/run", null);
        WorkflowPreparationEvent? preparationEvent = await runResponse.Content.ReadFromJsonAsync<WorkflowPreparationEvent>(JsonOptions);
        HttpResponseMessage historyResponse = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/preparation/history");
        WorkflowPreparationEvent[]? history = await historyResponse.Content.ReadFromJsonAsync<WorkflowPreparationEvent[]>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        Assert.NotNull(evaluation);
        Assert.True(evaluation.CanPrepare);
        Assert.Equal(WorkflowGateType.CommitApproval, evaluation.BlockingGate);
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);
        Assert.NotNull(preparationEvent);
        Assert.Equal("Created", preparationEvent.Decision);
        Assert.Contains("commit-preparation:snapshot-prepared", preparationEvent.CreatedArtifactIds);
        Assert.Equal(evaluation.Fingerprint, preparationEvent.InputFingerprint);
        Assert.Equal(1, fixture.PrepareCommitCallCount);
        Assert.Equal(0, fixture.CommitCallCount);
        Assert.Equal(0, fixture.PushCallCount);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        WorkflowPreparationEvent persisted = Assert.Single(history ?? []);
        Assert.Equal(preparationEvent.EventId, persisted.EventId);
    }

    [Fact]
    public async Task PreparationEvaluationSkipsDecisionArtifactsWhenEquivalentDomainEvidenceExists()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Candidates.Add(CreateCandidate(fixture.Repository.Id, "cand-0001"));
        fixture.DecisionProposals.Add(CreateProposal(fixture.Repository.Id, "proposal-0001", "cand-0001"));
        fixture.PackageVersions.Add(new DecisionPackageVersion(
            "package-0001",
            fixture.Repository.Id,
            "proposal-0001",
            "cand-0001",
            DateTimeOffset.Parse("2026-06-23T10:20:00Z"),
            "package-fingerprint",
            null!));

        WorkflowPreparationEvaluation evaluation =
            await fixture.CreatePreparationService().EvaluatePreparationAsync(fixture.Repository.Id);

        Assert.False(evaluation.CanPrepare);
        Assert.True(evaluation.HasDuplicateDomainEvidence);
        Assert.Equal("Duplicate", evaluation.Outcome);
        Assert.Contains("decision-candidate:cand-0001", evaluation.DuplicateEvidence);
        Assert.Contains("decision-proposal:proposal-0001", evaluation.DuplicateEvidence);
        Assert.Contains("decision-package:package-0001", evaluation.DuplicateEvidence);
        Assert.Contains(evaluation.Diagnostics.DuplicateEvidence, evidence => evidence.Contains("decision-package", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreparationRunDoesNotInvokeDecisionDiscoveryWhenGateIsOpen()
    {
        TestFixture fixture = TestFixture.Create();
        ArrangeAuthorityGateScenario(fixture, "decision-resolution");
        var discoveryService = new DecisionDiscoveryServiceStub([]);
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new FileSystemWorkflowRepository(new MemoryArtifactStore()),
            discoveryService);

        WorkflowPreparationEvent preparationEvent = await service.RunPreparationAsync(fixture.Repository.Id);

        Assert.Equal("Refused", preparationEvent.Decision);
        Assert.Equal(WorkflowGateType.DecisionResolution, preparationEvent.BlockingGate);
        Assert.Equal(0, discoveryService.CallCount);
        Assert.Empty(preparationEvent.CreatedArtifactIds);
    }

    [Fact]
    public async Task PreparationRunDoesNotInvokeDecisionDiscoveryWhenDuplicateEvidenceExists()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Candidates.Add(CreateCandidate(fixture.Repository.Id, "cand-0001"));
        var discoveryService = new DecisionDiscoveryServiceStub([]);
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new FileSystemWorkflowRepository(new MemoryArtifactStore()),
            discoveryService);

        WorkflowPreparationEvent preparationEvent = await service.RunPreparationAsync(fixture.Repository.Id);

        Assert.Equal("Duplicate", preparationEvent.Decision);
        Assert.Equal(0, discoveryService.CallCount);
        Assert.Empty(preparationEvent.CreatedArtifactIds);
        Assert.Contains("decision-candidate:cand-0001", preparationEvent.DuplicateEvidence);
    }

    [Fact]
    public async Task PreparationRunInvokesDecisionDiscoveryAndPersistsCreatedArtifacts()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        DecisionCandidate candidate = CreateCandidate(fixture.Repository.Id, "cand-0001");
        var discoveryService = new DecisionDiscoveryServiceStub([candidate], discovered =>
        {
            fixture.Candidates.AddRange(discovered);
        });
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T10:45:00Z"),
                WorkflowStage.Decision,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.Handoff,
                []));
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository,
            discoveryService);

        WorkflowPreparationEvent preparationEvent = await service.RunPreparationAsync(fixture.Repository.Id);
        IReadOnlyList<WorkflowPreparationEvent> history = await workflowRepository.ListPreparationEventsAsync(fixture.Repository);
        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(1, discoveryService.CallCount);
        Assert.Equal("Created", preparationEvent.Decision);
        Assert.Contains("decision-candidate:cand-0001", preparationEvent.CreatedArtifactIds);
        Assert.Single(history);
        Assert.Equal(WorkflowStage.Decision, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.DecisionResolution, projection.BlockingGate);
    }

    [Fact]
    public async Task PreparationRunRepeatDoesNotCreateDuplicateDecisionArtifacts()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        DecisionCandidate candidate = CreateCandidate(fixture.Repository.Id, "cand-0001");
        var discoveryService = new DecisionDiscoveryServiceStub([candidate], discovered =>
        {
            fixture.Candidates.AddRange(discovered);
        });
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T10:45:00Z"),
                WorkflowStage.Decision,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.Handoff,
                []));
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository,
            discoveryService);

        WorkflowPreparationEvent first = await service.RunPreparationAsync(fixture.Repository.Id);
        WorkflowPreparationEvent second = await service.RunPreparationAsync(fixture.Repository.Id);

        Assert.Equal(1, discoveryService.CallCount);
        Assert.Equal("Created", first.Decision);
        Assert.Equal("Duplicate", second.Decision);
        Assert.Contains("decision-candidate:cand-0001", second.DuplicateEvidence);
    }

    [Fact]
    public async Task PreparationRunInvokesDecisionProposalGenerationForPromotedCandidateAndPreservesDecisionGate()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        DecisionCandidate candidate = CreatePromotedCandidate(fixture.Repository.Id, "cand-0001");
        fixture.Candidates.Add(candidate);
        DecisionProposal proposal = CreateProposal(fixture.Repository.Id, "proposal-created", candidate.Id);
        var generationService = new DecisionGenerationServiceStub(proposal, generated =>
        {
            fixture.DecisionProposals.Add(generated);
            fixture.PackageVersions.Add(new DecisionPackageVersion(
                "package-created",
                fixture.Repository.Id,
                generated.Id,
                generated.CandidateId,
                DateTimeOffset.Parse("2026-06-23T10:50:00Z"),
                "package-fingerprint",
                null!));
        });
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository,
            null,
            null,
            null,
            generationService);

        WorkflowPreparationEvent preparationEvent = await service.RunPreparationAsync(fixture.Repository.Id);
        IReadOnlyList<WorkflowPreparationEvent> history = await workflowRepository.ListPreparationEventsAsync(fixture.Repository);
        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(1, generationService.CallCount);
        Assert.Equal("Created", preparationEvent.Decision);
        Assert.Equal(WorkflowPreparationCommand.GenerateDecisionProposal, preparationEvent.Command);
        Assert.Equal(WorkflowGateType.DecisionResolution, preparationEvent.BlockingGate);
        Assert.Contains("decision-proposal:proposal-created", preparationEvent.CreatedArtifactIds);
        Assert.Single(history);
        Assert.Equal(WorkflowStage.Decision, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.DecisionResolution, projection.BlockingGate);
        Assert.False(projection.IsDecisionResolutionEligible);
        Assert.Single(fixture.DecisionProposals);
        Assert.Empty(fixture.Decisions);
    }

    [Fact]
    public async Task PreparationRunRepeatDoesNotCreateDuplicateDecisionProposal()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        DecisionCandidate candidate = CreatePromotedCandidate(fixture.Repository.Id, "cand-0001");
        fixture.Candidates.Add(candidate);
        DecisionProposal proposal = CreateProposal(fixture.Repository.Id, "proposal-created", candidate.Id);
        var generationService = new DecisionGenerationServiceStub(proposal, generated =>
        {
            fixture.DecisionProposals.Add(generated);
        });
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository,
            null,
            null,
            null,
            generationService);

        WorkflowPreparationEvent first = await service.RunPreparationAsync(fixture.Repository.Id);
        WorkflowPreparationEvent second = await service.RunPreparationAsync(fixture.Repository.Id);

        Assert.Equal(1, generationService.CallCount);
        Assert.Equal("Created", first.Decision);
        Assert.Equal("Duplicate", second.Decision);
        Assert.Contains("decision-proposal:proposal-created", second.DuplicateEvidence);
        Assert.Single(fixture.DecisionProposals);
    }

    [Fact]
    public async Task PreparationEvaluationSkipsOperationalContextArtifactsWhenEquivalentDomainEvidenceExists()
    {
        TestFixture fixture = TestFixture.Create();
        Decision decision = CreateResolvedDecision(fixture.Repository.Id, "DEC-0001");
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(decision);
        fixture.AssimilationRecommendations.Add(CreateAssimilationRecommendation(fixture.Repository.Id, decision, "decision-fingerprint"));
        fixture.Proposals.Add(CreateOperationalContextProposal(
            fixture.Repository.Id,
            "ctx-0001",
            OperationalContextProposalStatus.Promoted,
            OperationalContextReviewState.Accepted,
            DateTimeOffset.Parse("2026-06-23T11:10:00Z"),
            DateTimeOffset.Parse("2026-06-23T11:20:00Z")));

        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T11:25:00Z"),
                WorkflowStage.OperationalContext,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.Decision,
                []));
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository);

        WorkflowPreparationEvaluation evaluation = await service.EvaluatePreparationAsync(fixture.Repository.Id);

        Assert.False(evaluation.CanPrepare);
        Assert.True(evaluation.HasDuplicateDomainEvidence);
        Assert.Equal("Duplicate", evaluation.Outcome);
        Assert.Contains("operational-context-proposal:ctx-0001", evaluation.DuplicateEvidence);
        Assert.Contains("operational-context-decision-link:DEC-0001", evaluation.DuplicateEvidence);
        Assert.Contains("operational-context-assimilation", evaluation.DuplicateEvidence);
    }

    [Fact]
    public async Task PreparationRunDoesNotInvokeOperationalContextGenerationWhenGateIsOpen()
    {
        TestFixture fixture = TestFixture.Create();
        ArrangeAuthorityGateScenario(fixture, "operational-context-review");
        var generationService = new OperationalContextGenerationServiceStub(CreateOperationalContextProposal(
            fixture.Repository.Id,
            "ctx-created",
            OperationalContextProposalStatus.Pending,
            OperationalContextReviewState.PendingReview,
            null,
            null));
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new FileSystemWorkflowRepository(new MemoryArtifactStore()),
            null,
            generationService);

        WorkflowPreparationEvent preparationEvent = await service.RunPreparationAsync(fixture.Repository.Id);

        Assert.Equal("Refused", preparationEvent.Decision);
        Assert.Equal(WorkflowGateType.OperationalContextReview, preparationEvent.BlockingGate);
        Assert.Equal(0, generationService.CallCount);
        Assert.Empty(preparationEvent.CreatedArtifactIds);
    }

    [Fact]
    public async Task PreparationRunDoesNotInvokeOperationalContextGenerationWhenProposalEvidenceExists()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateResolvedDecision(fixture.Repository.Id, "DEC-0001"));
        fixture.Proposals.Add(CreateOperationalContextProposal(
            fixture.Repository.Id,
            "ctx-0001",
            OperationalContextProposalStatus.Promoted,
            OperationalContextReviewState.Accepted,
            DateTimeOffset.Parse("2026-06-23T11:10:00Z"),
            DateTimeOffset.Parse("2026-06-23T11:20:00Z")));
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        await SaveOperationalContextPreparationTimelineAsync(fixture, workflowRepository);
        var generationService = new OperationalContextGenerationServiceStub(CreateOperationalContextProposal(
            fixture.Repository.Id,
            "ctx-created",
            OperationalContextProposalStatus.Pending,
            OperationalContextReviewState.PendingReview,
            null,
            null));
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository,
            null,
            generationService);

        WorkflowPreparationEvent preparationEvent = await service.RunPreparationAsync(fixture.Repository.Id);

        Assert.Equal("Duplicate", preparationEvent.Decision);
        Assert.Equal(0, generationService.CallCount);
        Assert.Empty(preparationEvent.CreatedArtifactIds);
        Assert.Contains("operational-context-proposal:ctx-0001", preparationEvent.DuplicateEvidence);
    }

    [Fact]
    public async Task PreparationRunDoesNotInvokeOperationalContextGenerationWhenAssimilationEvidenceExists()
    {
        TestFixture fixture = TestFixture.Create();
        Decision decision = CreateResolvedDecision(fixture.Repository.Id, "DEC-0001");
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(decision);
        fixture.AssimilationRecommendations.Add(CreateAssimilationRecommendation(fixture.Repository.Id, decision, "decision-fingerprint"));
        fixture.Proposals.Add(CreateOperationalContextProposal(
            fixture.Repository.Id,
            "ctx-0001",
            OperationalContextProposalStatus.Promoted,
            OperationalContextReviewState.Accepted,
            DateTimeOffset.Parse("2026-06-23T11:10:00Z"),
            DateTimeOffset.Parse("2026-06-23T11:20:00Z")));
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        await SaveOperationalContextPreparationTimelineAsync(fixture, workflowRepository);
        var generationService = new OperationalContextGenerationServiceStub(CreateOperationalContextProposal(
            fixture.Repository.Id,
            "ctx-created",
            OperationalContextProposalStatus.Pending,
            OperationalContextReviewState.PendingReview,
            null,
            null));
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository,
            null,
            generationService);

        WorkflowPreparationEvent preparationEvent = await service.RunPreparationAsync(fixture.Repository.Id);

        Assert.Equal("Duplicate", preparationEvent.Decision);
        Assert.Equal(0, generationService.CallCount);
        Assert.Empty(preparationEvent.CreatedArtifactIds);
        Assert.Contains("operational-context-assimilation", preparationEvent.DuplicateEvidence);
    }

    [Fact]
    public async Task PreparationRunDoesNotInvokeOperationalContextGenerationWhenLinkageEvidenceExists()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateResolvedDecision(fixture.Repository.Id, "DEC-0001"));
        fixture.Proposals.Add(CreateOperationalContextProposal(
            fixture.Repository.Id,
            "ctx-0001",
            OperationalContextProposalStatus.Promoted,
            OperationalContextReviewState.Accepted,
            DateTimeOffset.Parse("2026-06-23T11:10:00Z"),
            DateTimeOffset.Parse("2026-06-23T11:20:00Z")));
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        await SaveOperationalContextPreparationTimelineAsync(fixture, workflowRepository);
        var generationService = new OperationalContextGenerationServiceStub(CreateOperationalContextProposal(
            fixture.Repository.Id,
            "ctx-created",
            OperationalContextProposalStatus.Pending,
            OperationalContextReviewState.PendingReview,
            null,
            null));
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository,
            null,
            generationService);

        WorkflowPreparationEvent preparationEvent = await service.RunPreparationAsync(fixture.Repository.Id);

        Assert.Equal("Duplicate", preparationEvent.Decision);
        Assert.Equal(0, generationService.CallCount);
        Assert.Empty(preparationEvent.CreatedArtifactIds);
        Assert.Contains("operational-context-execution-link", preparationEvent.DuplicateEvidence.Single(evidence =>
            evidence.StartsWith("operational-context-execution-link:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task PreparationRunInvokesOperationalContextGenerationAndPersistsCreatedProposal()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateResolvedDecision(fixture.Repository.Id, "DEC-0001"));
        OperationalContextProposal proposal = CreateOperationalContextProposal(
            fixture.Repository.Id,
            "ctx-created",
            OperationalContextProposalStatus.Pending,
            OperationalContextReviewState.PendingReview,
            null,
            null);
        var generationService = new OperationalContextGenerationServiceStub(proposal, created =>
        {
            fixture.Proposals.Add(created);
        });
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        await SaveOperationalContextPreparationTimelineAsync(fixture, workflowRepository);
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository,
            null,
            generationService);

        WorkflowPreparationEvent preparationEvent = await service.RunPreparationAsync(fixture.Repository.Id);
        IReadOnlyList<WorkflowPreparationEvent> history = await workflowRepository.ListPreparationEventsAsync(fixture.Repository);
        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(1, generationService.CallCount);
        Assert.Equal("Created", preparationEvent.Decision);
        Assert.Contains("operational-context-proposal:ctx-created", preparationEvent.CreatedArtifactIds);
        Assert.Single(history);
        Assert.Equal(WorkflowStage.OperationalContext, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.OperationalContextReview, projection.BlockingGate);
        Assert.All(fixture.Proposals, stored => Assert.NotEqual(OperationalContextProposalStatus.Promoted, stored.Status));
    }

    [Fact]
    public async Task PreparationRunRepeatDoesNotCreateDuplicateOperationalContextProposal()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateResolvedDecision(fixture.Repository.Id, "DEC-0001"));
        OperationalContextProposal proposal = CreateOperationalContextProposal(
            fixture.Repository.Id,
            "ctx-created",
            OperationalContextProposalStatus.Pending,
            OperationalContextReviewState.PendingReview,
            null,
            null);
        var generationService = new OperationalContextGenerationServiceStub(proposal, created =>
        {
            fixture.Proposals.Add(created);
        });
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        await SaveOperationalContextPreparationTimelineAsync(fixture, workflowRepository);
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository,
            null,
            generationService);

        WorkflowPreparationEvent first = await service.RunPreparationAsync(fixture.Repository.Id);
        WorkflowPreparationEvent second = await service.RunPreparationAsync(fixture.Repository.Id);

        Assert.Equal(1, generationService.CallCount);
        Assert.Equal("Created", first.Decision);
        Assert.Equal("Refused", second.Decision);
        Assert.Equal(WorkflowGateType.OperationalContextReview, second.BlockingGate);
        Assert.Single(fixture.Proposals);
        Assert.DoesNotContain(fixture.Proposals, stored => stored.Promotion.PromotedAt is not null);
    }

    [Fact]
    public async Task PreparationEvaluationSkipsCommitPreparationWhenEquivalentDomainEvidenceExists()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = PreparedAcceptedSession();

        WorkflowPreparationEvaluation evaluation =
            await fixture.CreatePreparationService().EvaluatePreparationAsync(fixture.Repository.Id);

        Assert.False(evaluation.CanPrepare);
        Assert.True(evaluation.HasDuplicateDomainEvidence);
        Assert.Equal("Duplicate", evaluation.Outcome);
        Assert.Contains("commit-preparation:snapshot-0001", evaluation.DuplicateEvidence);
    }

    [Fact]
    public async Task PreparationRunDoesNotInvokeCommitPreparationWhenNonCommitGateIsOpen()
    {
        TestFixture fixture = TestFixture.Create();
        ArrangeAuthorityGateScenario(fixture, "decision-resolution");
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new FileSystemWorkflowRepository(new MemoryArtifactStore()),
            null,
            null,
            new ExecutionSessionServiceStub(fixture));

        WorkflowPreparationEvent preparationEvent = await service.RunPreparationAsync(fixture.Repository.Id);

        Assert.Equal("Refused", preparationEvent.Decision);
        Assert.Equal(WorkflowGateType.DecisionResolution, preparationEvent.BlockingGate);
        Assert.Equal(0, fixture.PrepareCommitCallCount);
        Assert.Equal(0, fixture.CommitCallCount);
        Assert.Equal(0, fixture.PushCallCount);
        Assert.Empty(preparationEvent.CreatedArtifactIds);
    }

    [Fact]
    public async Task PreparationRunDoesNotInvokeCommitPreparationWhenPreparedSnapshotExists()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = PreparedAwaitingCommitSession();
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new FileSystemWorkflowRepository(new MemoryArtifactStore()),
            null,
            null,
            new ExecutionSessionServiceStub(fixture));

        WorkflowPreparationEvent preparationEvent = await service.RunPreparationAsync(fixture.Repository.Id);

        Assert.Equal("Duplicate", preparationEvent.Decision);
        Assert.Equal(0, fixture.PrepareCommitCallCount);
        Assert.Empty(preparationEvent.CreatedArtifactIds);
        Assert.Contains("commit-preparation:snapshot-0001", preparationEvent.DuplicateEvidence);
    }

    [Fact]
    public async Task PreparationRunInvokesExecutionCommitPreparationAndPreservesCommitGate()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository,
            null,
            null,
            new ExecutionSessionServiceStub(fixture));

        WorkflowPreparationEvent preparationEvent = await service.RunPreparationAsync(fixture.Repository.Id);
        IReadOnlyList<WorkflowPreparationEvent> history = await workflowRepository.ListPreparationEventsAsync(fixture.Repository);
        WorkflowInstance projection = await fixture.CreateService().ProjectAsync(fixture.Repository.Id);

        Assert.Equal(1, fixture.PrepareCommitCallCount);
        Assert.Equal(0, fixture.CommitCallCount);
        Assert.Equal(0, fixture.PushCallCount);
        Assert.Equal("Created", preparationEvent.Decision);
        Assert.Contains("commit-preparation:snapshot-prepared", preparationEvent.CreatedArtifactIds);
        Assert.Single(history);
        Assert.Equal(WorkflowStage.Commit, projection.CurrentStage);
        Assert.Equal(WorkflowGateType.CommitApproval, projection.BlockingGate);
        Assert.Equal("snapshot-prepared", fixture.Session?.PreparationSnapshotId);
        Assert.Null(fixture.Session?.CommitSha);
        Assert.Null(fixture.Session?.PushedCommitSha);
    }

    [Fact]
    public async Task PreparationRunRepeatDoesNotCreateDuplicateCommitPreparation()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        var service = new WorkflowPreparationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            workflowRepository,
            null,
            null,
            new ExecutionSessionServiceStub(fixture));

        WorkflowPreparationEvent first = await service.RunPreparationAsync(fixture.Repository.Id);
        WorkflowPreparationEvent second = await service.RunPreparationAsync(fixture.Repository.Id);

        Assert.Equal(1, fixture.PrepareCommitCallCount);
        Assert.Equal("Created", first.Decision);
        Assert.Equal("Duplicate", second.Decision);
        Assert.Contains("commit-preparation:snapshot-prepared", second.DuplicateEvidence);
    }

    [Fact]
    public async Task ContinuationRunDoesNotDuplicateIdenticalFingerprint()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var service = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);

        WorkflowContinuationEvent first = await service.RunContinuationAsync(fixture.Repository.Id);
        WorkflowContinuationEvent second = await service.RunContinuationAsync(fixture.Repository.Id);
        IReadOnlyList<WorkflowContinuationEvent> history = await workflowRepository.ListContinuationEventsAsync(fixture.Repository);

        Assert.Equal(first.EventId, second.EventId);
        Assert.Equal(first.InputFingerprint, second.InputFingerprint);
        Assert.Single(history);
    }

    [Fact]
    public async Task ContinuationRunAdvancesOnePersistedStageWhenDomainEvidenceIsAhead()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingAcceptance;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingAcceptance);
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var service = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
                WorkflowStage.Execution,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.WorkSelection,
                []));

        WorkflowContinuationEvent continuationEvent = await service.RunContinuationAsync(fixture.Repository.Id);
        WorkflowTimeline? latest = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.Equal("Advance", continuationEvent.Decision);
        Assert.Equal(WorkflowStage.Execution, continuationEvent.FromStage);
        Assert.Equal(WorkflowStage.Handoff, continuationEvent.ToStage);
        Assert.NotNull(latest);
        Assert.Equal(WorkflowStage.Handoff, latest.CurrentStage);
        Assert.Equal(WorkflowStage.Execution, latest.PreviousStage);
        Assert.Equal(WorkflowGateType.ExecutionAcceptance, latest.BlockingGate);
    }

    [Fact]
    public async Task ContinuationRunDoesNotDuplicateAlreadyAppliedProgression()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingAcceptance;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingAcceptance);
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var service = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
                WorkflowStage.Execution,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.WorkSelection,
                []));

        WorkflowContinuationEvent first = await service.RunContinuationAsync(fixture.Repository.Id);
        WorkflowContinuationEvent second = await service.RunContinuationAsync(fixture.Repository.Id);
        IReadOnlyList<WorkflowContinuationEvent> history = await workflowRepository.ListContinuationEventsAsync(fixture.Repository);
        WorkflowTimeline? latest = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.Equal("Advance", first.Decision);
        Assert.Equal("Stop", second.Decision);
        Assert.Equal(2, history.Count);
        Assert.NotNull(latest);
        Assert.Equal(WorkflowStage.Handoff, latest.CurrentStage);
        Assert.Equal(WorkflowGateType.ExecutionAcceptance, second.BlockingGate);
    }

    [Fact]
    public async Task ContinuationRunAdvancesAcceptedHandoffToDecisionWhenDomainEvidenceIsAhead()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Candidates.Add(CreateCandidate(fixture.Repository.Id, "cand-0001"));
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var service = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T10:20:00Z"),
                WorkflowStage.Handoff,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.Execution,
                []));

        WorkflowContinuationEvent continuationEvent = await service.RunContinuationAsync(fixture.Repository.Id);
        WorkflowTimeline? latest = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.Equal("Advance", continuationEvent.Decision);
        Assert.Equal(WorkflowStage.Handoff, continuationEvent.FromStage);
        Assert.Equal(WorkflowStage.Decision, continuationEvent.ToStage);
        Assert.NotNull(latest);
        Assert.Equal(WorkflowStage.Decision, latest.CurrentStage);
        Assert.Equal(WorkflowStage.Handoff, latest.PreviousStage);
        Assert.Equal(WorkflowGateType.DecisionResolution, latest.BlockingGate);
    }

    [Fact]
    public async Task ContinuationRunAdvancesResolvedDecisionToOperationalContextWhenDomainEvidenceIsAhead()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateResolvedDecision(fixture.Repository.Id, "DEC-0001"));
        fixture.Proposals.Add(CreateOperationalContextProposal(
            fixture.Repository.Id,
            "ctx-0001",
            OperationalContextProposalStatus.Pending,
            OperationalContextReviewState.PendingReview,
            null,
            null));
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var service = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T10:40:00Z"),
                WorkflowStage.Decision,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.Handoff,
                []));

        WorkflowContinuationEvent continuationEvent = await service.RunContinuationAsync(fixture.Repository.Id);
        WorkflowTimeline? latest = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.Equal("Advance", continuationEvent.Decision);
        Assert.Equal(WorkflowStage.Decision, continuationEvent.FromStage);
        Assert.Equal(WorkflowStage.OperationalContext, continuationEvent.ToStage);
        Assert.NotNull(latest);
        Assert.Equal(WorkflowStage.OperationalContext, latest.CurrentStage);
        Assert.Equal(WorkflowStage.Decision, latest.PreviousStage);
        Assert.Equal(WorkflowGateType.OperationalContextReview, latest.BlockingGate);
    }

    [Fact]
    public async Task ContinuationRunAdvancesCompletedOperationalContextToCommitWhenDomainEvidenceIsAhead()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);
        fixture.Decisions.Add(CreateResolvedDecision(fixture.Repository.Id, "DEC-0001"));
        fixture.Proposals.Add(CreateOperationalContextProposal(
            fixture.Repository.Id,
            "ctx-0001",
            OperationalContextProposalStatus.Promoted,
            OperationalContextReviewState.Accepted,
            DateTimeOffset.Parse("2026-06-23T11:10:00Z"),
            DateTimeOffset.Parse("2026-06-23T11:20:00Z")));
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var service = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T11:25:00Z"),
                WorkflowStage.OperationalContext,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.Decision,
                []));

        WorkflowContinuationEvent continuationEvent = await service.RunContinuationAsync(fixture.Repository.Id);
        WorkflowTimeline? latest = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.Equal("Advance", continuationEvent.Decision);
        Assert.Equal(WorkflowStage.OperationalContext, continuationEvent.FromStage);
        Assert.Equal(WorkflowStage.Commit, continuationEvent.ToStage);
        Assert.NotNull(latest);
        Assert.Equal(WorkflowStage.Commit, latest.CurrentStage);
        Assert.Equal(WorkflowStage.OperationalContext, latest.PreviousStage);
        Assert.Equal(WorkflowGateType.CommitApproval, latest.BlockingGate);
    }

    [Fact]
    public async Task ContinuationRunAdvancesCommittedWorkflowToPushWhenDomainEvidenceIsAhead()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = AwaitingPushCommittedSession();
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var service = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T12:01:00Z"),
                WorkflowStage.Commit,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.OperationalContext,
                []));

        WorkflowContinuationEvent continuationEvent = await service.RunContinuationAsync(fixture.Repository.Id);
        WorkflowTimeline? latest = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.Equal("Advance", continuationEvent.Decision);
        Assert.Equal(WorkflowStage.Commit, continuationEvent.FromStage);
        Assert.Equal(WorkflowStage.Push, continuationEvent.ToStage);
        Assert.NotNull(latest);
        Assert.Equal(WorkflowStage.Push, latest.CurrentStage);
        Assert.Equal(WorkflowStage.Commit, latest.PreviousStage);
        Assert.Equal(WorkflowGateType.PushApproval, latest.BlockingGate);
    }

    [Fact]
    public async Task ContinuationRunAdvancesPushedWorkflowToCompletedWhenDomainEvidenceIsAhead()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = PushedSession();
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var service = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T12:11:00Z"),
                WorkflowStage.Push,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.Commit,
                []));

        WorkflowContinuationEvent continuationEvent = await service.RunContinuationAsync(fixture.Repository.Id);
        WorkflowTimeline? latest = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.Equal("Advance", continuationEvent.Decision);
        Assert.Equal(WorkflowStage.Push, continuationEvent.FromStage);
        Assert.Equal(WorkflowStage.Completed, continuationEvent.ToStage);
        Assert.True(continuationEvent.IsComplete);
        Assert.NotNull(latest);
        Assert.Equal(WorkflowStage.Completed, latest.CurrentStage);
        Assert.Equal(WorkflowStage.Push, latest.PreviousStage);
        Assert.Equal(WorkflowGateType.WorkSelection, latest.BlockingGate);
    }

    [Fact]
    public async Task ContinuationRunAdvancesNoChangeWorkflowToCompletedWhenDomainEvidenceIsAhead()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var service = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T12:11:00Z"),
                WorkflowStage.Push,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.Commit,
                []));

        WorkflowContinuationEvent continuationEvent = await service.RunContinuationAsync(fixture.Repository.Id);
        WorkflowTimeline? latest = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.Equal("Advance", continuationEvent.Decision);
        Assert.Equal(WorkflowStage.Push, continuationEvent.FromStage);
        Assert.Equal(WorkflowStage.Completed, continuationEvent.ToStage);
        Assert.True(continuationEvent.IsComplete);
        Assert.NotNull(latest);
        Assert.Equal(WorkflowStage.Completed, latest.CurrentStage);
        Assert.Equal(WorkflowGateType.WorkSelection, latest.BlockingGate);
        Assert.Contains(continuationEvent.Diagnostics, diagnostic => diagnostic.Contains("no changes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ContinuationRunStopsAtWorkSelectionAfterCompletionProgression()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = PushedSession();
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var service = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T12:11:00Z"),
                WorkflowStage.Push,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.Commit,
                []));

        WorkflowContinuationEvent first = await service.RunContinuationAsync(fixture.Repository.Id);
        WorkflowContinuationEvent second = await service.RunContinuationAsync(fixture.Repository.Id);
        IReadOnlyList<WorkflowContinuationEvent> history = await workflowRepository.ListContinuationEventsAsync(fixture.Repository);
        WorkflowTimeline? latest = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.Equal("Advance", first.Decision);
        Assert.Equal("Stop", second.Decision);
        Assert.Equal(WorkflowGateType.WorkSelection, second.BlockingGate);
        Assert.True(second.IsWaitingForHuman);
        Assert.Contains("WorkSelection", second.Reason, StringComparison.Ordinal);
        Assert.Equal(2, history.Count);
        Assert.NotNull(latest);
        Assert.Equal(WorkflowStage.Completed, latest.CurrentStage);
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
    public async Task WorkflowExecutionEndpointReturnsExecutionProjection()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingAcceptance;
        fixture.Session = new ExecutionSessionSummary
        {
            SessionId = Guid.NewGuid(),
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.AwaitingAcceptance,
            StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
            HandoffPath = ".agents/handoffs/handoff.md"
        };
        await using WebApplication app = Program.CreateApp(
            [],
            services => fixture.ReplaceServices(services));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/execution");
        WorkflowExecutionProjection? execution = await response.Content.ReadFromJsonAsync<WorkflowExecutionProjection>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(execution);
        Assert.Equal(WorkflowExecutionStatus.AwaitingAcceptance, execution.Status);
        Assert.True(execution.HasHandoff);
    }

    [Fact]
    public async Task WorkflowHandoffServiceProjectsPendingAcceptedRejectedMissingAndInvalidStates()
    {
        TestFixture fixture = TestFixture.Create();
        Guid sessionId = Guid.NewGuid();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingAcceptance;
        fixture.Session = new ExecutionSessionSummary
        {
            SessionId = sessionId,
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.AwaitingAcceptance,
            StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
            HandoffPath = ".agents/handoffs/handoff.md"
        };

        WorkflowHandoffProjection pending = await fixture.CreateHandoffService().ProjectHandoffAsync(fixture.Repository.Id);
        Assert.Equal(WorkflowHandoffStatus.Pending, pending.Status);
        Assert.True(pending.Validation.IsValid);

        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = new ExecutionSessionSummary
        {
            SessionId = sessionId,
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.Accepted,
            StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
            HandoffPath = ".agents/handoffs/handoff.md",
            AcceptedAt = DateTimeOffset.Parse("2026-06-23T10:15:00Z")
        };
        WorkflowHandoffProjection accepted = await fixture.CreateHandoffService().ProjectHandoffAsync(fixture.Repository.Id);
        Assert.Equal(WorkflowHandoffStatus.Accepted, accepted.Status);

        fixture.ExecutionState = RepositoryExecutionState.Ready;
        fixture.Session = new ExecutionSessionSummary
        {
            SessionId = sessionId,
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.Ready,
            StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
            HandoffPath = ".agents/handoffs/handoff.md",
            RejectedAt = DateTimeOffset.Parse("2026-06-23T10:16:00Z")
        };
        WorkflowHandoffProjection rejected = await fixture.CreateHandoffService().ProjectHandoffAsync(fixture.Repository.Id);
        Assert.Equal(WorkflowHandoffStatus.Rejected, rejected.Status);

        fixture.Session = new ExecutionSessionSummary
        {
            SessionId = sessionId,
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.Ready,
            StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z")
        };
        WorkflowHandoffProjection missing = await fixture.CreateHandoffService().ProjectHandoffAsync(fixture.Repository.Id);
        Assert.Equal(WorkflowHandoffStatus.Missing, missing.Status);

        fixture.Session = new ExecutionSessionSummary
        {
            SessionId = sessionId,
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.AwaitingAcceptance,
            StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
            HandoffPath = ".agents/handoffs/handoff.md"
        };
        fixture.HandoffContent = "";
        WorkflowHandoffProjection invalid = await fixture.CreateHandoffService().ProjectHandoffAsync(fixture.Repository.Id);
        Assert.Equal(WorkflowHandoffStatus.Invalid, invalid.Status);
        Assert.False(invalid.Validation.IsValid);
    }

    [Fact]
    public async Task WorkflowHandoffEndpointReturnsHandoffProjection()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingAcceptance;
        fixture.Session = new ExecutionSessionSummary
        {
            SessionId = Guid.NewGuid(),
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.AwaitingAcceptance,
            StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
            HandoffPath = ".agents/handoffs/handoff.md"
        };
        await using WebApplication app = Program.CreateApp(
            [],
            services => fixture.ReplaceServices(services));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/handoff");
        WorkflowHandoffProjection? handoff = await response.Content.ReadFromJsonAsync<WorkflowHandoffProjection>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(handoff);
        Assert.Equal(WorkflowHandoffStatus.Pending, handoff.Status);
        Assert.Equal(".agents/handoffs/handoff.md", handoff.HandoffPath);
    }

    [Fact]
    public async Task WorkflowDecisionEndpointReturnsDecisionProjection()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateDecision(fixture.Repository.Id, "DEC-0001", DecisionState.UnderReview));
        await using WebApplication app = Program.CreateApp(
            [],
            services => fixture.ReplaceServices(services));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/decisions");
        WorkflowDecisionProjection? decision = await response.Content.ReadFromJsonAsync<WorkflowDecisionProjection>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(decision);
        Assert.Equal("DEC-0001", decision.DecisionId);
        Assert.Equal(WorkflowDecisionStatus.UnderReview, decision.Status);
    }

    [Fact]
    public async Task WorkflowOperationalContextEndpointReturnsContextProjection()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Proposals.Add(new OperationalContextProposal
        {
            ProposalId = "ctx-0001",
            RepositoryId = fixture.Repository.Id,
            GeneratedAt = DateTimeOffset.Parse("2026-06-23T11:00:00Z"),
            Status = OperationalContextProposalStatus.Accepted,
            Review = new OperationalContextReview
            {
                ProposalId = "ctx-0001",
                ReviewState = OperationalContextReviewState.Accepted,
                ReviewedAt = DateTimeOffset.Parse("2026-06-23T11:10:00Z")
            }
        });
        await using WebApplication app = Program.CreateApp(
            [],
            services => fixture.ReplaceServices(services));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{fixture.Repository.Id}/workflow/operational-context");
        WorkflowOperationalContextProjection? context = await response.Content.ReadFromJsonAsync<WorkflowOperationalContextProjection>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(context);
        Assert.Equal("ctx-0001", context.ProposalId);
        Assert.Equal(WorkflowOperationalContextStatus.ReadyForPromotion, context.Status);
        Assert.False(context.IsCommitEligible);
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
    public async Task WorkflowRepositorySavesLoadsAndListsContinuationEvents()
    {
        TestFixture fixture = TestFixture.Create();
        var store = new MemoryArtifactStore();
        var repository = new FileSystemWorkflowRepository(store);
        var continuationEvent = new WorkflowContinuationEvent(
            fixture.Repository.Id,
            "continuation.20260623T110000.0000000Z",
            DateTimeOffset.Parse("2026-06-23T11:00:00Z"),
            "endpoint",
            WorkflowStage.Commit,
            WorkflowStage.Push,
            WorkflowProgressState.Ready,
            WorkflowGateType.None,
            "Advance",
            "Transition Commit -> Push can advance mechanically.",
            new WorkflowContinuationFingerprint("fingerprint"),
            false,
            false,
            "No human action required.",
            ["diagnostic"]);

        await repository.SaveContinuationEventAsync(fixture.Repository, continuationEvent);

        WorkflowContinuationEvent? loaded = await repository.LoadContinuationEventAsync(fixture.Repository, continuationEvent.EventId);
        IReadOnlyList<WorkflowContinuationEvent> listed = await repository.ListContinuationEventsAsync(fixture.Repository);

        Assert.NotNull(loaded);
        Assert.Equal(continuationEvent.InputFingerprint, loaded.InputFingerprint);
        WorkflowContinuationEvent persisted = Assert.Single(listed);
        Assert.Equal(continuationEvent.EventId, persisted.EventId);
        Assert.True(await store.ExistsAsync(WorkflowArtifactPaths.Resolve(fixture.Repository, WorkflowArtifactPaths.ContinuationMarkdown(continuationEvent.EventId))));
    }

    [Fact]
    public async Task WorkflowRepositorySavesLoadsAndListsPreparationEvents()
    {
        TestFixture fixture = TestFixture.Create();
        var store = new MemoryArtifactStore();
        var repository = new FileSystemWorkflowRepository(store);
        var preparationEvent = new WorkflowPreparationEvent(
            fixture.Repository.Id,
            "preparation.20260623T110000.0000000Z",
            DateTimeOffset.Parse("2026-06-23T11:00:00Z"),
            "endpoint",
            WorkflowStage.Commit,
            WorkflowProgressState.AwaitingGate,
            WorkflowGateType.CommitApproval,
            WorkflowPreparationCommand.PrepareExecutionCommit,
            "execution_prepare_commit",
            "Refused",
            "Preparation refused because CommitApproval is awaiting human action.",
            new WorkflowPreparationFingerprint("fingerprint"),
            true,
            false,
            [],
            [],
            ["diagnostic"]);

        await repository.SavePreparationEventAsync(fixture.Repository, preparationEvent);

        WorkflowPreparationEvent? loaded = await repository.LoadPreparationEventAsync(fixture.Repository, preparationEvent.EventId);
        IReadOnlyList<WorkflowPreparationEvent> listed = await repository.ListPreparationEventsAsync(fixture.Repository);

        Assert.NotNull(loaded);
        Assert.Equal(preparationEvent.InputFingerprint, loaded.InputFingerprint);
        WorkflowPreparationEvent persisted = Assert.Single(listed);
        Assert.Equal(preparationEvent.EventId, persisted.EventId);
        Assert.True(await store.ExistsAsync(WorkflowArtifactPaths.Resolve(fixture.Repository, WorkflowArtifactPaths.PreparationMarkdown(preparationEvent.EventId))));
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
    public async Task RecoveryRebuildsDecisionWorkflowState()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateDecision(fixture.Repository.Id, "DEC-0001", DecisionState.UnderReview));
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var recovery = fixture.CreateRecoveryService(workflowRepository);

        WorkflowRecoveryResult result = await recovery.RecoverCurrentWorkflowAsync(fixture.Repository.Id);

        Assert.True(result.Diagnostics.Rebuilt);
        Assert.Equal(WorkflowStage.Decision, result.Timeline.CurrentStage);
        Assert.Contains(result.Timeline.Entries, entry => entry.EventType == WorkflowTimelineEventType.DecisionReviewed);
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
    public async Task RecoveryRebuildsOperationalContextTimelineEvidence()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Proposals.Add(new OperationalContextProposal
        {
            ProposalId = "ctx-0001",
            RepositoryId = fixture.Repository.Id,
            GeneratedAt = DateTimeOffset.Parse("2026-06-23T11:00:00Z"),
            Status = OperationalContextProposalStatus.Promoted,
            Review = new OperationalContextReview
            {
                ProposalId = "ctx-0001",
                ReviewState = OperationalContextReviewState.Accepted,
                ReviewedAt = DateTimeOffset.Parse("2026-06-23T11:10:00Z")
            },
            Promotion = new OperationalContextPromotion
            {
                ProposalId = "ctx-0001",
                PromotedAt = DateTimeOffset.Parse("2026-06-23T11:20:00Z")
            }
        });
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());

        WorkflowRecoveryResult recovered = await fixture.CreateRecoveryService(workflowRepository).RecoverCurrentWorkflowAsync(fixture.Repository.Id);

        Assert.Contains(recovered.Timeline.Entries, entry => entry.EventType == WorkflowTimelineEventType.OperationalContextAccepted);
        Assert.Contains(recovered.Timeline.Entries, entry => entry.EventType == WorkflowTimelineEventType.OperationalContextPromoted);
        Assert.Contains(recovered.Diagnostics.Diagnostics, diagnostic => diagnostic.Contains("rebuilt timeline", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecoveryRebuildsCompletedWorkflowFromDomainEvidenceWithoutPersistedStage()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = PushedSession();
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());

        WorkflowRecoveryResult recovered = await fixture.CreateRecoveryService(workflowRepository).RecoverCurrentWorkflowAsync(fixture.Repository.Id);

        Assert.True(recovered.Diagnostics.Rebuilt);
        Assert.Equal(WorkflowStage.Completed, recovered.Timeline.CurrentStage);
        Assert.Equal(WorkflowGateType.WorkSelection, recovered.Timeline.BlockingGate);
        Assert.Contains(recovered.Timeline.Entries, entry => entry.EventType == WorkflowTimelineEventType.PushExecuted);
    }

    [Fact]
    public async Task RecoveryLetsDomainProjectionWinOverStalePersistedTimeline()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = PushedSession();
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T12:05:00Z"),
                WorkflowStage.Commit,
                WorkflowProgressState.AwaitingGate,
                WorkflowGateType.CommitApproval,
                WorkflowStage.OperationalContext,
                [WorkflowBlockingCondition.PendingCommitApproval]));

        WorkflowRecoveryResult recovered = await fixture.CreateRecoveryService(workflowRepository).RecoverCurrentWorkflowAsync(fixture.Repository.Id);
        WorkflowTimeline? latest = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.True(recovered.Diagnostics.Rebuilt);
        Assert.False(recovered.Diagnostics.PersistedEvidenceMatchedDomain);
        Assert.Equal(WorkflowStage.Completed, recovered.Timeline.CurrentStage);
        Assert.Equal(WorkflowGateType.WorkSelection, recovered.Timeline.BlockingGate);
        Assert.Contains(recovered.Diagnostics.DiscardedArtifacts, artifact => !string.IsNullOrWhiteSpace(artifact));
        Assert.NotNull(latest);
        Assert.Equal(recovered.Timeline.Fingerprint, latest.Fingerprint);
    }

    [Fact]
    public async Task ContinuationAfterRestartDoesNotDuplicateCompletedStopEvent()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
        fixture.Session = PushedSession();
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        var firstService = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);
        await workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T12:11:00Z"),
                WorkflowStage.Push,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.Commit,
                []));
        await firstService.RunContinuationAsync(fixture.Repository.Id);
        WorkflowContinuationEvent completedStop = await firstService.RunContinuationAsync(fixture.Repository.Id);
        await fixture.CreateRecoveryService(workflowRepository).RecoverCurrentWorkflowAsync(fixture.Repository.Id);
        var restartedService = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);

        WorkflowContinuationEvent restartedStop = await restartedService.RunContinuationAsync(fixture.Repository.Id);
        IReadOnlyList<WorkflowContinuationEvent> history = await workflowRepository.ListContinuationEventsAsync(fixture.Repository);

        Assert.Equal(completedStop.EventId, restartedStop.EventId);
        Assert.Equal(2, history.Count);
        Assert.Equal("Stop", restartedStop.Decision);
        Assert.Equal(WorkflowStage.Completed, restartedStop.FromStage);
        Assert.Equal(WorkflowGateType.WorkSelection, restartedStop.BlockingGate);
    }

    [Fact]
    public async Task RecoveryRebuildsNoChangeCompletionAndContinuationStopsAtWorkSelection()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);

        WorkflowRecoveryResult recovered = await fixture.CreateRecoveryService(workflowRepository).RecoverCurrentWorkflowAsync(fixture.Repository.Id);
        var continuationService = new WorkflowContinuationService(
            new RepositoryServiceStub(fixture.Repository),
            fixture.CreateService(),
            new WorkflowStateMachineService(),
            workflowRepository);
        WorkflowContinuationEvent continuationEvent = await continuationService.RunContinuationAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.Completed, recovered.Timeline.CurrentStage);
        Assert.Equal(WorkflowGateType.WorkSelection, recovered.Timeline.BlockingGate);
        Assert.Equal("Stop", continuationEvent.Decision);
        Assert.Equal(WorkflowStage.Completed, continuationEvent.FromStage);
        Assert.Equal(WorkflowGateType.WorkSelection, continuationEvent.BlockingGate);
        Assert.True(continuationEvent.IsWaitingForHuman);
        Assert.Contains(continuationEvent.Diagnostics, diagnostic => diagnostic.Contains("no changes", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public async Task HostedContinuationDisabledConfigDoesNotRun()
    {
        Repository repository = CreateRepository("repo-1");
        var continuationService = new HostedContinuationServiceStub();
        var preparationService = new HostedPreparationServiceStub();
        var hosted = new WorkflowContinuationHostedService(
            new RepositoryServiceStub(repository),
            continuationService,
            preparationService,
            Options.Create(new WorkflowContinuationOptions
            {
                ContinuationEnabled = false,
                ContinuationIntervalSeconds = 3600
            }));

        await hosted.StartAsync(CancellationToken.None);

        Assert.Empty(continuationService.CalledRepositoryIds);
        Assert.Empty(preparationService.CalledRepositoryIds);
    }

    [Fact]
    public async Task HostedContinuationEnabledConfigRunsContinuationAndPreparationOnce()
    {
        Repository repository = CreateRepository("repo-1");
        var continuationService = new HostedContinuationServiceStub();
        var preparationService = new HostedPreparationServiceStub();
        var hosted = new WorkflowContinuationHostedService(
            new RepositoryServiceStub(repository),
            continuationService,
            preparationService,
            Options.Create(new WorkflowContinuationOptions
            {
                ContinuationEnabled = true,
                ContinuationIntervalSeconds = 3600
            }));

        await hosted.StartAsync(CancellationToken.None);
        await hosted.StopAsync(CancellationToken.None);

        Assert.Equal([repository.Id], continuationService.CalledRepositoryIds);
        Assert.Equal([repository.Id], preparationService.CalledRepositoryIds);
        Assert.Equal(["hosted"], continuationService.Triggers);
        Assert.Equal(["hosted"], preparationService.Triggers);
    }

    [Fact]
    public async Task HostedContinuationRestartDoesNotDuplicateContinuationOrPreparationEvidence()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
        fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        var firstHosted = new WorkflowContinuationHostedService(
            new RepositoryServiceStub(fixture.Repository),
            new WorkflowContinuationService(
                new RepositoryServiceStub(fixture.Repository),
                fixture.CreateService(),
                new WorkflowStateMachineService(),
                workflowRepository),
            new WorkflowPreparationService(
                new RepositoryServiceStub(fixture.Repository),
                fixture.CreateService(),
                workflowRepository,
                null,
                null,
                new ExecutionSessionServiceStub(fixture)),
            Options.Create(new WorkflowContinuationOptions
            {
                ContinuationEnabled = true,
                ContinuationIntervalSeconds = 3600
            }));
        var restartedHosted = new WorkflowContinuationHostedService(
            new RepositoryServiceStub(fixture.Repository),
            new WorkflowContinuationService(
                new RepositoryServiceStub(fixture.Repository),
                fixture.CreateService(),
                new WorkflowStateMachineService(),
                workflowRepository),
            new WorkflowPreparationService(
                new RepositoryServiceStub(fixture.Repository),
                fixture.CreateService(),
                workflowRepository,
                null,
                null,
                new ExecutionSessionServiceStub(fixture)),
            Options.Create(new WorkflowContinuationOptions
            {
                ContinuationEnabled = true,
                ContinuationIntervalSeconds = 3600
            }));

        await firstHosted.RunOnceAsync(CancellationToken.None);
        await restartedHosted.RunOnceAsync(CancellationToken.None);

        IReadOnlyList<WorkflowContinuationEvent> continuationHistory =
            await workflowRepository.ListContinuationEventsAsync(fixture.Repository);
        IReadOnlyList<WorkflowPreparationEvent> preparationHistory =
            await workflowRepository.ListPreparationEventsAsync(fixture.Repository);
        WorkflowTimeline? latestTimeline = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.Single(continuationHistory);
        Assert.Single(preparationHistory);
        Assert.Equal(1, fixture.PrepareCommitCallCount);
        Assert.Equal(0, fixture.CommitCallCount);
        Assert.Equal(0, fixture.PushCallCount);
        Assert.Null(latestTimeline);
    }

    [Fact]
    public async Task HostedContinuationOpenGateStopsProgressionWithoutAuthorityAction()
    {
        TestFixture fixture = TestFixture.Create();
        ArrangeAuthorityGateScenario(fixture, "decision-resolution");
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        var hosted = new WorkflowContinuationHostedService(
            new RepositoryServiceStub(fixture.Repository),
            new WorkflowContinuationService(
                new RepositoryServiceStub(fixture.Repository),
                fixture.CreateService(),
                new WorkflowStateMachineService(),
                workflowRepository),
            new WorkflowPreparationService(
                new RepositoryServiceStub(fixture.Repository),
                fixture.CreateService(),
                workflowRepository,
                new DecisionDiscoveryServiceStub([]),
                null,
                new ExecutionSessionServiceStub(fixture)),
            Options.Create(new WorkflowContinuationOptions
            {
                ContinuationEnabled = true,
                ContinuationIntervalSeconds = 3600
            }));

        await hosted.RunOnceAsync(CancellationToken.None);

        WorkflowContinuationEvent continuationEvent = Assert.Single(
            await workflowRepository.ListContinuationEventsAsync(fixture.Repository));
        WorkflowPreparationEvent preparationEvent = Assert.Single(
            await workflowRepository.ListPreparationEventsAsync(fixture.Repository));
        WorkflowTimeline? latestTimeline = await workflowRepository.GetLatestTimelineAsync(fixture.Repository);

        Assert.Equal("Stop", continuationEvent.Decision);
        Assert.True(continuationEvent.IsWaitingForHuman);
        Assert.Equal(WorkflowGateType.DecisionResolution, continuationEvent.BlockingGate);
        Assert.Equal("Refused", preparationEvent.Decision);
        Assert.Null(latestTimeline);
        Decision existingDecision = Assert.Single(fixture.Decisions);
        Assert.Equal(DecisionState.Open, existingDecision.State);
        Assert.Equal(0, fixture.CommitCallCount);
        Assert.Equal(0, fixture.PushCallCount);
    }

    [Fact]
    public async Task HostedContinuationRepositoryFailureDoesNotBlockOtherRepositories()
    {
        Repository failingRepository = CreateRepository("repo-failing");
        Repository healthyRepository = CreateRepository("repo-healthy");
        var continuationService = new HostedContinuationServiceStub(failingRepository.Id);
        var preparationService = new HostedPreparationServiceStub();
        var hosted = new WorkflowContinuationHostedService(
            new RepositoryServiceStub(failingRepository, healthyRepository),
            continuationService,
            preparationService,
            Options.Create(new WorkflowContinuationOptions
            {
                ContinuationEnabled = true,
                ContinuationIntervalSeconds = 3600
            }));

        await hosted.RunOnceAsync(CancellationToken.None);

        Assert.Equal([failingRepository.Id, healthyRepository.Id], continuationService.CalledRepositoryIds);
        Assert.Equal([healthyRepository.Id], preparationService.CalledRepositoryIds);
    }

    private static ExecutionSessionSummary CompletedAcceptedSession(
        RepositoryExecutionState repositoryState = RepositoryExecutionState.Accepted) => new()
    {
        SessionId = Guid.NewGuid(),
        State = ExecutionSessionState.Completed,
        RepositoryState = repositoryState,
        StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
        CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
        AcceptedAt = DateTimeOffset.Parse("2026-06-23T10:15:00Z"),
        HandoffPath = ".agents/handoffs/handoff.md"
    };

    private static Repository CreateRepository(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Path = $"C:\\{name}"
    };

    private static ExecutionSessionSummary AwaitingPushCommittedSession() => new()
    {
        SessionId = Guid.NewGuid(),
        State = ExecutionSessionState.Completed,
        RepositoryState = RepositoryExecutionState.AwaitingPush,
        StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
        CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
        AcceptedAt = DateTimeOffset.Parse("2026-06-23T10:15:00Z"),
        HandoffPath = ".agents/handoffs/handoff.md",
        CommittedAt = DateTimeOffset.Parse("2026-06-23T12:00:00Z"),
        CommitSha = "abc123"
    };

    private static ExecutionSessionSummary PreparedAcceptedSession() => new()
    {
        SessionId = Guid.NewGuid(),
        State = ExecutionSessionState.Completed,
        RepositoryState = RepositoryExecutionState.Accepted,
        StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
        CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
        AcceptedAt = DateTimeOffset.Parse("2026-06-23T10:15:00Z"),
        HandoffPath = ".agents/handoffs/handoff.md",
        PreparationSnapshotId = "snapshot-0001"
    };

    private static ExecutionSessionSummary PreparedAwaitingCommitSession() => new()
    {
        SessionId = Guid.NewGuid(),
        State = ExecutionSessionState.Completed,
        RepositoryState = RepositoryExecutionState.AwaitingCommit,
        StartedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z"),
        CompletedAt = DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
        AcceptedAt = DateTimeOffset.Parse("2026-06-23T10:15:00Z"),
        HandoffPath = ".agents/handoffs/handoff.md",
        PreparationSnapshotId = "snapshot-0001"
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
        HandoffPath = ".agents/handoffs/handoff.md",
        CommittedAt = DateTimeOffset.Parse("2026-06-23T12:00:00Z"),
        CommitSha = "abc123",
        PushedAt = DateTimeOffset.Parse("2026-06-23T12:10:00Z"),
        PushedCommitSha = "abc123"
    };

    public static IEnumerable<object[]> AuthorityGatePreparationCases()
    {
        yield return [WorkflowGateType.WorkSelection, "work-selection"];
        yield return [WorkflowGateType.ExecutionAcceptance, "execution-acceptance"];
        yield return [WorkflowGateType.DecisionResolution, "decision-resolution"];
        yield return [WorkflowGateType.OperationalContextReview, "operational-context-review"];
        yield return [WorkflowGateType.OperationalContextPromotion, "operational-context-promotion"];
        yield return [WorkflowGateType.PushApproval, "push-approval"];
    }

    [Fact]
    public async Task InfluenceTraceExplainsStageGateContinuationAndPreparationEvidence()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateDecision(fixture.Repository.Id, "DEC-0001", DecisionState.Open));
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        await workflowRepository.SaveContinuationEventAsync(
            fixture.Repository,
            new WorkflowContinuationEvent(
                fixture.Repository.Id,
                WorkflowArtifactPaths.ContinuationEventId(DateTimeOffset.Parse("2026-06-23T12:00:00Z")),
                DateTimeOffset.Parse("2026-06-23T12:00:00Z"),
                "endpoint",
                WorkflowStage.Handoff,
                WorkflowStage.Decision,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                "Advance",
                "Advanced to decision.",
                new WorkflowContinuationFingerprint("continuation-fingerprint"),
                false,
                false,
                "Resolve outstanding decisions.",
                ["Continuation diagnostic."]));
        await workflowRepository.SavePreparationEventAsync(
            fixture.Repository,
            new WorkflowPreparationEvent(
                fixture.Repository.Id,
                WorkflowArtifactPaths.PreparationEventId(DateTimeOffset.Parse("2026-06-23T12:01:00Z")),
                DateTimeOffset.Parse("2026-06-23T12:01:00Z"),
                "endpoint",
                WorkflowStage.Decision,
                WorkflowProgressState.AwaitingGate,
                WorkflowGateType.DecisionResolution,
                WorkflowPreparationCommand.DiscoverDecisionCandidates,
                "decisions_discover_candidates",
                "Refused",
                "DecisionResolution is awaiting human action.",
                new WorkflowPreparationFingerprint("preparation-fingerprint"),
                true,
                false,
                [],
                [],
                ["Preparation diagnostic."]));
        var service = fixture.CreateHealthService(workflowRepository);

        WorkflowInfluenceTrace trace = await service.TraceInfluenceAsync(fixture.Repository.Id);

        Assert.Equal(WorkflowStage.Decision, trace.CurrentStage);
        Assert.Equal(WorkflowGateType.DecisionResolution, trace.BlockingGate);
        Assert.Contains(trace.StageInfluences, influence => influence.Contains("Decision workflow status", StringComparison.Ordinal));
        Assert.Contains(trace.ProgressionInfluences, influence => influence.Contains("Latest continuation event", StringComparison.Ordinal));
        Assert.Contains(trace.PreparationInfluences, influence => influence.Contains("decisions_discover_candidates", StringComparison.Ordinal));
        Assert.Contains(trace.GateInfluences, influence => influence.Contains("Open gate DecisionResolution", StringComparison.Ordinal));
        Assert.Contains(trace.BlockingInfluences, influence => influence.Contains("UnresolvedDecision", StringComparison.Ordinal));
        Assert.Contains(trace.EvidencePaths, path => path.Contains("decision:DEC-0001", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(trace.Fingerprint));
    }

    [Fact]
    public async Task HealthAssessmentIsDecomposedAndIncludesInfluenceTrace()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateDecision(fixture.Repository.Id, "DEC-0001", DecisionState.Open));
        var service = fixture.CreateHealthService(new FileSystemWorkflowRepository(new MemoryArtifactStore()));

        WorkflowHealthAssessment assessment = await service.AssessHealthAsync(fixture.Repository.Id);

        Assert.Equal(fixture.Repository.Id, assessment.RepositoryId);
        Assert.Equal("Blocked", assessment.OverallStatus);
        Assert.Contains(assessment.Dimensions, dimension => dimension.Name == "Projection" && dimension.Status == "Healthy");
        Assert.Contains(assessment.Dimensions, dimension => dimension.Name == "Recovery" && dimension.Status == "Healthy");
        Assert.Contains(assessment.Dimensions, dimension => dimension.Name == "Gates" && dimension.Status == "Blocked");
        Assert.Contains(assessment.Dimensions, dimension => dimension.Name == "Continuation");
        Assert.Contains(assessment.Dimensions, dimension => dimension.Name == "Preparation");
        Assert.Equal(WorkflowStage.Decision, assessment.InfluenceTrace.CurrentStage);
    }

    [Fact]
    public async Task WorkflowCertificationPassesAuthorityBoundaryAndPersistsReport()
    {
        TestFixture fixture = TestFixture.Create();
        fixture.ExecutionState = RepositoryExecutionState.Accepted;
        fixture.Session = CompletedAcceptedSession();
        fixture.Decisions.Add(CreateDecision(fixture.Repository.Id, "DEC-0001", DecisionState.Open));
        var store = new MemoryArtifactStore();
        var workflowRepository = new FileSystemWorkflowRepository(store);
        WorkflowCertificationService service = fixture.CreateCertificationService(workflowRepository);

        WorkflowCertificationResult current = await service.GetCurrentCertificationAsync(fixture.Repository.Id);
        WorkflowCertificationResult persisted = await service.RunCertificationAsync(fixture.Repository.Id);
        IReadOnlyList<string> reportFiles = await store.ListAsync(
            WorkflowArtifactPaths.Resolve(fixture.Repository, WorkflowArtifactPaths.ReportsRoot),
            "*.json");

        Assert.True(current.Certified);
        Assert.Equal(WorkflowStage.Decision, current.CurrentStage);
        Assert.Equal(WorkflowGateType.DecisionResolution, current.BlockingGate);
        Assert.All(current.Findings, finding => Assert.Equal("Authority", finding.Category));
        Assert.Contains(current.Findings, finding => finding.Id == "authority-continuation-halts-at-gates" && finding.Passed);
        Assert.True(persisted.Certified);
        Assert.Single(reportFiles);
    }

    [Fact]
    public async Task WorkflowCertificationFailsForbiddenPreparationAuthorityCommand()
    {
        TestFixture fixture = TestFixture.Create();
        var workflowRepository = new FileSystemWorkflowRepository(new MemoryArtifactStore());
        await workflowRepository.SavePreparationEventAsync(
            fixture.Repository,
            new WorkflowPreparationEvent(
                fixture.Repository.Id,
                WorkflowArtifactPaths.PreparationEventId(DateTimeOffset.Parse("2026-06-23T12:30:00Z")),
                DateTimeOffset.Parse("2026-06-23T12:30:00Z"),
                "test",
                WorkflowStage.Push,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowPreparationCommand.None,
                "push_execution",
                "Created",
                "Forged authority command.",
                new WorkflowPreparationFingerprint("forged-authority"),
                false,
                false,
                [],
                [],
                []));
        WorkflowCertificationService service = fixture.CreateCertificationService(workflowRepository);

        WorkflowCertificationResult result = await service.GetCurrentCertificationAsync(fixture.Repository.Id);

        Assert.False(result.Certified);
        WorkflowCertificationFinding finding = Assert.Single(
            result.Findings,
            candidate => candidate.Id == "authority-no-forbidden-preparation-command");
        Assert.False(finding.Passed);
        Assert.Contains(finding.Diagnostics, diagnostic => diagnostic.Contains("push_execution", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("authority-no-forbidden-preparation-command", StringComparison.Ordinal));
    }

    private static void ArrangeAuthorityGateScenario(TestFixture fixture, string scenario)
    {
        switch (scenario)
        {
            case "work-selection":
                return;
            case "execution-acceptance":
                fixture.ExecutionState = RepositoryExecutionState.AwaitingAcceptance;
                fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingAcceptance);
                return;
            case "decision-resolution":
                fixture.ExecutionState = RepositoryExecutionState.Accepted;
                fixture.Session = CompletedAcceptedSession();
                fixture.Decisions.Add(CreateDecision(fixture.Repository.Id, "DEC-0001", DecisionState.Open));
                return;
            case "operational-context-review":
                fixture.ExecutionState = RepositoryExecutionState.Accepted;
                fixture.Session = CompletedAcceptedSession();
                fixture.Decisions.Add(CreateResolvedDecision(fixture.Repository.Id, "DEC-0001"));
                fixture.Proposals.Add(CreateOperationalContextProposal(
                    fixture.Repository.Id,
                    "ctx-0001",
                    OperationalContextProposalStatus.Pending,
                    OperationalContextReviewState.PendingReview,
                    null,
                    null));
                return;
            case "operational-context-promotion":
                fixture.ExecutionState = RepositoryExecutionState.Accepted;
                fixture.Session = CompletedAcceptedSession();
                fixture.Decisions.Add(CreateResolvedDecision(fixture.Repository.Id, "DEC-0001"));
                fixture.Proposals.Add(CreateOperationalContextProposal(
                    fixture.Repository.Id,
                    "ctx-0001",
                    OperationalContextProposalStatus.Accepted,
                    OperationalContextReviewState.Accepted,
                    DateTimeOffset.Parse("2026-06-23T11:10:00Z"),
                    null));
                return;
            case "commit-approval":
                fixture.ExecutionState = RepositoryExecutionState.AwaitingCommit;
                fixture.Session = CompletedAcceptedSession(RepositoryExecutionState.AwaitingCommit);
                return;
            case "push-approval":
                fixture.ExecutionState = RepositoryExecutionState.AwaitingPush;
                fixture.Session = AwaitingPushCommittedSession();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown authority gate scenario.");
        }
    }

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

    private static DecisionCandidate CreateCandidate(Guid repositoryId, string id) =>
        new(
            id,
            repositoryId,
            DecisionCandidateState.Discovered,
            DecisionCandidatePriority.Medium,
            DecisionClassification.Operational,
            "Workflow decision",
            "Candidate summary",
            "fingerprint",
            [],
            [],
            [],
            [],
            [
                new DecisionHistoryEntry(
                    DateTimeOffset.Parse("2026-06-23T10:05:00Z"),
                    "Discovered",
                    null,
                    DecisionCandidateState.Discovered.ToString(),
                    "Workflow test.",
                    [])
            ]);

    private static DecisionCandidate CreatePromotedCandidate(Guid repositoryId, string id) =>
        CreateCandidate(repositoryId, id) with
        {
            State = DecisionCandidateState.Promoted,
            History =
            [
                new DecisionHistoryEntry(
                    DateTimeOffset.Parse("2026-06-23T10:05:00Z"),
                    "Promoted",
                    DecisionCandidateState.Discovered.ToString(),
                    DecisionCandidateState.Promoted.ToString(),
                    "Workflow test.",
                    [])
            ]
        };

    private static DecisionProposal CreateProposal(Guid repositoryId, string id, string candidateId) =>
        new(
            id,
            repositoryId,
            candidateId,
            DecisionProposalState.Generated,
            "Workflow decision",
            "Context",
            [],
            [],
            null,
            [],
            [],
            [
                new DecisionHistoryEntry(
                    DateTimeOffset.Parse("2026-06-23T10:10:00Z"),
                    "Generated",
                    null,
                    DecisionProposalState.Generated.ToString(),
                    "Workflow test.",
                    [])
            ]);

    private static OperationalContextProposal CreateOperationalContextProposal(
        Guid repositoryId,
        string proposalId,
        OperationalContextProposalStatus status,
        OperationalContextReviewState reviewState,
        DateTimeOffset? reviewedAt,
        DateTimeOffset? promotedAt) =>
        new()
        {
            ProposalId = proposalId,
            RepositoryId = repositoryId,
            GeneratedAt = DateTimeOffset.Parse("2026-06-23T11:00:00Z"),
            Status = status,
            Review = new OperationalContextReview
            {
                ProposalId = proposalId,
                ReviewState = reviewState,
                ReviewedAt = reviewedAt
            },
            Promotion = new OperationalContextPromotion
            {
                ProposalId = proposalId,
                PromotedAt = promotedAt
            }
        };

    private static DecisionAssimilationRecommendation CreateAssimilationRecommendation(
        Guid repositoryId,
        Decision decision,
        string decisionFingerprint)
    {
        var validation = new DecisionContextValidationResult(true, [], []);
        var diagnostics = new DecisionContextDiagnostics([], []);
        var context = new DecisionContext(repositoryId, "context-fingerprint", [], diagnostics, validation);
        var snapshot = new DecisionContextSnapshot(
            "context-snapshot-1",
            repositoryId,
            DateTimeOffset.Parse("2026-06-23T10:35:00Z"),
            "context-fingerprint",
            context,
            diagnostics,
            validation);

        return new DecisionAssimilationRecommendation(
            decision.Id.Value,
            repositoryId,
            DateTimeOffset.Parse("2026-06-23T10:40:00Z"),
            decisionFingerprint,
            snapshot.SnapshotId,
            snapshot.Fingerprint,
            decision,
            snapshot,
            "Record the resolved workflow decision.",
            "Resolved decision should be available to future work.",
            "human",
            null,
            [],
            [],
            []);
    }

    private static WorkflowTimeline CreateTimeline(Guid repositoryId, DateTimeOffset generatedAt, WorkflowStage currentStage)
        => CreateTimeline(
            repositoryId,
            generatedAt,
            currentStage,
            WorkflowProgressState.AwaitingGate,
            WorkflowGateType.CommitApproval,
            WorkflowStage.Handoff,
            [WorkflowBlockingCondition.PendingCommitApproval]);

    private static WorkflowTimeline CreateTimeline(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        WorkflowStage currentStage,
        WorkflowProgressState progressState,
        WorkflowGateType blockingGate,
        WorkflowStage previousStage,
        IReadOnlyList<WorkflowBlockingCondition?> blockingConditions)
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
            previousStage,
            progressState,
            blockingGate,
            [entry],
            blockingConditions).Value;

        return new WorkflowTimeline(
            repositoryId,
            currentStage,
            previousStage,
            progressState,
            blockingGate,
            generatedAt,
            [entry],
            fingerprint);
    }

    private static Task SaveOperationalContextPreparationTimelineAsync(
        TestFixture fixture,
        IWorkflowRepository workflowRepository) =>
        workflowRepository.SaveTimelineAsync(
            fixture.Repository,
            CreateTimeline(
                fixture.Repository.Id,
                DateTimeOffset.Parse("2026-06-23T11:25:00Z"),
                WorkflowStage.OperationalContext,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowStage.Decision,
                []));

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

        public string? HandoffContent { get; set; } = "# Handoff\n\nGenerated handoff.";

        public List<Decision> Decisions { get; } = [];

        public List<DecisionCandidate> Candidates { get; } = [];

        public List<DecisionProposal> DecisionProposals { get; } = [];

        public List<DecisionPackageVersion> PackageVersions { get; } = [];

        public List<DecisionGovernanceReport> GovernanceReports { get; } = [];

        public List<DecisionQualityAssessment> QualityAssessments { get; } = [];

        public List<DecisionCertificationReport> CertificationReports { get; } = [];

        public List<DecisionAssimilationRecommendation> AssimilationRecommendations { get; } = [];

        public List<OperationalContextProposal> Proposals { get; } = [];

        public RepositoryGitStatus GitStatus { get; set; } = new()
        {
            Branch = "main",
            DirtyState = new RepositoryDirtyState { IsClean = true },
            CapturedAt = DateTimeOffset.Parse("2026-06-23T10:00:00Z")
        };

        public int PrepareCommitCallCount { get; set; }

        public int CommitCallCount { get; set; }

        public int PushCallCount { get; set; }

        public static TestFixture Create() => new();

        public WorkflowProjectionService CreateService() =>
            new(
                new RepositoryServiceStub(Repository),
                new WorkflowExecutionService(new ExecutionSessionServiceStub(this)),
            new WorkflowHandoffService(
                new RepositoryServiceStub(Repository),
                new ExecutionSessionServiceStub(this),
                new ArtifactStoreStub(this)),
            new WorkflowDecisionService(new RepositoryServiceStub(Repository), new DecisionRepositoryStub(this)),
            new WorkflowOperationalContextService(
                new RepositoryServiceStub(Repository),
                new OperationalContextProposalStoreStub(this),
                new DecisionRepositoryStub(this)),
            new WorkflowGitService(
                new RepositoryServiceStub(Repository),
                new ExecutionSessionServiceStub(this),
                new GitServiceStub(this)),
            new WorkflowStateMachineService());

        public WorkflowExecutionService CreateExecutionService() =>
            new(new ExecutionSessionServiceStub(this));

        public WorkflowHandoffService CreateHandoffService() =>
            new(new RepositoryServiceStub(Repository), new ExecutionSessionServiceStub(this), new ArtifactStoreStub(this));

        public WorkflowDecisionService CreateDecisionService() =>
            new(new RepositoryServiceStub(Repository), new DecisionRepositoryStub(this));

        public WorkflowOperationalContextService CreateOperationalContextService() =>
            new(new RepositoryServiceStub(Repository), new OperationalContextProposalStoreStub(this), new DecisionRepositoryStub(this));

        public WorkflowGitService CreateGitWorkflowService() =>
            new(new RepositoryServiceStub(Repository), new ExecutionSessionServiceStub(this), new GitServiceStub(this));

        public WorkflowContinuationService CreateContinuationService() =>
            new(
                new RepositoryServiceStub(Repository),
                CreateService(),
                new WorkflowStateMachineService(),
                new FileSystemWorkflowRepository(new MemoryArtifactStore()));

        public WorkflowPreparationService CreatePreparationService() =>
            new(
                new RepositoryServiceStub(Repository),
                CreateService(),
                new FileSystemWorkflowRepository(new MemoryArtifactStore()));

        public WorkflowRecoveryService CreateRecoveryService(IWorkflowRepository workflowRepository) =>
            new(new RepositoryServiceStub(Repository), CreateService(), workflowRepository);

        public WorkflowHealthService CreateHealthService(IWorkflowRepository workflowRepository) =>
            new(new RepositoryServiceStub(Repository), CreateService(), workflowRepository);

        public WorkflowCertificationService CreateCertificationService(IWorkflowRepository workflowRepository) =>
            new(
                new RepositoryServiceStub(Repository),
                CreateService(),
                workflowRepository,
                CreateHealthService(workflowRepository));

        public void ReplaceServices(IServiceCollection services)
        {
            services.RemoveAll<IRepositoryService>();
            services.RemoveAll<IArtifactStore>();
            services.RemoveAll<IExecutionSessionService>();
            services.RemoveAll<IDecisionRepository>();
            services.RemoveAll<IOperationalContextProposalStore>();
            services.RemoveAll<IGitService>();
            services.RemoveAll<IWorkflowStateMachineService>();
            services.RemoveAll<IWorkflowExecutionService>();
            services.RemoveAll<IWorkflowHandoffService>();
            services.RemoveAll<IWorkflowDecisionService>();
            services.RemoveAll<IWorkflowOperationalContextService>();
            services.RemoveAll<IWorkflowGitService>();
            services.RemoveAll<IWorkflowProjectionService>();
            services.RemoveAll<IWorkflowGateCatalogService>();
            services.RemoveAll<IWorkflowContinuationService>();
            services.RemoveAll<IWorkflowPreparationService>();
            services.RemoveAll<IWorkflowHealthService>();
            services.RemoveAll<IWorkflowCertificationService>();
            services.RemoveAll<IWorkflowRepository>();
            services.RemoveAll<IWorkflowRecoveryService>();
            services.RemoveAll<IHostedService>();

            services.AddSingleton<IRepositoryService>(new RepositoryServiceStub(Repository));
            services.AddSingleton<IArtifactStore>(new ArtifactStoreStub(this));
            services.AddSingleton<IExecutionSessionService>(new ExecutionSessionServiceStub(this));
            services.AddSingleton<IDecisionRepository>(new DecisionRepositoryStub(this));
            services.AddSingleton<IOperationalContextProposalStore>(new OperationalContextProposalStoreStub(this));
            services.AddSingleton<IGitService>(new GitServiceStub(this));
            services.AddSingleton<IWorkflowRepository, FileSystemWorkflowRepository>();
            services.AddSingleton<IWorkflowExecutionService, WorkflowExecutionService>();
            services.AddSingleton<IWorkflowHandoffService, WorkflowHandoffService>();
            services.AddSingleton<IWorkflowDecisionService, WorkflowDecisionService>();
            services.AddSingleton<IWorkflowOperationalContextService, WorkflowOperationalContextService>();
            services.AddSingleton<IWorkflowGitService, WorkflowGitService>();
            services.AddSingleton<IWorkflowStateMachineService, WorkflowStateMachineService>();
            services.AddSingleton<IWorkflowProjectionService, WorkflowProjectionService>();
            services.AddSingleton<IWorkflowGateCatalogService, WorkflowGateCatalogService>();
            services.AddSingleton<IWorkflowContinuationService, WorkflowContinuationService>();
            services.AddSingleton<IWorkflowPreparationService, WorkflowPreparationService>();
            services.AddSingleton<IWorkflowHealthService, WorkflowHealthService>();
            services.AddSingleton<IWorkflowCertificationService, WorkflowCertificationService>();
            services.AddSingleton<IWorkflowRecoveryService, WorkflowRecoveryService>();
        }
    }

    private sealed class RepositoryServiceStub(params Repository[] repositories) : IRepositoryService
    {
        public Task<IReadOnlyList<Repository>> GetAllAsync() => Task.FromResult<IReadOnlyList<Repository>>(repositories);

        public Task<Repository> RegisterAsync(string repositoryPath) => throw new NotSupportedException("Mutating repository methods are not used by workflow projection.");

        public Task RemoveAsync(Guid repositoryId) => throw new NotSupportedException("Mutating repository methods are not used by workflow projection.");
    }

    private sealed class HostedContinuationServiceStub(Guid? failingRepositoryId = null) : IWorkflowContinuationService
    {
        public List<Guid> CalledRepositoryIds { get; } = [];

        public List<string> Triggers { get; } = [];

        public Task<WorkflowContinuationEvaluation> EvaluateContinuationAsync(Guid repositoryId) =>
            throw new NotSupportedException("Hosted service tests call run paths only.");

        public Task<WorkflowContinuationEvent> RunContinuationAsync(Guid repositoryId, string trigger = "endpoint")
        {
            CalledRepositoryIds.Add(repositoryId);
            Triggers.Add(trigger);
            if (failingRepositoryId == repositoryId)
            {
                throw new InvalidOperationException("Simulated repository failure.");
            }

            return Task.FromResult(new WorkflowContinuationEvent(
                repositoryId,
                $"continuation-{CalledRepositoryIds.Count}",
                DateTimeOffset.Parse("2026-06-23T12:00:00Z"),
                trigger,
                WorkflowStage.Handoff,
                WorkflowStage.Decision,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                "Advance",
                "Advanced for hosted service test.",
                new WorkflowContinuationFingerprint("fingerprint"),
                false,
                false,
                "No human action required.",
                []));
        }

        public Task<IReadOnlyList<WorkflowContinuationEvent>> GetContinuationHistoryAsync(Guid repositoryId) =>
            Task.FromResult<IReadOnlyList<WorkflowContinuationEvent>>([]);
    }

    private sealed class HostedPreparationServiceStub : IWorkflowPreparationService
    {
        public List<Guid> CalledRepositoryIds { get; } = [];

        public List<string> Triggers { get; } = [];

        public Task<WorkflowPreparationEvaluation> EvaluatePreparationAsync(Guid repositoryId)
        {
            var fingerprint = new WorkflowPreparationFingerprint("fingerprint");
            var diagnostics = new WorkflowPreparationDiagnostics(
                repositoryId,
                [],
                [],
                ["Hosted service test preparation evaluation."],
                [],
                [],
                [],
                0,
                0,
                fingerprint);

            return Task.FromResult(new WorkflowPreparationEvaluation(
                repositoryId,
                WorkflowStage.Decision,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                true,
                false,
                false,
                WorkflowPreparationCommand.DiscoverDecisionCandidates,
                "decisions_discover_candidates",
                "Allowed",
                "Allowed for hosted service test.",
                [],
                fingerprint,
                diagnostics));
        }

        public Task<WorkflowPreparationEvent> RunPreparationAsync(Guid repositoryId, string trigger = "endpoint")
        {
            CalledRepositoryIds.Add(repositoryId);
            Triggers.Add(trigger);
            return Task.FromResult(new WorkflowPreparationEvent(
                repositoryId,
                $"preparation-{CalledRepositoryIds.Count}",
                DateTimeOffset.Parse("2026-06-23T12:01:00Z"),
                trigger,
                WorkflowStage.Decision,
                WorkflowProgressState.Ready,
                WorkflowGateType.None,
                WorkflowPreparationCommand.DiscoverDecisionCandidates,
                "decisions_discover_candidates",
                "Skipped",
                "Skipped for hosted service test.",
                new WorkflowPreparationFingerprint("fingerprint"),
                false,
                false,
                [],
                [],
                []));
        }

        public Task<IReadOnlyList<WorkflowPreparationEvent>> GetPreparationHistoryAsync(Guid repositoryId) =>
            Task.FromResult<IReadOnlyList<WorkflowPreparationEvent>>([]);
    }

    private sealed class ArtifactStoreStub(TestFixture fixture) : IArtifactStore
    {
        private readonly MemoryArtifactStore innerStore = new();

        public async Task<bool> ExistsAsync(string path)
        {
            if (IsCurrentHandoffPath(path) && fixture.HandoffContent is not null)
            {
                return true;
            }

            return await innerStore.ExistsAsync(path);
        }

        public async Task<string?> ReadAsync(string path)
        {
            if (IsCurrentHandoffPath(path))
            {
                return fixture.HandoffContent;
            }

            return await innerStore.ReadAsync(path);
        }

        public Task WriteAsync(string path, string content) => innerStore.WriteAsync(path, content);

        public Task DeleteAsync(string path) => innerStore.DeleteAsync(path);

        public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern) => innerStore.ListAsync(path, searchPattern);

        public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) => innerStore.ListDirectoriesAsync(path);

        private bool IsCurrentHandoffPath(string path)
        {
            string expected = Path.Combine(fixture.Repository.Path, ".agents", "handoffs", "handoff.md");
            return string.Equals(path, expected, StringComparison.OrdinalIgnoreCase);
        }
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

        public Task<CommitPreparation> PrepareCommitAsync(Guid sessionId)
        {
            fixture.PrepareCommitCallCount++;
            ExecutionSessionSummary summary = fixture.Session
                ?? throw new KeyNotFoundException($"Execution session was not found: {sessionId}");
            if (summary.SessionId != sessionId)
            {
                throw new KeyNotFoundException($"Execution session was not found: {sessionId}");
            }

            var snapshot = new CommitStatusSnapshot
            {
                Id = "snapshot-prepared",
                Branch = "main",
                DirtyState = new RepositoryDirtyState
                {
                    IsClean = false,
                    ModifiedPaths = ["src/CommandCenter.Workflow/Services/WorkflowPreparationService.cs"]
                },
                CapturedAt = DateTimeOffset.Parse("2026-06-23T12:30:00Z")
            };
            var preparation = new CommitPreparation
            {
                Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                SessionId = sessionId,
                RepositoryId = fixture.Repository.Id,
                RepositoryPath = fixture.Repository.Path,
                ProposedMessage = "Prepare workflow commit",
                ScopeItems =
                [
                    new CommitScopeItem
                    {
                        Path = "src/CommandCenter.Workflow/Services/WorkflowPreparationService.cs",
                        ChangeType = CommitChangeType.Modified
                    }
                ],
                StatusSnapshot = snapshot,
                GeneratedAt = DateTimeOffset.Parse("2026-06-23T12:31:00Z")
            };
            fixture.Session = new ExecutionSessionSummary
            {
                SessionId = summary.SessionId,
                State = summary.State,
                RepositoryState = summary.RepositoryState,
                MilestonePath = summary.MilestonePath,
                StartedAt = summary.StartedAt,
                CompletedAt = summary.CompletedAt,
                Duration = summary.Duration,
                AcceptedAt = summary.AcceptedAt,
                RejectedAt = summary.RejectedAt,
                DecisionNote = summary.DecisionNote,
                LastActivityAt = DateTimeOffset.Parse("2026-06-23T12:31:00Z"),
                ProviderName = summary.ProviderName,
                ProviderExecutablePath = summary.ProviderExecutablePath,
                ProviderProcessId = summary.ProviderProcessId,
                ProviderStartedAt = summary.ProviderStartedAt,
                HandoffPath = summary.HandoffPath,
                CommitSha = summary.CommitSha,
                CommittedAt = summary.CommittedAt,
                CommitMessage = summary.CommitMessage,
                PreparationSnapshotId = snapshot.Id,
                PushAttemptedAt = summary.PushAttemptedAt,
                PushedAt = summary.PushedAt,
                PushedCommitSha = summary.PushedCommitSha,
                PushRemoteName = summary.PushRemoteName,
                PushBranchName = summary.PushBranchName,
                FailureReason = summary.FailureReason
            };

            return Task.FromResult(preparation);
        }

        public Task<ExecutionSessionSummary> CommitAsync(Guid sessionId, CommitRequest request)
        {
            fixture.CommitCallCount++;
            throw new NotSupportedException("Workflow preparation must not execute commits.");
        }

        public Task<ExecutionSessionSummary> PushAsync(Guid sessionId, PushRequest request)
        {
            fixture.PushCallCount++;
            throw new NotSupportedException("Workflow preparation must not execute pushes.");
        }
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
        public Task<IReadOnlyList<DecisionCandidate>> ListCandidatesAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionCandidate>>(fixture.Candidates);
        public Task<DecisionCandidate?> GetCandidateAsync(Repository repository, string candidateId) => Task.FromResult(fixture.Candidates.FirstOrDefault(candidate => candidate.Id == candidateId));
        public Task<DecisionCandidate> SaveCandidateAsync(Repository repository, DecisionCandidate candidate) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionProposal>> ListProposalsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionProposal>>(fixture.DecisionProposals);
        public Task<DecisionProposal?> GetProposalAsync(Repository repository, string proposalId) => Task.FromResult(fixture.DecisionProposals.FirstOrDefault(proposal => proposal.Id == proposalId));
        public Task<DecisionProposal> SaveProposalAsync(Repository repository, DecisionProposal proposal) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Repository repository, string proposalId) => Task.FromResult<IReadOnlyList<DecisionProposalRevision>>([]);
        public Task<DecisionProposalRevision> SaveProposalRevisionAsync(Repository repository, DecisionProposalRevision revision) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionPackageVersion>> ListPackageVersionsAsync(Repository repository, string proposalId) => Task.FromResult<IReadOnlyList<DecisionPackageVersion>>(fixture.PackageVersions.Where(package => package.ProposalId == proposalId).ToArray());
        public Task<DecisionPackageVersion?> GetPackageVersionAsync(Repository repository, string proposalId, string packageId) => Task.FromResult(fixture.PackageVersions.FirstOrDefault(package => package.ProposalId == proposalId && package.Id == packageId));
        public Task<DecisionPackageVersion> SavePackageVersionAsync(Repository repository, DecisionPackageVersion packageVersion) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionRefinementArtifact>> ListRefinementArtifactsAsync(Repository repository, string proposalId) => Task.FromResult<IReadOnlyList<DecisionRefinementArtifact>>([]);
        public Task<DecisionRefinementArtifact> SaveRefinementArtifactAsync(Repository repository, DecisionRefinementArtifact refinementArtifact) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<DecisionReviewStatus?> GetReviewStatusAsync(Repository repository, string proposalId) => Task.FromResult<DecisionReviewStatus?>(null);
        public Task<DecisionReviewStatus> SaveReviewStatusAsync(Repository repository, DecisionReviewStatus reviewStatus) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionReviewNote>> ListReviewNotesAsync(Repository repository, string proposalId) => Task.FromResult<IReadOnlyList<DecisionReviewNote>>([]);
        public Task<DecisionReviewNote> SaveReviewNoteAsync(Repository repository, DecisionReviewNote note) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<DecisionAssimilationRecommendation?> GetAssimilationRecommendationAsync(Repository repository, DecisionId decisionId) => Task.FromResult(fixture.AssimilationRecommendations.FirstOrDefault(recommendation => recommendation.DecisionId == decisionId.Value));
        public Task<DecisionAssimilationRecommendation> SaveAssimilationRecommendationAsync(Repository repository, DecisionAssimilationRecommendation recommendation) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionGovernanceReport>> ListGovernanceReportsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionGovernanceReport>>(fixture.GovernanceReports);
        public Task<DecisionGovernanceReport> SaveGovernanceReportAsync(Repository repository, DecisionGovernanceReport report) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionCertificationReport>> ListCertificationReportsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionCertificationReport>>(fixture.CertificationReports);
        public Task<DecisionCertificationReport> SaveCertificationReportAsync(Repository repository, DecisionCertificationReport report) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionGenerationCertificationReport>> ListGenerationCertificationReportsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionGenerationCertificationReport>>([]);
        public Task<DecisionGenerationCertificationReport> SaveGenerationCertificationReportAsync(Repository repository, DecisionGenerationCertificationReport report) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionQualityAssessment>> ListQualityAssessmentsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionQualityAssessment>>(fixture.QualityAssessments);
        public Task<DecisionQualityAssessment> SaveQualityAssessmentAsync(Repository repository, DecisionQualityAssessment assessment) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionQualityReport>> ListQualityReportsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionQualityReport>>([]);
        public Task<DecisionQualityReport> SaveQualityReportAsync(Repository repository, DecisionQualityReport report) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
        public Task<IReadOnlyList<DecisionQualityTrend>> ListQualityTrendsAsync(Repository repository) => Task.FromResult<IReadOnlyList<DecisionQualityTrend>>([]);
        public Task<DecisionQualityTrend> SaveQualityTrendAsync(Repository repository, DecisionQualityTrend trend) => throw new NotSupportedException("Mutating decision methods are not used by workflow projection.");
    }

    private sealed class DecisionDiscoveryServiceStub(
        IReadOnlyList<DecisionCandidate> candidates,
        Action<IReadOnlyList<DecisionCandidate>>? onDiscover = null) : IDecisionDiscoveryService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<DecisionCandidate>> ListCandidatesAsync(Guid repositoryId) =>
            Task.FromResult(candidates);

        public Task<DecisionDiscoveryResult> DiscoverAsync(Guid repositoryId)
        {
            CallCount++;
            onDiscover?.Invoke(candidates);
            return Task.FromResult(new DecisionDiscoveryResult(
                candidates,
                new DecisionDiscoveryDiagnostics(
                    "fingerprint",
                    1,
                    candidates.Count,
                    candidates.Count,
                    0,
                    [])));
        }

        public Task<DecisionCandidate> PromoteCandidateAsync(Guid repositoryId, string candidateId, string? reason) =>
            throw new NotSupportedException("Workflow preparation must not promote decision candidates.");

        public Task<DecisionCandidate> DismissCandidateAsync(Guid repositoryId, string candidateId, string? reason) =>
            throw new NotSupportedException("Workflow preparation must not dismiss decision candidates.");

        public Task<DecisionCandidate> ExpireCandidateAsync(Guid repositoryId, string candidateId, string? reason) =>
            throw new NotSupportedException("Workflow preparation must not expire decision candidates.");

        public Task<DecisionCandidate> MarkCandidateDuplicateAsync(
            Guid repositoryId,
            string candidateId,
            string duplicateOfCandidateId,
            string? reason) =>
            throw new NotSupportedException("Workflow preparation must not mark decision candidates duplicate.");
    }

    private sealed class DecisionGenerationServiceStub(
        DecisionProposal proposal,
        Action<DecisionProposal>? onGenerate = null) : IDecisionGenerationService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<DecisionProposal>> ListProposalsAsync(Guid repositoryId) =>
            Task.FromResult<IReadOnlyList<DecisionProposal>>([proposal]);

        public Task<DecisionProposal> GetProposalAsync(Guid repositoryId, string proposalId) =>
            Task.FromResult(proposal);

        public Task<DecisionProposal> GenerateProposalAsync(Guid repositoryId, string candidateId)
        {
            CallCount++;
            onGenerate?.Invoke(proposal);
            return Task.FromResult(proposal);
        }

        public Task<DecisionProposal> MarkProposalViewedAsync(Guid repositoryId, string proposalId, string? reason) =>
            throw new NotSupportedException("Workflow preparation must not mark decision proposals viewed.");

        public Task<DecisionProposal> MarkProposalNeedsRefinementAsync(Guid repositoryId, string proposalId, string? reason) =>
            throw new NotSupportedException("Workflow preparation must not mark decision proposals for refinement.");

        public Task<DecisionProposal> MarkProposalReadyForResolutionAsync(Guid repositoryId, string proposalId, string? reason) =>
            throw new NotSupportedException("Workflow preparation must not mark decision proposals ready for resolution.");

        public Task<DecisionProposal> RefineProposalAsync(Guid repositoryId, string proposalId, DecisionRefinementRequest request) =>
            throw new NotSupportedException("Workflow preparation must not refine decision proposals.");

        public Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Guid repositoryId, string proposalId) =>
            Task.FromResult<IReadOnlyList<DecisionProposalRevision>>([]);

        public Task<DecisionProposal> ExpireProposalAsync(Guid repositoryId, string proposalId, string? reason) =>
            throw new NotSupportedException("Workflow preparation must not expire decision proposals.");

        public Task<DecisionProposal> DiscardProposalAsync(Guid repositoryId, string proposalId, string? reason) =>
            throw new NotSupportedException("Workflow preparation must not discard decision proposals.");
    }

    private sealed class OperationalContextGenerationServiceStub(
        OperationalContextProposal proposal,
        Action<OperationalContextProposal>? onGenerate = null) : IOperationalContextGenerationService
    {
        public int CallCount { get; private set; }

        public Task<OperationalContextProposal> GenerateAsync(Guid repositoryId)
        {
            CallCount++;
            onGenerate?.Invoke(proposal);
            return Task.FromResult(proposal);
        }
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
