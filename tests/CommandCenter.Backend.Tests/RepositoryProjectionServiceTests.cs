using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Configuration;
using CommandCenter.Continuity;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Continuity.Services;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Primitives;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;
using CommandCenter.Execution;
using CommandCenter.Core.Planning;
using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;
using CommandCenter.Execution.Services;
using CommandCenter.Middle.Projections;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.Backend.Tests;

public sealed class RepositoryProjectionServiceTests
{
    [Fact]
    public async Task DashboardReturnsRegisteredRepositoryAvailability()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        IReadOnlyList<RepositoryDashboardProjection> dashboard = await projectionService.GetDashboardAsync();

        RepositoryDashboardProjection projection = Assert.Single(dashboard);
        Assert.Equal(repository.Id, projection.Repository.Id);
        Assert.Equal(RepositoryAvailability.Available, projection.Availability);
    }

    [Fact]
    public async Task MissingRegisteredRepositoryReportsMissing()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        Directory.Delete(repositoryPath, recursive: true);
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        IReadOnlyList<RepositoryDashboardProjection> dashboard = await projectionService.GetDashboardAsync();

        RepositoryDashboardProjection projection = Assert.Single(dashboard);
        Assert.Equal(repository.Id, projection.Repository.Id);
        Assert.Equal(RepositoryAvailability.Missing, projection.Availability);
    }

    [Fact]
    public async Task WorkspaceRefreshDiscoversExternallyAddedArtifacts()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        RepositoryWorkspaceProjection beforeRefresh = await projectionService.GetWorkspaceAsync(repository.Id);
        await WriteAsync(repository, ".agents/milestones/m2.md", "milestone");
        RepositoryWorkspaceProjection afterRefresh = await projectionService.RefreshWorkspaceAsync(repository.Id);

        Assert.Empty(beforeRefresh.ArtifactInventory.Milestones);
        Artifact milestone = Assert.Single(afterRefresh.ArtifactInventory.Milestones);
        Assert.Equal(".agents/milestones/m2.md", milestone.RelativePath);
    }

    [Fact]
    public async Task WorkspaceRefreshRemovesExternallyDeletedArtifacts()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "handoff");
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        RepositoryWorkspaceProjection beforeDelete = await projectionService.GetWorkspaceAsync(repository.Id);
        File.Delete(Path.Combine(repository.Path, ".agents", "handoffs", "handoff.md"));
        RepositoryWorkspaceProjection afterRefresh = await projectionService.RefreshWorkspaceAsync(repository.Id);

        Assert.NotNull(beforeDelete.ArtifactInventory.CurrentHandoff);
        Assert.Null(afterRefresh.ArtifactInventory.CurrentHandoff);
        Assert.False(afterRefresh.HasCurrentHandoff);
    }

    [Fact]
    public async Task WorkspaceProjectionComposesInventoryStatus()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/plan.md", "plan");
        await WriteAsync(repository, ".agents/operational_context.md", "context");
        await WriteAsync(repository, ".agents/decisions/decisions.md", "decisions");
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        Assert.True(workspace.HasPlan);
        Assert.True(workspace.HasOperationalContext);
        Assert.True(workspace.HasCurrentDecisions);
        Assert.NotNull(workspace.ArtifactInventory.Plan);
        Assert.NotNull(workspace.ArtifactInventory.OperationalContext);
        Assert.NotNull(workspace.ArtifactInventory.CurrentDecisions);
    }

    [Fact]
    public async Task WorkspaceRefreshRecoversMissingDecisionIndexFromStructuredArtifacts()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        DateTimeOffset now = new(2026, 06, 22, 12, 00, 00, TimeSpan.Zero);
        await decisionRepository.SaveDecisionAsync(repository, new Decision(
            new DecisionId("DEC-0001"),
            DecisionState.Open,
            DecisionClassification.Architectural,
            "Recover decision projections",
            "Structured JSON remains authoritative after generated markdown is deleted.",
            new DecisionMetadata(repository.Id, now, now),
            null,
            [],
            [],
            [new DecisionHistoryEntry(now, "Created", null, DecisionState.Open.ToString(), null, [])]));
        var projectionRecovery = new DecisionArtifactProjectionService(decisionRepository, store);
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService, projectionRecovery);

        RepositoryWorkspaceProjection workspace = await projectionService.RefreshWorkspaceAsync(repository.Id);

        Assert.True(workspace.HasCurrentDecisions);
        Assert.NotNull(workspace.ArtifactInventory.CurrentDecisions);
        Assert.Equal(".agents/decisions/decisions.md", workspace.ArtifactInventory.CurrentDecisions.RelativePath);
        string generatedIndex = await File.ReadAllTextAsync(Path.Combine(repository.Path, ".agents", "decisions", "decisions.md"));
        Assert.Contains("- DEC-0001 | Open | Architectural | Unresolved | Recover decision projections", generatedIndex);
    }

    [Fact]
    public async Task WorkspaceProjectionParsesOperationalContextIntoUnderstandingSections()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/operational_context.0004.md", "historical");
        await WriteAsync(repository, ".agents/operational_context.md", """
            # Operational Context

            ## Current Mental Model

            - Command Center preserves understanding through repository artifacts.

            ## Architecture

            - Backend projections are the authority for workspace continuity state.

            ## Authority Boundaries

            - The UI may display understanding but must not compute it.

            ## Constraints

            - Operational context mutation requires human review.

            ## Stable Decisions

            - Disposable execution sessions remain separate from continuity.

            ## Decision Rationale

            - Repository artifacts survive restarts and provider replacement.

            ## Open Questions

            - Which continuity warnings should be visible on the dashboard?

            ## Active Risks

            - Projection drift could make the UI appear authoritative.

            ## Recent Understanding Changes

            - M7 introduced a backend understanding projection.
            """);
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        Assert.True(workspace.OperationalContext.Exists);
        Assert.Equal(".agents/operational_context.md", workspace.OperationalContext.CurrentRelativePath);
        Assert.Equal(2, workspace.OperationalContext.RevisionCount);
        Assert.Equal(5, workspace.OperationalContext.CurrentRevisionNumber);
        Assert.NotNull(workspace.OperationalContext.LastUpdatedAt);
        Assert.Contains("repository artifacts", Assert.Single(workspace.OperationalContext.CurrentUnderstandingSummary));
        Assert.Contains(workspace.OperationalContext.Architecture, item =>
            item.Text.Contains("Backend projections", StringComparison.Ordinal));
        Assert.Contains(workspace.OperationalContext.AuthorityBoundaries, item =>
            item.Text.Contains("UI may display", StringComparison.Ordinal));
        Assert.Contains(workspace.OperationalContext.Constraints, item =>
            item.Text.Contains("human review", StringComparison.Ordinal));
        Assert.Contains(workspace.OperationalContext.StableDecisions, item =>
            item.Text.Contains("Disposable execution sessions", StringComparison.Ordinal));
        Assert.Contains(workspace.OperationalContext.DecisionRationale, item =>
            item.Text.Contains("survive restarts", StringComparison.Ordinal));
        Assert.Contains(workspace.OperationalContext.OpenQuestions, item =>
            item.Text.Contains("dashboard", StringComparison.Ordinal));
        Assert.Contains(workspace.OperationalContext.ActiveRisks, item =>
            item.Text.Contains("Projection drift", StringComparison.Ordinal));
        Assert.Contains(workspace.OperationalContext.RecentUnderstandingChanges, item =>
            item.Text.Contains("M7 introduced", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WorkspaceProjectionReportsMissingOperationalContextExplicitly()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        Assert.False(workspace.OperationalContext.Exists);
        Assert.Null(workspace.OperationalContext.CurrentRelativePath);
        Assert.Equal(0, workspace.OperationalContext.RevisionCount);
        Assert.Equal(0, workspace.OperationalContext.CurrentRevisionNumber);
        Assert.Empty(workspace.OperationalContext.OpenQuestions);
        Assert.Empty(workspace.OperationalContext.ActiveRisks);
    }

    [Fact]
    public async Task DashboardContinuitySummaryExposesRevisionQuestionAndRiskCounts()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/operational_context.0001.md", "historical");
        await WriteAsync(repository, ".agents/operational_context.md", """
            # Operational Context

            ## Open Questions

            - First question?
            - Second question?

            ## Active Risks

            - Current risk.
            """);
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        IReadOnlyList<RepositoryDashboardProjection> dashboard = await projectionService.GetDashboardAsync();

        RepositoryDashboardProjection projection = Assert.Single(dashboard);
        Assert.True(projection.ContinuitySummary.OperationalContextExists);
        Assert.Equal(2, projection.ContinuitySummary.OperationalContextRevisionCount);
        Assert.NotNull(projection.ContinuitySummary.OperationalContextLastUpdatedAt);
        Assert.Equal(2, projection.ContinuitySummary.OpenQuestionCount);
        Assert.Equal(1, projection.ContinuitySummary.ActiveRiskCount);
    }

    [Fact]
    public async Task DashboardAndWorkspaceExposeReadOnlyReasoningSummary()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        IReasoningRepository reasoningRepository = CreateReasoningRepository();
        ReasoningEvent hypothesis = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Hypothesis,
            ReasoningEventType.HypothesisRaised,
            "Hypothesis raised"));
        ReasoningEvent alternative = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Alternative,
            ReasoningEventType.AlternativeRejected,
            "Alternative rejected"));
        ReasoningEvent contradiction = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Contradiction,
            ReasoningEventType.ContradictionResolved,
            "Contradiction resolved"));
        ReasoningEvent direction = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Direction,
            ReasoningEventType.DirectionShifted,
            "Direction shifted"));
        ReasoningThread thread = await reasoningRepository.CreateThreadAsync(repository, new CreateReasoningThreadCommand(
            "Reasoning summary thread",
            ReasoningThreadTheme.StrategicMovement,
            "Tracks recent reasoning activity.",
            [hypothesis.Id, alternative.Id, contradiction.Id, direction.Id],
            []));
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.LeadsTo,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, contradiction.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, direction.Id),
            new ReasoningNarrative("The resolved contradiction led to a direction shift."),
            Provenance()));
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService, reasoningRepository);

        IReadOnlyList<RepositoryDashboardProjection> dashboard = await projectionService.GetDashboardAsync();
        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        RepositoryReasoningSummary dashboardSummary = Assert.Single(dashboard).ReasoningSummary;
        Assert.Equal(4, dashboardSummary.EventCount);
        Assert.Equal(1, dashboardSummary.ThreadCount);
        Assert.Equal(1, dashboardSummary.RelationshipCount);
        Assert.Equal(1, dashboardSummary.HypothesisEventCount);
        Assert.Equal(1, dashboardSummary.AlternativeEventCount);
        Assert.Equal(1, dashboardSummary.ContradictionEventCount);
        Assert.Equal(1, dashboardSummary.DirectionEventCount);
        Assert.Equal(0, dashboardSummary.DecisionEvolutionEventCount);
        Assert.NotNull(dashboardSummary.LastEventAt);
        Assert.NotNull(dashboardSummary.LastThreadActivityAt);
        Assert.NotNull(dashboardSummary.LastRelationshipAt);
        Assert.NotNull(dashboardSummary.LastActivityAt);
        Assert.Null(dashboardSummary.LastReconstructionAt);
        Assert.Null(dashboardSummary.LastCertificationAt);
        Assert.Null(dashboardSummary.CertificationResult);
        Assert.Equal(dashboardSummary.EventCount, workspace.ReasoningSummary.EventCount);
        Assert.Equal(thread.Id, (await reasoningRepository.ListThreadsAsync(repository)).Single().Id);
    }

    [Fact]
    public async Task DashboardAndWorkspaceExposeDecisionSessionSummaryWhenAvailable()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        var observability = new StaticDecisionSessionObservabilityService();
        RepositoryProjectionService projectionService = CreateProjectionService(
            repositoryService,
            decisionSessionObservabilityService: observability);

        IReadOnlyList<RepositoryDashboardProjection> dashboard = await projectionService.GetDashboardAsync();
        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        RepositoryDecisionSessionSummary dashboardSummary = Assert.Single(dashboard).DecisionSessionSummary;
        Assert.Equal(observability.SessionId.ToString(), dashboardSummary.DecisionSessionId);
        Assert.Equal("Active", dashboardSummary.State);
        Assert.Equal("Transfer", dashboardSummary.LifecycleDecision);
        Assert.Equal("Eligible", dashboardSummary.TransferEligibilityStatus);
        Assert.Equal(250_000, dashboardSummary.EstimatedTokenCount);
        Assert.Equal(TimeSpan.FromMinutes(12), dashboardSummary.EstimatedCacheTtl);
        Assert.Equal(0.42m, dashboardSummary.CacheMissRisk);
        Assert.Equal(0.67m, dashboardSummary.CoherenceScore);
        Assert.Equal(0.81m, dashboardSummary.TransferPressure);
        RepositoryDecisionSessionHealthDimension health = Assert.Single(dashboardSummary.HealthDimensions);
        Assert.Equal("Lifecycle", health.Name);
        Assert.Equal("Warning", health.Status);
        Assert.Contains("Transfer pressure is elevated.", health.Findings);
        RepositoryDecisionSessionTransferSummary transfer = Assert.Single(dashboardSummary.RecentTransferLineage);
        Assert.Equal("transfer-1", transfer.TransferId);
        Assert.Equal(observability.SessionId.ToString(), transfer.SourceSessionId);
        Assert.Equal("artifact-1", transfer.ContinuityArtifactId);
        Assert.Contains("registry warning", dashboardSummary.Diagnostics);
        Assert.NotNull(dashboardSummary.GeneratedAt);

        Assert.Equal(dashboardSummary.DecisionSessionId, workspace.DecisionSessionSummary.DecisionSessionId);
        Assert.Equal(dashboardSummary.TransferPressure, workspace.DecisionSessionSummary.TransferPressure);
        Assert.Equal(dashboardSummary.HealthDimensions.Count, workspace.DecisionSessionSummary.HealthDimensions.Count);
    }

    [Fact]
    public async Task DecisionSessionSummaryRemainsEmptyWhenObservabilityIsAbsent()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        IReadOnlyList<RepositoryDashboardProjection> dashboard = await projectionService.GetDashboardAsync();
        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        Assert.Null(Assert.Single(dashboard).DecisionSessionSummary.DecisionSessionId);
        Assert.Null(workspace.DecisionSessionSummary.DecisionSessionId);
        Assert.Empty(workspace.DecisionSessionSummary.HealthDimensions);
        Assert.Empty(workspace.DecisionSessionSummary.RecentTransferLineage);
    }

    [Fact]
    public async Task WorkspaceProjectionIncludesLatestProposalReviewStateAndWarnings()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        var proposalStore = new FileSystemOperationalContextProposalStore(new FileSystemArtifactStore());
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        await proposalStore.SaveAsync(repository, new OperationalContextProposal
        {
            ProposalId = "proposal-1",
            RepositoryId = repository.Id,
            GeneratedAt = generatedAt,
            Status = OperationalContextProposalStatus.Accepted,
            BaselineCurrentContextHash = "baseline",
            GeneratedContentHash = "generated",
            InputFingerprints =
            [
                new OperationalContextInputFingerprint { Name = "CurrentHandoff", Present = true },
                new OperationalContextInputFingerprint { Name = "GeneratedProposal", Present = true, ByteCount = 15, CharacterCount = 15 }
            ],
            CompressionSummary = new OperationalContextCompressionSummary
            {
                Warnings = ["Stable decision rationale is missing."]
            },
            Review = new OperationalContextReview
            {
                ProposalId = "proposal-1",
                ReviewState = OperationalContextReviewState.Accepted,
                BaselineCurrentContextHash = "baseline",
                ReviewedContentHash = "generated",
                ReviewedAt = generatedAt
            }
        }, "# Operational Context");
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        Assert.Equal("proposal-1", workspace.OperationalContext.PendingProposalSummary.LatestProposalId);
        Assert.Equal(OperationalContextProposalStatus.Accepted, workspace.OperationalContext.PendingProposalSummary.Status);
        Assert.Equal(OperationalContextReviewState.Accepted, workspace.OperationalContext.LatestReviewState);
        Assert.Contains("Stable decision rationale", Assert.Single(workspace.OperationalContext.ContinuityWarnings));
    }

    [Fact]
    public async Task DashboardAndWorkspaceProjectPlanningReadiness()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/plan.md", "plan");
        await WriteAsync(repository, ".agents/milestones/m1.md", "# M1");
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        IReadOnlyList<RepositoryDashboardProjection> dashboard = await projectionService.GetDashboardAsync();
        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        RepositoryDashboardProjection dashboardProjection = Assert.Single(dashboard);
        Assert.Equal(ExecutionReadiness.Ready, dashboardProjection.Readiness);
        Assert.Equal(1, dashboardProjection.MilestoneCount);
        Assert.Equal(ExecutionReadiness.Ready, workspace.Readiness);
        Assert.True(workspace.HasPlan);
        Assert.Equal(1, workspace.MilestoneCount);
    }

    [Fact]
    public async Task DashboardAndWorkspaceProjectDefaultExecutionState()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        IReadOnlyList<RepositoryDashboardProjection> dashboard = await projectionService.GetDashboardAsync();
        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        RepositoryDashboardProjection dashboardProjection = Assert.Single(dashboard);
        Assert.Equal(RepositoryExecutionState.Ready, dashboardProjection.ExecutionState);
        Assert.Null(dashboardProjection.ActiveExecutionSession);
        Assert.Equal(RepositoryExecutionState.Ready, workspace.ExecutionState);
        Assert.Null(workspace.ExecutionSummary);
    }

    [Fact]
    public async Task DashboardAndWorkspaceProjectAwaitingAcceptanceSummary()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        var sessionId = Guid.NewGuid();
        RepositoryProjectionService projectionService = CreateProjectionService(
            repositoryService,
            new StaticExecutionSessionService(
                repository.Id,
                new ExecutionSessionSummary
                {
                    SessionId = sessionId,
                    State = ExecutionSessionState.Completed,
                    RepositoryState = RepositoryExecutionState.AwaitingAcceptance,
                    MilestonePath = ".agents/milestones/m4.md",
                    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
                    CompletedAt = DateTimeOffset.UtcNow,
                    Duration = TimeSpan.FromMinutes(3),
                    LastActivityAt = DateTimeOffset.UtcNow,
                    ProviderName = "fake",
                    HandoffPath = HandoffService.CurrentHandoffPath
                }));

        IReadOnlyList<RepositoryDashboardProjection> dashboard = await projectionService.GetDashboardAsync();
        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        RepositoryDashboardProjection dashboardProjection = Assert.Single(dashboard);
        Assert.Equal(RepositoryExecutionState.AwaitingAcceptance, dashboardProjection.ExecutionState);
        Assert.Null(dashboardProjection.ActiveExecutionSession);
        Assert.NotNull(dashboardProjection.ExecutionSummary);
        Assert.Equal(sessionId, dashboardProjection.ExecutionSummary.SessionId);
        ExecutionSessionSummary dashboardHistory = Assert.Single(dashboardProjection.ExecutionHistory);
        Assert.Equal(sessionId, dashboardHistory.SessionId);
        Assert.Equal(HandoffService.CurrentHandoffPath, dashboardProjection.ExecutionSummary.HandoffPath);
        Assert.Equal(TimeSpan.FromMinutes(3), dashboardProjection.ExecutionSummary.Duration);
        Assert.Equal(RepositoryExecutionState.AwaitingAcceptance, workspace.ExecutionState);
        Assert.NotNull(workspace.ExecutionSummary);
        ExecutionSessionSummary workspaceHistory = Assert.Single(workspace.ExecutionHistory);
        Assert.Equal(sessionId, workspaceHistory.SessionId);
        Assert.Equal(HandoffService.CurrentHandoffPath, workspace.ExecutionSummary.HandoffPath);
        Assert.Equal(TimeSpan.FromMinutes(3), workspace.ExecutionSummary.Duration);
    }

    [Fact]
    public async Task RefreshAfterAddingMilestoneUpdatesReadiness()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService repositoryService = CreateRepositoryService();
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/plan.md", "plan");
        RepositoryProjectionService projectionService = CreateProjectionService(repositoryService);

        RepositoryWorkspaceProjection beforeRefresh = await projectionService.GetWorkspaceAsync(repository.Id);
        await WriteAsync(repository, ".agents/milestones/m1.md", "# M1");
        RepositoryWorkspaceProjection afterRefresh = await projectionService.RefreshWorkspaceAsync(repository.Id);

        Assert.Equal(ExecutionReadiness.MissingMilestones, beforeRefresh.Readiness);
        Assert.Equal(0, beforeRefresh.MilestoneCount);
        Assert.Equal(ExecutionReadiness.Ready, afterRefresh.Readiness);
        Assert.Equal(1, afterRefresh.MilestoneCount);
    }

    private static RepositoryService CreateRepositoryService()
    {
        return new RepositoryService(new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
    }

    private static RepositoryProjectionService CreateProjectionService(IRepositoryService repositoryService)
    {
        return CreateProjectionService(repositoryService, new ReadyExecutionSessionService());
    }

    private static RepositoryProjectionService CreateProjectionService(
        IRepositoryService repositoryService,
        IDecisionArtifactProjectionService decisionArtifactProjectionService)
    {
        return CreateProjectionService(repositoryService, new ReadyExecutionSessionService(), decisionArtifactProjectionService);
    }

    private static RepositoryProjectionService CreateProjectionService(
        IRepositoryService repositoryService,
        IReasoningRepository reasoningRepository)
    {
        return CreateProjectionService(
            repositoryService,
            new ReadyExecutionSessionService(),
            null,
            reasoningRepository);
    }

    private static RepositoryProjectionService CreateProjectionService(
        IRepositoryService repositoryService,
        IDecisionSessionObservabilityService decisionSessionObservabilityService)
    {
        return CreateProjectionService(
            repositoryService,
            new ReadyExecutionSessionService(),
            null,
            null,
            decisionSessionObservabilityService);
    }

    private static RepositoryProjectionService CreateProjectionService(
        IRepositoryService repositoryService,
        IExecutionSessionService executionSessionService,
        IDecisionArtifactProjectionService? decisionArtifactProjectionService = null,
        IReasoningRepository? reasoningRepository = null,
        IDecisionSessionObservabilityService? decisionSessionObservabilityService = null)
    {
        return new RepositoryProjectionService(
            repositoryService,
            new ArtifactService(new FileSystemArtifactStore()),
            new PlanningService(new FileSystemArtifactStore()),
            executionSessionService,
            new FileSystemOperationalContextProposalStore(new FileSystemArtifactStore()),
            new MarkdownOperationalContextParser(),
            new FileSystemArtifactStore(),
            decisionArtifactProjectionService,
            reasoningRepository,
            decisionSessionObservabilityService);
    }

    private static IReasoningRepository CreateReasoningRepository()
    {
        return new FileSystemReasoningRepository(
            new FileSystemArtifactStore(),
            new ReasoningArtifactProjectionService());
    }

    private static CreateReasoningEventCommand EventCommand(
        ReasoningEventFamily family,
        ReasoningEventType type,
        string title)
    {
        return new CreateReasoningEventCommand(
            family,
            type,
            title,
            new ReasoningNarrative($"{title} summary."),
            [],
            Provenance(),
            [],
            []);
    }

    private static ReasoningProvenance Provenance()
    {
        return new ReasoningProvenance(
            "UserSupplied",
            "RepositoryProjectionServiceTests",
            ".agents/decisions/decisions.md");
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content);
    }

    private static string CreateGitRepositoryDirectory()
    {
        string directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        return directory;
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class ReadyExecutionSessionService : IExecutionSessionService
    {
        public Task RecoverAsync()
        {
            return Task.CompletedTask;
        }

        public Task<RepositoryExecutionState> GetRepositoryStateAsync(Guid repositoryId)
        {
            return Task.FromResult(RepositoryExecutionState.Ready);
        }

        public Task<ExecutionSessionSummary?> GetActiveSessionAsync(Guid repositoryId)
        {
            return Task.FromResult<ExecutionSessionSummary?>(null);
        }

        public Task<ExecutionSessionSummary?> GetRepositorySessionSummaryAsync(Guid repositoryId)
        {
            return Task.FromResult<ExecutionSessionSummary?>(null);
        }

        public Task<IReadOnlyList<ExecutionSessionSummary>> GetRepositorySessionHistoryAsync(Guid repositoryId, int limit = 10)
        {
            return Task.FromResult<IReadOnlyList<ExecutionSessionSummary>>([]);
        }

        public Task<ExecutionSessionSummary> StartAsync(Guid repositoryId, ExecutionStartRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSession?> GetSessionAsync(Guid sessionId)
        {
            return Task.FromResult<ExecutionSession?>(null);
        }

        public Task<ExecutionSessionSummary> AcceptAsync(Guid sessionId, ExecutionAcceptanceRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSessionSummary> RejectAsync(Guid sessionId, ExecutionAcceptanceRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<CommitPreparation> PrepareCommitAsync(Guid sessionId)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSessionSummary> CommitAsync(Guid sessionId, CommitRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSessionSummary> PushAsync(Guid sessionId, PushRequest request)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StaticExecutionSessionService(
        Guid repositoryId,
        ExecutionSessionSummary summary) : IExecutionSessionService
    {
        public Task RecoverAsync()
        {
            return Task.CompletedTask;
        }

        public Task<RepositoryExecutionState> GetRepositoryStateAsync(Guid requestedRepositoryId)
        {
            return Task.FromResult(requestedRepositoryId == repositoryId
                ? summary.RepositoryState
                : RepositoryExecutionState.Ready);
        }

        public Task<ExecutionSessionSummary?> GetActiveSessionAsync(Guid requestedRepositoryId)
        {
            return Task.FromResult<ExecutionSessionSummary?>(
                requestedRepositoryId == repositoryId && summary.RepositoryState == RepositoryExecutionState.Executing
                    ? summary
                    : null);
        }

        public Task<ExecutionSessionSummary?> GetRepositorySessionSummaryAsync(Guid requestedRepositoryId)
        {
            return Task.FromResult<ExecutionSessionSummary?>(
                requestedRepositoryId == repositoryId ? summary : null);
        }

        public Task<IReadOnlyList<ExecutionSessionSummary>> GetRepositorySessionHistoryAsync(Guid requestedRepositoryId, int limit = 10)
        {
            return Task.FromResult<IReadOnlyList<ExecutionSessionSummary>>(
                requestedRepositoryId == repositoryId ? [summary] : []);
        }

        public Task<ExecutionSessionSummary> StartAsync(Guid requestedRepositoryId, ExecutionStartRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSession?> GetSessionAsync(Guid sessionId)
        {
            return Task.FromResult<ExecutionSession?>(null);
        }

        public Task<ExecutionSessionSummary> AcceptAsync(Guid sessionId, ExecutionAcceptanceRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSessionSummary> RejectAsync(Guid sessionId, ExecutionAcceptanceRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<CommitPreparation> PrepareCommitAsync(Guid sessionId)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSessionSummary> CommitAsync(Guid sessionId, CommitRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSessionSummary> PushAsync(Guid sessionId, PushRequest request)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StaticDecisionSessionObservabilityService : IDecisionSessionObservabilityService
    {
        private readonly DateTimeOffset generatedAt = DateTimeOffset.Parse("2026-06-24T10:00:00Z");

        public DecisionSessionId SessionId { get; } = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

        public Task<DecisionSessionLifecycleProjection> GetProjectionAsync(Guid requestedRepositoryId)
        {
            DecisionSessionProjection activeSession = new(
                SessionId,
                requestedRepositoryId,
                DecisionSessionState.Active,
                generatedAt.AddHours(-2),
                generatedAt.AddHours(-2),
                null,
                "test");
            DecisionSessionMetrics metrics = new(
                250_000,
                1_000_000,
                12,
                3,
                4,
                5,
                0,
                0,
                1,
                generatedAt.AddMinutes(-10),
                generatedAt);
            DecisionSessionStatistics statistics = new(
                TimeSpan.FromHours(2),
                TimeSpan.FromHours(2),
                TimeSpan.FromMinutes(10),
                1m,
                1m);
            DecisionSessionCacheMetrics cache = new(
                TimeSpan.FromMinutes(12),
                0.42m,
                generatedAt.AddMinutes(12));
            DecisionSessionMetricsSnapshot metricsSnapshot = new(
                requestedRepositoryId,
                metrics,
                statistics,
                new DecisionSessionActivity(1, generatedAt.AddMinutes(-10), TimeSpan.FromMinutes(10), 1m),
                new DecisionSessionGrowth(1_000_000, 250_000, TimeSpan.FromHours(2), 1m),
                cache,
                new DecisionSessionMetricsDiagnostics(requestedRepositoryId, generatedAt, [], [], []),
                generatedAt);
            DecisionSessionCoherenceSnapshot coherenceSnapshot = new(
                requestedRepositoryId,
                new DecisionSessionCoherence(0.67m, 0.22m, 0.55m, 0.70m, 0.81m),
                null!,
                generatedAt);
            DecisionSessionLifecycleEvaluation evaluation = new(
                DecisionSessionLifecycleDecision.Transfer,
                0.40m,
                0.90m,
                "Transfer pressure exceeds reuse value.",
                ["high transfer pressure"],
                generatedAt);
            DecisionSessionLifecycleSnapshot policySnapshot = new(
                requestedRepositoryId,
                evaluation,
                null!,
                generatedAt);
            DecisionSessionTransferEligibilitySnapshot eligibilitySnapshot = new(
                requestedRepositoryId,
                new DecisionSessionTransferEligibility(
                    DecisionSessionTransferEligibilityStatus.Eligible,
                    evaluation,
                    SessionId,
                    [],
                    generatedAt),
                null!,
                generatedAt);
            DecisionSessionTransferEventProjection transfer = new(
                "transfer-1",
                SessionId,
                new DecisionSessionId(Guid.Parse("ffffffff-1111-2222-3333-444444444444")),
                generatedAt.AddMinutes(-5),
                generatedAt.AddMinutes(-4),
                true,
                "routine transfer",
                250_000,
                DecisionSessionLifecycleDecision.Transfer,
                0.40m,
                0.90m,
                DecisionSessionTransferEligibilityStatus.Eligible,
                "artifact-1",
                [],
                []);

            DecisionSessionLifecycleProjection projection = new(
                requestedRepositoryId,
                activeSession,
                [activeSession],
                metricsSnapshot,
                new DecisionSessionSizeProjection(
                    250_000,
                    1_000_000,
                    12,
                    5,
                    TimeSpan.FromHours(2),
                    TimeSpan.FromMinutes(10),
                    0.42m,
                    generatedAt),
                null,
                coherenceSnapshot,
                policySnapshot,
                eligibilitySnapshot,
                null,
                [],
                [],
                [],
                [transfer],
                [],
                new DecisionSessionDiagnostics(
                    requestedRepositoryId,
                    true,
                    1,
                    1,
                    [],
                    ["registry warning"],
                    generatedAt),
                generatedAt);

            return Task.FromResult(projection);
        }

        public Task<DecisionSessionLifecycleHistory> GetHistoryAsync(Guid requestedRepositoryId)
        {
            return Task.FromResult(new DecisionSessionLifecycleHistory(requestedRepositoryId, [], generatedAt));
        }

        public Task<DecisionSessionInfluenceTrace> GetInfluenceTraceAsync(Guid requestedRepositoryId)
        {
            return Task.FromResult(new DecisionSessionInfluenceTrace(requestedRepositoryId, SessionId, DecisionSessionLifecycleDecision.Transfer, DecisionSessionTransferEligibilityStatus.Eligible, [], [], generatedAt));
        }

        public Task<DecisionSessionHealthAssessment> GetHealthAsync(Guid requestedRepositoryId)
        {
            return Task.FromResult(new DecisionSessionHealthAssessment(
                requestedRepositoryId,
                [new DecisionSessionHealthDimension("Lifecycle", DecisionSessionHealthStatus.Warning, ["Transfer pressure is elevated."], ["coherence:0.67"])],
                new DecisionSessionInfluenceTrace(requestedRepositoryId, SessionId, DecisionSessionLifecycleDecision.Transfer, DecisionSessionTransferEligibilityStatus.Eligible, [], [], generatedAt),
                generatedAt));
        }
    }
}
