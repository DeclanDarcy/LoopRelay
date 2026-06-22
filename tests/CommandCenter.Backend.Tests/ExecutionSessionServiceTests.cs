using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Configuration;
using CommandCenter.Continuity;
using CommandCenter.Continuity.Services;
using CommandCenter.Execution;
using CommandCenter.Core.Planning;
using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Modules;
using CommandCenter.Execution.Primitives;
using CommandCenter.Execution.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionSessionServiceTests
{
    [Fact]
    public async Task ReadyRepositoryLaunchesWithFakeProvider()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteReadyArtifactsAsync(harness.Repository);

        ExecutionSessionSummary summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        Assert.Equal(ExecutionSessionState.Executing, summary.State);
        Assert.Equal(RepositoryExecutionState.Executing, summary.RepositoryState);
        Assert.Equal("fake", summary.ProviderName);
        Assert.Equal(RepositoryExecutionState.Executing, await harness.SessionService.GetRepositoryStateAsync(harness.Repository.Id));
    }

    [Fact]
    public async Task MissingPlanBlocksLaunch()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/milestones/m2.md", "milestone");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.StartAsync(
                harness.Repository.Id,
                new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" }));

        Assert.Contains("launch is blocked", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await harness.Store.LoadAsync());
    }

    [Fact]
    public async Task MissingMilestoneBlocksLaunch()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.StartAsync(
                harness.Repository.Id,
                new ExecutionStartRequest { MilestonePath = ".agents/milestones/missing.md" }));

        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await harness.Store.LoadAsync());
    }

    [Fact]
    public async Task ContextHardLimitBlocksLaunch()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", new string('a', 260 * 1024));
        await WriteAsync(harness.Repository, ".agents/milestones/m2.md", "milestone");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.StartAsync(
                harness.Repository.Id,
                new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" }));

        Assert.Contains("hard limit", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await harness.Store.LoadAsync());
    }

    [Fact]
    public async Task DirtyRepositoryLaunchSucceedsAndStoresSnapshot()
    {
        var dirtyState = new RepositoryDirtyState
        {
            ModifiedPaths = ["src/changed.cs"],
            IsClean = false
        };
        Harness harness = await CreateHarnessAsync(dirtyState);
        await WriteReadyArtifactsAsync(harness.Repository);

        ExecutionSessionSummary summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        ExecutionSession session = (await harness.Store.LoadAsync()).Single(session => session.Id == summary.SessionId);

        Assert.False(session.RepositorySnapshot!.DirtyState.IsClean);
        Assert.Contains("src/changed.cs", session.RepositorySnapshot.DirtyState.ModifiedPaths);
    }

    [Fact]
    public async Task DuplicateLaunchBlocks()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteReadyArtifactsAsync(harness.Repository);

        await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.StartAsync(
                harness.Repository.Id,
                new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" }));

        Assert.Contains("active execution", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await harness.Store.LoadAsync());
    }

    [Fact]
    public async Task FakeProviderFailureLeavesRepositoryReadyAndRecordsFailure()
    {
        var provider = new FakeExecutionProvider { FailOnStart = true };
        Harness harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);

        ExecutionSessionSummary summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        ExecutionSession session = (await harness.Store.LoadAsync()).Single();

        Assert.Equal(ExecutionSessionState.Failed, summary.State);
        Assert.Equal(RepositoryExecutionState.Ready, summary.RepositoryState);
        Assert.Equal(RepositoryExecutionState.Ready, await harness.SessionService.GetRepositoryStateAsync(harness.Repository.Id));
        Assert.Contains("failed to start", session.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StructuredProviderStartFailureLeavesRepositoryReadyAndRecordsFailure()
    {
        var provider = new FailingExecutionProvider(new ExecutionProviderException(
            "ProviderLaunchFailed",
            "Codex process failed to start."));
        Harness harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);

        ExecutionSessionSummary summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        ExecutionSession session = (await harness.Store.LoadAsync()).Single();

        Assert.Equal(ExecutionSessionState.Failed, summary.State);
        Assert.Equal(RepositoryExecutionState.Ready, summary.RepositoryState);
        Assert.Equal(RepositoryExecutionState.Ready, await harness.SessionService.GetRepositoryStateAsync(harness.Repository.Id));
        Assert.Contains("ProviderLaunchFailed", session.FailureReason);
    }

    [Fact]
    public async Task StartupRecoveryFailsPersistedExecutingSessionAfterStoreReload()
    {
        var provider = new MetadataExecutionProvider();
        Harness harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);
        ExecutionSessionSummary summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        var reloadedService = new ExecutionSessionService(
            harness.ContextService,
            new FileSystemExecutionSessionStore(harness.StorePath),
            new FakeExecutionProvider(),
            new ExecutionPromptBuilder(),
            new ExecutionMonitoringService(new FileSystemExecutionSessionStore(harness.StorePath)),
            new FakeGitService(null, null));

        await reloadedService.RecoverAsync();
        ExecutionSessionSummary? active = await reloadedService.GetActiveSessionAsync(harness.Repository.Id);
        ExecutionSessionSummary? repositorySummary = await reloadedService.GetRepositorySessionSummaryAsync(harness.Repository.Id);
        ExecutionSession? recoveredSession = await reloadedService.GetSessionAsync(summary.SessionId);

        Assert.Null(active);
        Assert.NotNull(repositorySummary);
        Assert.Equal(ExecutionSessionState.Failed, repositorySummary.State);
        Assert.Equal(RepositoryExecutionState.Failed, repositorySummary.RepositoryState);
        Assert.Equal(ExecutionSessionService.OrphanedProviderFailureReason, repositorySummary.FailureReason);
        Assert.Equal("C:\\tools\\codex.exe", repositorySummary.ProviderExecutablePath);
        Assert.Equal(7890, repositorySummary.ProviderProcessId);
        Assert.NotNull(recoveredSession);
        Assert.Equal(ExecutionSessionState.Failed, recoveredSession.State);
        Assert.Equal(RepositoryExecutionState.Failed, recoveredSession.RepositoryState);
        Assert.Equal(RepositoryExecutionState.Failed, await reloadedService.GetRepositoryStateAsync(harness.Repository.Id));
        Assert.Equal(ExecutionSessionService.OrphanedProviderFailureReason, recoveredSession.FailureReason);
        Assert.Equal("C:\\tools\\codex.exe", recoveredSession.ProviderExecutablePath);
        Assert.Equal(7890, recoveredSession.ProviderProcessId);
        Assert.NotNull(recoveredSession.ProviderStartedAt);
        Assert.NotNull(recoveredSession.PromptMetadata);
    }

    [Fact]
    public async Task StartupRecoveryKeepsExecutingSessionActiveWhenProviderReattachSucceeds()
    {
        var provider = new MetadataExecutionProvider();
        Harness harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);
        ExecutionSessionSummary summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        var reattachProvider = new FakeExecutionProvider
        {
            SupportsReattach = true,
            ReattachSucceeds = true
        };
        var reloadedStore = new FileSystemExecutionSessionStore(harness.StorePath);
        var reloadedMonitoringService = new ExecutionMonitoringService(reloadedStore);
        var reloadedService = new ExecutionSessionService(
            harness.ContextService,
            reloadedStore,
            reattachProvider,
            new ExecutionPromptBuilder(),
            reloadedMonitoringService,
            new FakeGitService(null, null));

        await reloadedService.RecoverAsync();
        ExecutionSessionSummary? active = await reloadedService.GetActiveSessionAsync(harness.Repository.Id);
        ExecutionSessionSummary? repositorySummary = await reloadedService.GetRepositorySessionSummaryAsync(harness.Repository.Id);
        ExecutionSession? recoveredSession = await reloadedService.GetSessionAsync(summary.SessionId);
        IReadOnlyList<ExecutionEvent> events = await reloadedMonitoringService.GetEventsAsync(summary.SessionId);

        Assert.NotNull(active);
        Assert.Equal(ExecutionSessionState.Executing, active.State);
        Assert.Equal(RepositoryExecutionState.Executing, active.RepositoryState);
        Assert.NotNull(repositorySummary);
        Assert.Equal(ExecutionSessionState.Executing, repositorySummary.State);
        Assert.NotNull(recoveredSession);
        Assert.Equal(ExecutionSessionState.Executing, recoveredSession.State);
        Assert.Equal(RepositoryExecutionState.Executing, await reloadedService.GetRepositoryStateAsync(harness.Repository.Id));
        ExecutionEvent recoveryEvent = Assert.Single(events, executionEvent => executionEvent.Type == ExecutionEventType.Recovery);
        Assert.Equal(ExecutionEventType.Recovery, recoveryEvent.Type);
        Assert.Equal(ExecutionSessionService.ReattachedProviderRecoveryMessage, recoveryEvent.Message);
    }

    [Fact]
    public async Task DashboardProjectionShowsRecoveredFailedStateAfterStoreReload()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteReadyArtifactsAsync(harness.Repository);
        await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        var reloadedService = new ExecutionSessionService(
            harness.ContextService,
            new FileSystemExecutionSessionStore(harness.StorePath),
            new FakeExecutionProvider(),
            new ExecutionPromptBuilder(),
            new ExecutionMonitoringService(new FileSystemExecutionSessionStore(harness.StorePath)),
            new FakeGitService(null, null));
        await reloadedService.RecoverAsync();
        var artifactStore = new FileSystemArtifactStore();
        var projectionService = new RepositoryProjectionService(
            harness.RepositoryService,
            new ArtifactService(artifactStore),
            new PlanningService(artifactStore),
            reloadedService,
            new FileSystemOperationalContextProposalStore(artifactStore),
            new MarkdownOperationalContextParser(),
            artifactStore);

        IReadOnlyList<RepositoryDashboardProjection> dashboard = await projectionService.GetDashboardAsync();
        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(harness.Repository.Id);

        RepositoryDashboardProjection dashboardProjection = Assert.Single(dashboard);
        Assert.Equal(RepositoryExecutionState.Failed, dashboardProjection.ExecutionState);
        Assert.Null(dashboardProjection.ActiveExecutionSession);
        Assert.NotNull(dashboardProjection.ExecutionSummary);
        Assert.Equal(ExecutionSessionState.Failed, dashboardProjection.ExecutionSummary.State);
        Assert.Equal(ExecutionSessionService.OrphanedProviderFailureReason, dashboardProjection.ExecutionSummary.FailureReason);
        Assert.Equal(RepositoryExecutionState.Failed, workspace.ExecutionState);
        Assert.NotNull(workspace.ExecutionSummary);
        Assert.Equal(ExecutionSessionState.Failed, workspace.ExecutionSummary.State);
        Assert.Equal(ExecutionSessionService.OrphanedProviderFailureReason, workspace.ExecutionSummary.FailureReason);
    }

    [Fact]
    public async Task ProviderStartFailureRemainsVisibleWhenRepositoryReturnsReady()
    {
        var provider = new FailingExecutionProvider(new ExecutionProviderException(
            "ProviderLaunchFailed",
            "Codex process failed to start."));
        Harness harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);

        ExecutionSessionSummary summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        ExecutionSessionSummary? active = await harness.SessionService.GetActiveSessionAsync(harness.Repository.Id);
        ExecutionSessionSummary? repositorySummary = await harness.SessionService.GetRepositorySessionSummaryAsync(harness.Repository.Id);

        Assert.Equal(ExecutionSessionState.Failed, summary.State);
        Assert.Equal(RepositoryExecutionState.Ready, summary.RepositoryState);
        Assert.Null(active);
        Assert.NotNull(repositorySummary);
        Assert.Equal(ExecutionSessionState.Failed, repositorySummary.State);
        Assert.Equal(RepositoryExecutionState.Ready, repositorySummary.RepositoryState);
        Assert.Contains("ProviderLaunchFailed", repositorySummary.FailureReason);
    }

    [Fact]
    public async Task StartupRecoveryDoesNotMutateNonExecutingSessions()
    {
        Harness harness = await CreateHarnessAsync();
        DateTimeOffset startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var failedSession = new ExecutionSession
        {
            Id = Guid.NewGuid(),
            RepositoryId = harness.Repository.Id,
            RepositoryPath = harness.Repository.Path,
            MilestonePath = ".agents/milestones/m2.md",
            StartedAt = startedAt,
            CompletedAt = startedAt.AddMinutes(1),
            LastActivityAt = startedAt.AddMinutes(1),
            State = ExecutionSessionState.Failed,
            RepositoryState = RepositoryExecutionState.Failed,
            ProviderName = "codex",
            ProviderExecutablePath = "C:\\tools\\codex.exe",
            ProviderProcessId = 7890,
            ProviderStartedAt = startedAt,
            PromptMetadata = new ExecutionPromptMetadata
            {
                RepositoryPath = harness.Repository.Path,
                MilestonePath = ".agents/milestones/m2.md",
                IncludedArtifactPaths = [".agents/plan.md"]
            },
            FailureReason = "Existing failure."
        };
        await harness.Store.SaveAsync([failedSession]);

        await harness.SessionService.RecoverAsync();
        ExecutionSession recoveredSession = (await harness.Store.LoadAsync()).Single();

        Assert.Equal(ExecutionSessionState.Failed, recoveredSession.State);
        Assert.Equal(RepositoryExecutionState.Failed, recoveredSession.RepositoryState);
        Assert.Equal("Existing failure.", recoveredSession.FailureReason);
        Assert.Equal(failedSession.CompletedAt, recoveredSession.CompletedAt);
        Assert.Equal("C:\\tools\\codex.exe", recoveredSession.ProviderExecutablePath);
        Assert.Equal(7890, recoveredSession.ProviderProcessId);
        Assert.NotNull(recoveredSession.PromptMetadata);
    }

    [Fact]
    public async Task PreviousCurrentHandoffSnapshotIsCapturedBeforeProviderStart()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteReadyArtifactsAsync(harness.Repository);
        await WriteAsync(harness.Repository, ".agents/handoffs/handoff.md", "previous handoff");

        ExecutionSessionSummary summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        ExecutionSession session = (await harness.Store.LoadAsync()).Single(session => session.Id == summary.SessionId);

        Assert.Equal("previous handoff", session.PreviousHandoffContent);
        Assert.NotNull(session.PreviousHandoffCapturedAt);
    }

    [Fact]
    public async Task ProviderReceivesExecutionPrompt()
    {
        var provider = new FakeExecutionProvider();
        Harness harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);

        await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        Assert.NotNull(provider.LastPrompt);
        Assert.Contains("Produce or update `.agents/handoffs/handoff.md`", provider.LastPrompt.Text);
        Assert.Equal(".agents/milestones/m2.md", provider.LastPrompt.Metadata.MilestonePath);
    }

    [Fact]
    public async Task ProviderLaunchMetadataPersistsAfterStoreReload()
    {
        var provider = new MetadataExecutionProvider();
        Harness harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);

        ExecutionSessionSummary summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        IReadOnlyList<ExecutionSession> sessions = await new FileSystemExecutionSessionStore(harness.StorePath).LoadAsync();
        ExecutionSession session = sessions.Single(session => session.Id == summary.SessionId);

        Assert.Equal("codex", session.ProviderName);
        Assert.Equal("C:\\tools\\codex.exe", session.ProviderExecutablePath);
        Assert.Equal(7890, session.ProviderProcessId);
        Assert.NotNull(session.ProviderStartedAt);
        Assert.NotNull(session.PromptMetadata);
        Assert.Equal(".agents/milestones/m2.md", session.PromptMetadata.MilestonePath);
        Assert.Equal([".agents/plan.md", ".agents/milestones/m2.md"], session.PromptMetadata.IncludedArtifactPaths);
    }

    [Fact]
    public async Task AcceptFromAwaitingAcceptanceWithChangedFilesTransitionsToAwaitingCommit()
    {
        var dirtyState = new RepositoryDirtyState
        {
            ModifiedPaths = ["src/changed.cs"],
            IsClean = false
        };
        Harness harness = await CreateHarnessAsync(dirtyState);
        ExecutionSession session = await StoreAwaitingAcceptanceSessionAsync(harness);

        ExecutionSessionSummary summary = await harness.SessionService.AcceptAsync(
            session.Id,
            new ExecutionAcceptanceRequest { DecisionNote = " Reviewed and accepted. " });
        ExecutionSession storedSession = (await harness.Store.LoadAsync()).Single(storedSession => storedSession.Id == session.Id);

        Assert.Equal(ExecutionSessionState.Completed, summary.State);
        Assert.Equal(RepositoryExecutionState.AwaitingCommit, summary.RepositoryState);
        Assert.NotNull(summary.AcceptedAt);
        Assert.Equal("Reviewed and accepted.", summary.DecisionNote);
        Assert.Equal(RepositoryExecutionState.AwaitingCommit, await harness.SessionService.GetRepositoryStateAsync(harness.Repository.Id));
        Assert.Equal(summary.AcceptedAt, storedSession.AcceptedAt);
        Assert.Equal("Reviewed and accepted.", storedSession.DecisionNote);
        Assert.False(storedSession.RepositorySnapshot!.DirtyState.IsClean);
    }

    [Fact]
    public async Task AcceptFromAwaitingAcceptanceWithCleanWorkingTreeTransitionsToReady()
    {
        Harness harness = await CreateHarnessAsync();
        ExecutionSession session = await StoreAwaitingAcceptanceSessionAsync(harness);

        ExecutionSessionSummary summary = await harness.SessionService.AcceptAsync(session.Id, new ExecutionAcceptanceRequest());

        Assert.Equal(ExecutionSessionState.Completed, summary.State);
        Assert.Equal(RepositoryExecutionState.Ready, summary.RepositoryState);
        Assert.NotNull(summary.AcceptedAt);
        Assert.Null(summary.DecisionNote);
        Assert.Equal(RepositoryExecutionState.Ready, await harness.SessionService.GetRepositoryStateAsync(harness.Repository.Id));
    }

    [Fact]
    public async Task RejectFromAwaitingAcceptanceTransitionsToReadyAndPreservesHandoff()
    {
        Harness harness = await CreateHarnessAsync();
        ExecutionSession session = await StoreAwaitingAcceptanceSessionAsync(harness);

        ExecutionSessionSummary summary = await harness.SessionService.RejectAsync(
            session.Id,
            new ExecutionAcceptanceRequest { DecisionNote = "Not sufficient" });
        ExecutionSession storedSession = (await harness.Store.LoadAsync()).Single(storedSession => storedSession.Id == session.Id);

        Assert.Equal(ExecutionSessionState.Completed, summary.State);
        Assert.Equal(RepositoryExecutionState.Ready, summary.RepositoryState);
        Assert.NotNull(summary.RejectedAt);
        Assert.Equal("Not sufficient", summary.DecisionNote);
        Assert.Equal(".agents/handoffs/handoff.md", summary.HandoffPath);
        Assert.Equal(".agents/handoffs/handoff.md", storedSession.HandoffPath);
        Assert.Equal(RepositoryExecutionState.Ready, await harness.SessionService.GetRepositoryStateAsync(harness.Repository.Id));
    }

    [Fact]
    public async Task AcceptOutsideAwaitingAcceptanceFails()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteReadyArtifactsAsync(harness.Repository);
        ExecutionSessionSummary summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.AcceptAsync(summary.SessionId, new ExecutionAcceptanceRequest()));

        Assert.Contains("awaiting acceptance", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectOutsideAwaitingAcceptanceFails()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteReadyArtifactsAsync(harness.Repository);
        ExecutionSessionSummary summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.RejectAsync(summary.SessionId, new ExecutionAcceptanceRequest()));

        Assert.Contains("awaiting acceptance", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcceptedStatePersistsAfterStoreReload()
    {
        var dirtyState = new RepositoryDirtyState
        {
            ModifiedPaths = ["src/changed.cs"],
            IsClean = false
        };
        Harness harness = await CreateHarnessAsync(dirtyState);
        ExecutionSession session = await StoreAwaitingAcceptanceSessionAsync(harness);

        await harness.SessionService.AcceptAsync(session.Id, new ExecutionAcceptanceRequest { DecisionNote = "accepted" });
        ExecutionSession reloadedSession = (await new FileSystemExecutionSessionStore(harness.StorePath).LoadAsync()).Single();

        Assert.Equal(ExecutionSessionState.Completed, reloadedSession.State);
        Assert.Equal(RepositoryExecutionState.AwaitingCommit, reloadedSession.RepositoryState);
        Assert.NotNull(reloadedSession.AcceptedAt);
        Assert.Null(reloadedSession.RejectedAt);
        Assert.Equal("accepted", reloadedSession.DecisionNote);
        Assert.Equal(".agents/handoffs/handoff.md", reloadedSession.HandoffPath);
    }

    [Fact]
    public async Task CommitFromAwaitingCommitPersistsMetadataAndTransitionsToAwaitingPush()
    {
        var gitService = new FakeGitService(
            new RepositoryDirtyState
            {
                ModifiedPaths = ["src/changed.cs"],
                IsClean = false
            },
            null);
        Harness harness = await CreateHarnessAsync(gitService: gitService);
        ExecutionSession session = await StoreAwaitingCommitSessionWithPreparationAsync(harness);

        ExecutionSessionSummary summary = await harness.SessionService.CommitAsync(
            session.Id,
            new CommitRequest
            {
                Message = "Reviewed commit",
                SelectedPaths = ["src/changed.cs"],
                StatusSnapshotId = "snapshot"
            });
        ExecutionSession storedSession = (await harness.Store.LoadAsync()).Single(storedSession => storedSession.Id == session.Id);

        Assert.Equal(RepositoryExecutionState.AwaitingPush, summary.RepositoryState);
        Assert.Equal("commit-sha", summary.CommitSha);
        Assert.Equal("Reviewed commit", summary.CommitMessage);
        Assert.Equal("snapshot", summary.PreparationSnapshotId);
        Assert.Equal(["src/changed.cs"], gitService.LastCommittedPaths);
        Assert.Equal("commit-sha", storedSession.CommitSha);
        Assert.Equal(RepositoryExecutionState.AwaitingPush, storedSession.RepositoryState);
    }

    [Fact]
    public async Task CommitRejectsEmptySelectedPaths()
    {
        Harness harness = await CreateHarnessAsync(
            new RepositoryDirtyState
            {
                ModifiedPaths = ["src/changed.cs"],
                IsClean = false
            });
        ExecutionSession session = await StoreAwaitingCommitSessionWithPreparationAsync(harness);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.CommitAsync(
                session.Id,
                new CommitRequest
                {
                    Message = "Reviewed commit",
                    SelectedPaths = [],
                    StatusSnapshotId = "snapshot"
                }));

        Assert.Contains("At least one path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommitRejectsStaleReviewedSnapshot()
    {
        Harness harness = await CreateHarnessAsync(
            new RepositoryDirtyState
            {
                ModifiedPaths = ["src/changed.cs"],
                IsClean = false
            });
        ExecutionSession session = await StoreAwaitingCommitSessionWithPreparationAsync(harness);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.CommitAsync(
                session.Id,
                new CommitRequest
                {
                    Message = "Reviewed commit",
                    SelectedPaths = ["src/changed.cs"],
                    StatusSnapshotId = "old-snapshot"
                }));

        Assert.Contains("stale status snapshot", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommitRejectsRepositoryStatusChangedAfterPreparation()
    {
        var gitService = new FakeGitService(
            new RepositoryDirtyState
            {
                ModifiedPaths = ["src/changed.cs", "src/other.cs"],
                IsClean = false
            },
            null)
        {
            CurrentSnapshotId = "changed-snapshot"
        };
        Harness harness = await CreateHarnessAsync(gitService: gitService);
        ExecutionSession session = await StoreAwaitingCommitSessionWithPreparationAsync(harness);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.CommitAsync(
                session.Id,
                new CommitRequest
                {
                    Message = "Reviewed commit",
                    SelectedPaths = ["src/changed.cs"],
                    StatusSnapshotId = "snapshot"
                }));

        Assert.Contains("Refresh commit review", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommitRejectsUnknownOrUnsafePaths()
    {
        Harness harness = await CreateHarnessAsync(
            new RepositoryDirtyState
            {
                ModifiedPaths = ["src/changed.cs"],
                IsClean = false
            });
        ExecutionSession session = await StoreAwaitingCommitSessionWithPreparationAsync(harness);

        var unknown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.CommitAsync(
                session.Id,
                new CommitRequest
                {
                    Message = "Reviewed commit",
                    SelectedPaths = ["src/unknown.cs"],
                    StatusSnapshotId = "snapshot"
                }));
        var escaping = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.CommitAsync(
                session.Id,
                new CommitRequest
                {
                    Message = "Reviewed commit",
                    SelectedPaths = ["../outside.cs"],
                    StatusSnapshotId = "snapshot"
                }));

        Assert.Contains("prepared commit scope", unknown.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("repository-relative", escaping.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommitFailureLeavesSessionAwaitingCommitForRetry()
    {
        var gitService = new FakeGitService(
            new RepositoryDirtyState
            {
                ModifiedPaths = ["src/changed.cs"],
                IsClean = false
            },
            null)
        {
            CommitFailure = "git commit failed"
        };
        Harness harness = await CreateHarnessAsync(gitService: gitService);
        ExecutionSession session = await StoreAwaitingCommitSessionWithPreparationAsync(harness);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.CommitAsync(
                session.Id,
                new CommitRequest
                {
                    Message = "Reviewed commit",
                    SelectedPaths = ["src/changed.cs"],
                    StatusSnapshotId = "snapshot"
                }));
        ExecutionSession storedSession = (await harness.Store.LoadAsync()).Single(storedSession => storedSession.Id == session.Id);

        Assert.Contains("git commit failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RepositoryExecutionState.AwaitingCommit, storedSession.RepositoryState);
        Assert.Null(storedSession.CommitSha);
    }

    [Fact]
    public async Task PushFromAwaitingPushPersistsMetadataAndTransitionsToReady()
    {
        var gitService = new FakeGitService(new RepositoryDirtyState { IsClean = true }, null);
        Harness harness = await CreateHarnessAsync(gitService: gitService);
        ExecutionSession session = await StoreAwaitingPushSessionAsync(harness);

        ExecutionSessionSummary summary = await harness.SessionService.PushAsync(session.Id, new PushRequest());
        ExecutionSession storedSession = (await harness.Store.LoadAsync()).Single(storedSession => storedSession.Id == session.Id);

        Assert.Equal(RepositoryExecutionState.Ready, summary.RepositoryState);
        Assert.Equal("commit-sha", summary.PushedCommitSha);
        Assert.NotNull(summary.PushAttemptedAt);
        Assert.NotNull(summary.PushedAt);
        Assert.Equal("main", summary.PushBranchName);
        Assert.Equal(RepositoryExecutionState.Ready, storedSession.RepositoryState);
        Assert.Equal("commit-sha", storedSession.PushedCommitSha);
        Assert.NotNull(storedSession.RepositorySnapshot);
        Assert.Equal("commit-sha", gitService.LastPushedCommitSha);
        Assert.Null(await harness.SessionService.GetActiveSessionAsync(harness.Repository.Id));
        ExecutionSessionSummary? latestSummary = await harness.SessionService.GetRepositorySessionSummaryAsync(harness.Repository.Id);
        Assert.NotNull(latestSummary);
        Assert.Equal(session.Id, latestSummary.SessionId);
        IReadOnlyList<ExecutionSessionSummary> history = await harness.SessionService.GetRepositorySessionHistoryAsync(harness.Repository.Id);
        ExecutionSessionSummary historySummary = Assert.Single(history);
        Assert.Equal(session.Id, historySummary.SessionId);
        Assert.Equal(RepositoryExecutionState.Ready, historySummary.RepositoryState);
    }

    [Fact]
    public async Task RepositorySessionHistoryReturnsNewestSessionsFirst()
    {
        Harness harness = await CreateHarnessAsync();
        DateTimeOffset firstStartedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        DateTimeOffset secondStartedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var firstSession = new ExecutionSession
        {
            Id = Guid.NewGuid(),
            RepositoryId = harness.Repository.Id,
            RepositoryPath = harness.Repository.Path,
            MilestonePath = ".agents/milestones/m7.md",
            StartedAt = firstStartedAt,
            CompletedAt = firstStartedAt.AddMinutes(1),
            LastActivityAt = firstStartedAt.AddMinutes(1),
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.Ready,
            ProviderName = "fake",
            PushedAt = firstStartedAt.AddMinutes(2),
            PushedCommitSha = "first-sha"
        };
        var secondSession = new ExecutionSession
        {
            Id = Guid.NewGuid(),
            RepositoryId = harness.Repository.Id,
            RepositoryPath = harness.Repository.Path,
            MilestonePath = ".agents/milestones/m8.md",
            StartedAt = secondStartedAt,
            CompletedAt = secondStartedAt.AddMinutes(1),
            LastActivityAt = secondStartedAt.AddMinutes(1),
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.Ready,
            ProviderName = "fake",
            PushedAt = secondStartedAt.AddMinutes(2),
            PushedCommitSha = "second-sha"
        };
        await harness.Store.SaveAsync([firstSession, secondSession]);

        IReadOnlyList<ExecutionSessionSummary> history = await harness.SessionService.GetRepositorySessionHistoryAsync(harness.Repository.Id, limit: 1);

        ExecutionSessionSummary summary = Assert.Single(history);
        Assert.Equal(secondSession.Id, summary.SessionId);
        Assert.Equal(".agents/milestones/m8.md", summary.MilestonePath);
        Assert.Equal("second-sha", summary.PushedCommitSha);
    }

    [Fact]
    public async Task RepeatableExecutionLoopRebuildsContextArchivesHandoffsAndSurvivesRestart()
    {
        var providerA = new FakeExecutionProvider();
        var gitService = new StatefulFakeGitService();
        Harness harness = await CreateHarnessAsync(provider: providerA, gitService: gitService);
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m8-a.md", "milestone A");
        await WriteAsync(harness.Repository, ".agents/milestones/m8-b.md", "milestone B");
        await WriteAsync(harness.Repository, ".agents/handoffs/handoff.md", "initial handoff");

        ExecutionSessionSummary first = await ExecuteLoopAsync(
            harness.SessionService,
            harness.MonitoringService,
            harness.Repository,
            ".agents/milestones/m8-a.md",
            "handoff A",
            "provider output A");

        Assert.Equal(RepositoryExecutionState.Ready, first.RepositoryState);
        Assert.Null(await harness.SessionService.GetActiveSessionAsync(harness.Repository.Id));
        Assert.NotNull(providerA.LastPrompt);
        Assert.Equal(".agents/milestones/m8-a.md", providerA.LastPrompt.Metadata.MilestonePath);
        Assert.Contains("milestone A", providerA.LastPrompt.Text);
        Assert.Equal("initial handoff", await ReadAsync(harness.Repository, ".agents/handoffs/handoff.0001.md"));
        Assert.Equal("handoff A", await ReadAsync(harness.Repository, ".agents/handoffs/handoff.md"));

        var providerB = new FakeExecutionProvider();
        var reloadedStore = new FileSystemExecutionSessionStore(harness.StorePath);
        ExecutionMonitoringService reloadedMonitoringService = CreateMonitoringService(reloadedStore);
        var reloadedService = new ExecutionSessionService(
            harness.ContextService,
            reloadedStore,
            providerB,
            new ExecutionPromptBuilder(),
            reloadedMonitoringService,
            gitService);

        Assert.Equal(RepositoryExecutionState.Ready, await reloadedService.GetRepositoryStateAsync(harness.Repository.Id));
        Assert.Null(await reloadedService.GetActiveSessionAsync(harness.Repository.Id));
        Assert.Single(await reloadedService.GetRepositorySessionHistoryAsync(harness.Repository.Id));

        ExecutionSessionSummary second = await ExecuteLoopAsync(
            reloadedService,
            reloadedMonitoringService,
            harness.Repository,
            ".agents/milestones/m8-b.md",
            "handoff B",
            "provider output B");
        IReadOnlyList<ExecutionSessionSummary> history = await reloadedService.GetRepositorySessionHistoryAsync(harness.Repository.Id);
        IReadOnlyList<ExecutionEvent> secondEvents = await reloadedMonitoringService.GetEventsAsync(second.SessionId);

        Assert.Equal(RepositoryExecutionState.Ready, second.RepositoryState);
        Assert.Null(await reloadedService.GetActiveSessionAsync(harness.Repository.Id));
        Assert.Equal(2, history.Count);
        Assert.Equal([second.SessionId, first.SessionId], history.Select(session => session.SessionId).ToArray());
        Assert.Contains(secondEvents, executionEvent =>
            executionEvent.Type == ExecutionEventType.StdOut &&
            executionEvent.Message == "provider output B");
        Assert.Contains(secondEvents, executionEvent => executionEvent.Type == ExecutionEventType.HandoffValidated);
        Assert.NotNull(providerB.LastPrompt);
        Assert.Equal(".agents/milestones/m8-b.md", providerB.LastPrompt.Metadata.MilestonePath);
        Assert.Contains("milestone B", providerB.LastPrompt.Text);
        Assert.DoesNotContain("milestone A", providerB.LastPrompt.Text);
        Assert.Equal("initial handoff", await ReadAsync(harness.Repository, ".agents/handoffs/handoff.0001.md"));
        Assert.Equal("handoff A", await ReadAsync(harness.Repository, ".agents/handoffs/handoff.0002.md"));
        Assert.Equal("handoff B", await ReadAsync(harness.Repository, ".agents/handoffs/handoff.md"));
    }

    [Fact]
    public async Task PushFailureLeavesSessionAwaitingPushForRetry()
    {
        var gitService = new FakeGitService(new RepositoryDirtyState { IsClean = true }, null)
        {
            PushFailure = "git push failed"
        };
        Harness harness = await CreateHarnessAsync(gitService: gitService);
        ExecutionSession session = await StoreAwaitingPushSessionAsync(harness);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.PushAsync(session.Id, new PushRequest()));
        ExecutionSession storedSession = (await harness.Store.LoadAsync()).Single(storedSession => storedSession.Id == session.Id);

        Assert.Contains("git push failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RepositoryExecutionState.AwaitingPush, storedSession.RepositoryState);
        Assert.NotNull(storedSession.PushAttemptedAt);
        Assert.Contains("git push failed", storedSession.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(storedSession.PushedAt);
        Assert.Null(storedSession.PushedCommitSha);
    }

    [Fact]
    public async Task LaunchEndpointReturnsSessionMetadata()
    {
        string configurationPath = Path.Combine(CreateTemporaryDirectory(), "configuration.json");
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        string? previousConfigurationPath = Environment.GetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH");
        string? previousStorePath = Environment.GetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH");
        Environment.SetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH", configurationPath);
        Environment.SetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH", storePath);

        try
        {
            string repositoryPath = CreateGitRepositoryDirectory();
            var repositoryService = new RepositoryService(new ApplicationConfigurationStore(configurationPath));
            Repository repository = await repositoryService.RegisterAsync(repositoryPath);
            await WriteReadyArtifactsAsync(repository);

            await using WebApplication app = Program.CreateApp(
                [],
                services =>
                {
                services.AddSingleton<IGitService>(new FakeGitService(null, null));
                    services.AddSingleton<IExecutionProvider>(new FakeExecutionProvider());
                    services.AddSingleton<IExecutionSessionStore>(new FileSystemExecutionSessionStore(storePath));
                });
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();

            using var client = new HttpClient();
            HttpResponseMessage response = await client.PostAsJsonAsync(
                app.Urls.Single() + $"/api/repositories/{repository.Id}/execution/start",
                new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var summary = await response.Content.ReadFromJsonAsync<ExecutionSessionSummary>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() }
            });
            Assert.NotNull(summary);
            Assert.Equal(ExecutionSessionState.Executing, summary.State);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH", previousConfigurationPath);
            Environment.SetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH", previousStorePath);
        }
    }

    [Fact]
    public async Task AppStartupRunsExecutionRecovery()
    {
        string configurationPath = Path.Combine(CreateTemporaryDirectory(), "configuration.json");
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        string? previousConfigurationPath = Environment.GetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH");
        string? previousStorePath = Environment.GetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH");
        Environment.SetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH", configurationPath);
        Environment.SetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH", storePath);

        try
        {
            string repositoryPath = CreateGitRepositoryDirectory();
            var repositoryService = new RepositoryService(new ApplicationConfigurationStore(configurationPath));
            Repository repository = await repositoryService.RegisterAsync(repositoryPath);
            var session = new ExecutionSession
            {
                Id = Guid.NewGuid(),
                RepositoryId = repository.Id,
                RepositoryPath = repository.Path,
                MilestonePath = ".agents/milestones/m2.md",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                LastActivityAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                State = ExecutionSessionState.Executing,
                RepositoryState = RepositoryExecutionState.Executing,
                ProviderName = "codex",
                ProviderExecutablePath = "C:\\tools\\codex.exe",
                ProviderProcessId = 7890,
                ProviderStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                PromptMetadata = new ExecutionPromptMetadata
                {
                    RepositoryPath = repository.Path,
                    MilestonePath = ".agents/milestones/m2.md",
                    IncludedArtifactPaths = [".agents/plan.md", ".agents/milestones/m2.md"]
                }
            };
            await new FileSystemExecutionSessionStore(storePath).SaveAsync([session]);

            await using WebApplication app = Program.CreateApp(
                [],
                services =>
                {
                    services.AddSingleton<IGitService>(new FakeGitService(null, null));
                    services.AddSingleton<IExecutionProvider>(new FakeExecutionProvider());
                    services.AddSingleton<IExecutionSessionStore>(new FileSystemExecutionSessionStore(storePath));
                });
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();

            using var client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{session.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var recoveredSession = await response.Content.ReadFromJsonAsync<ExecutionSession>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() }
            });
            Assert.NotNull(recoveredSession);
            Assert.Equal(ExecutionSessionState.Failed, recoveredSession.State);
            Assert.Equal(RepositoryExecutionState.Failed, recoveredSession.RepositoryState);
            Assert.Equal(ExecutionSessionService.OrphanedProviderFailureReason, recoveredSession.FailureReason);
            Assert.Equal("C:\\tools\\codex.exe", recoveredSession.ProviderExecutablePath);
            Assert.Equal(7890, recoveredSession.ProviderProcessId);
            Assert.NotNull(recoveredSession.PromptMetadata);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH", previousConfigurationPath);
            Environment.SetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH", previousStorePath);
        }
    }

    [Fact]
    public async Task AcceptAndRejectEndpointsReturnTransitionedSessionMetadata()
    {
        string configurationPath = Path.Combine(CreateTemporaryDirectory(), "configuration.json");
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        string? previousConfigurationPath = Environment.GetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH");
        string? previousStorePath = Environment.GetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH");
        Environment.SetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH", configurationPath);
        Environment.SetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH", storePath);

        try
        {
            string repositoryPath = CreateGitRepositoryDirectory();
            var repositoryService = new RepositoryService(new ApplicationConfigurationStore(configurationPath));
            Repository repository = await repositoryService.RegisterAsync(repositoryPath);
            ExecutionSession acceptSession = CreateAwaitingAcceptanceSession(repository, ".agents/milestones/m5-accept.md");
            ExecutionSession rejectSession = CreateAwaitingAcceptanceSession(repository, ".agents/milestones/m5-reject.md");
            await new FileSystemExecutionSessionStore(storePath).SaveAsync([acceptSession, rejectSession]);

            await using WebApplication app = Program.CreateApp(
                [],
                services =>
                {
                    services.AddSingleton<IGitService>(new FakeGitService(
                        new RepositoryDirtyState
                        {
                            ModifiedPaths = ["src/changed.cs"],
                            IsClean = false
                        },
                        null));
                    services.AddSingleton<IExecutionProvider>(new FakeExecutionProvider());
                    services.AddSingleton<IExecutionSessionStore>(new FileSystemExecutionSessionStore(storePath));
                });
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();

            using var client = new HttpClient();
            HttpResponseMessage acceptResponse = await client.PostAsJsonAsync(
                app.Urls.Single() + $"/api/execution-sessions/{acceptSession.Id}/accept",
                new ExecutionAcceptanceRequest { DecisionNote = "accepted" });
            HttpResponseMessage rejectResponse = await client.PostAsJsonAsync(
                app.Urls.Single() + $"/api/execution-sessions/{rejectSession.Id}/reject",
                new ExecutionAcceptanceRequest { DecisionNote = "rejected" });

            Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() }
            };
            var accepted = await acceptResponse.Content.ReadFromJsonAsync<ExecutionSessionSummary>(jsonOptions);
            var rejected = await rejectResponse.Content.ReadFromJsonAsync<ExecutionSessionSummary>(jsonOptions);

            Assert.NotNull(accepted);
            Assert.Equal(RepositoryExecutionState.AwaitingCommit, accepted.RepositoryState);
            Assert.NotNull(accepted.AcceptedAt);
            Assert.Equal("accepted", accepted.DecisionNote);
            Assert.NotNull(rejected);
            Assert.Equal(RepositoryExecutionState.Ready, rejected.RepositoryState);
            Assert.NotNull(rejected.RejectedAt);
            Assert.Equal("rejected", rejected.DecisionNote);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH", previousConfigurationPath);
            Environment.SetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH", previousStorePath);
        }
    }

    private static async Task<Harness> CreateHarnessAsync(
        RepositoryDirtyState? dirtyState = null,
        string? gitFailure = null,
        IExecutionProvider? provider = null,
        IGitService? gitService = null)
    {
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        Repository repository = await repositoryService.RegisterAsync(CreateGitRepositoryDirectory());
        var artifactStore = new FileSystemArtifactStore();
        var contextService = new ExecutionContextService(
            repositoryService,
            new ArtifactService(artifactStore),
            new PlanningService(artifactStore),
            gitService ?? new FakeGitService(dirtyState, gitFailure));
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        var store = new FileSystemExecutionSessionStore(storePath);
        ExecutionMonitoringService monitoringService = CreateMonitoringService(store);
        var sessionService = new ExecutionSessionService(
            contextService,
            store,
            provider ?? new FakeExecutionProvider(),
            new ExecutionPromptBuilder(),
            monitoringService,
            gitService ?? new FakeGitService(dirtyState, gitFailure));

        return new Harness(repositoryService, repository, contextService, store, storePath, monitoringService, sessionService);
    }

    private static ExecutionMonitoringService CreateMonitoringService(FileSystemExecutionSessionStore store)
    {
        return new ExecutionMonitoringService(
            store,
            new HandoffService(store, new FileSystemArtifactStore()));
    }

    private static async Task<ExecutionSessionSummary> ExecuteLoopAsync(
        ExecutionSessionService sessionService,
        ExecutionMonitoringService monitoringService,
        Repository repository,
        string milestonePath,
        string generatedHandoff,
        string providerOutput)
    {
        ExecutionSessionSummary started = await sessionService.StartAsync(
            repository.Id,
            new ExecutionStartRequest { MilestonePath = milestonePath });
        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sessionService.StartAsync(
                repository.Id,
                new ExecutionStartRequest { MilestonePath = milestonePath }));
        Assert.Contains("active execution", duplicate.Message, StringComparison.OrdinalIgnoreCase);

        IExecutionProviderObserver observer = monitoringService.CreateProviderObserver(started.SessionId);
        await observer.OnStdOutAsync(providerOutput);
        await WriteAsync(repository, ".agents/handoffs/handoff.md", generatedHandoff);
        await observer.OnProviderExitedAsync(0);

        ExecutionSession? completed = await sessionService.GetSessionAsync(started.SessionId);
        Assert.NotNull(completed);
        Assert.Equal(RepositoryExecutionState.AwaitingAcceptance, completed.RepositoryState);
        Assert.Equal(".agents/handoffs/handoff.md", completed.HandoffPath);

        ExecutionSessionSummary accepted = await sessionService.AcceptAsync(started.SessionId, new ExecutionAcceptanceRequest());
        Assert.Equal(RepositoryExecutionState.AwaitingCommit, accepted.RepositoryState);
        CommitPreparation preparation = await sessionService.PrepareCommitAsync(started.SessionId);
        ExecutionSessionSummary committed = await sessionService.CommitAsync(
            started.SessionId,
            new CommitRequest
            {
                Message = preparation.ProposedMessage,
                SelectedPaths = preparation.ScopeItems
                    .Where(item => item.IsSelected)
                    .Select(item => item.Path)
                    .ToArray(),
                StatusSnapshotId = preparation.StatusSnapshot.Id
            });
        Assert.Equal(RepositoryExecutionState.AwaitingPush, committed.RepositoryState);

        return await sessionService.PushAsync(started.SessionId, new PushRequest());
    }

    private static async Task WriteReadyArtifactsAsync(Repository repository)
    {
        await WriteAsync(repository, ".agents/plan.md", "plan");
        await WriteAsync(repository, ".agents/milestones/m2.md", "milestone");
    }

    private static async Task<ExecutionSession> StoreAwaitingAcceptanceSessionAsync(Harness harness)
    {
        ExecutionSession session = CreateAwaitingAcceptanceSession(harness.Repository, ".agents/milestones/m5.md");
        await harness.Store.SaveAsync([session]);
        return session;
    }

    private static async Task<ExecutionSession> StoreAwaitingCommitSessionWithPreparationAsync(Harness harness)
    {
        var session = new ExecutionSession
        {
            Id = Guid.NewGuid(),
            RepositoryId = harness.Repository.Id,
            RepositoryPath = harness.Repository.Path,
            MilestonePath = ".agents/milestones/m6-git-lifecycle.md",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
            AcceptedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
            LastActivityAt = DateTimeOffset.UtcNow.AddMinutes(-3),
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.AwaitingCommit,
            ProviderName = "fake",
            CommitPreparation = new CommitPreparation
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                RepositoryId = harness.Repository.Id,
                RepositoryPath = harness.Repository.Path,
                ProposedMessage = "m6-git-lifecycle\n\n- 1 file changed",
                ScopeItems =
                [
                    new CommitScopeItem
                    {
                        Path = "src/changed.cs",
                        ChangeType = CommitChangeType.Modified,
                        Origin = CommitChangeOrigin.ExecutionGenerated,
                        IsSelected = true
                    }
                ],
                StatusSnapshot = new CommitStatusSnapshot
                {
                    Id = "snapshot",
                    Branch = "main",
                    DirtyState = new RepositoryDirtyState
                    {
                        ModifiedPaths = ["src/changed.cs"],
                        IsClean = false
                    },
                    CapturedAt = DateTimeOffset.UtcNow.AddMinutes(-3)
                },
                GeneratedAt = DateTimeOffset.UtcNow.AddMinutes(-3)
            }
        };
        await harness.Store.SaveAsync([session]);
        return session;
    }

    private static async Task<ExecutionSession> StoreAwaitingPushSessionAsync(Harness harness)
    {
        var session = new ExecutionSession
        {
            Id = Guid.NewGuid(),
            RepositoryId = harness.Repository.Id,
            RepositoryPath = harness.Repository.Path,
            MilestonePath = ".agents/milestones/m6-git-lifecycle.md",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
            AcceptedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
            LastActivityAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.AwaitingPush,
            ProviderName = "fake",
            CommitSha = "commit-sha",
            CommittedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            CommitMessage = "Reviewed commit",
            PreparationSnapshotId = "snapshot"
        };
        await harness.Store.SaveAsync([session]);
        return session;
    }

    private static ExecutionSession CreateAwaitingAcceptanceSession(Repository repository, string milestonePath)
    {
        var sessionId = Guid.NewGuid();
        DateTimeOffset startedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        DateTimeOffset completedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        return new ExecutionSession
        {
            Id = sessionId,
            RepositoryId = repository.Id,
            RepositoryPath = repository.Path,
            MilestonePath = milestonePath,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            LastActivityAt = completedAt,
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.AwaitingAcceptance,
            ProviderName = "fake",
            HandoffPath = ".agents/handoffs/handoff.md",
            Events =
            [
                new ExecutionEvent
                {
                    Sequence = 1,
                    Type = ExecutionEventType.StdOut,
                    Timestamp = completedAt,
                    Message = "done"
                }
            ]
        };
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

    private static Task<string> ReadAsync(Repository repository, string relativePath)
    {
        return File.ReadAllTextAsync(Path.Combine(
            repository.Path,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
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

    private sealed record Harness(
        RepositoryService RepositoryService,
        Repository Repository,
        ExecutionContextService ContextService,
        FileSystemExecutionSessionStore Store,
        string StorePath,
        ExecutionMonitoringService MonitoringService,
        ExecutionSessionService SessionService);

    private sealed class FakeGitService(RepositoryDirtyState? dirtyState, string? failure) : IGitService
    {
        public string CurrentSnapshotId { get; init; } = "snapshot";

        public string? CommitFailure { get; init; }

        public string? PushFailure { get; init; }

        public IReadOnlyList<string> LastCommittedPaths { get; private set; } = [];

        public string? LastPushedCommitSha { get; private set; }

        public Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository)
        {
            if (failure is not null)
            {
                throw new InvalidOperationException(failure);
            }

            return Task.FromResult(new ExecutionRepositorySnapshot
            {
                Branch = "main",
                DirtyState = dirtyState ?? new RepositoryDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<RepositoryGitStatus> GetStatusAsync(Repository repository)
        {
            if (failure is not null)
            {
                throw new InvalidOperationException(failure);
            }

            return Task.FromResult(new RepositoryGitStatus
            {
                Branch = "main",
                DirtyState = dirtyState ?? new RepositoryDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<CommitPreparation> PrepareCommitAsync(Repository repository, ExecutionSession session)
        {
            if (failure is not null)
            {
                throw new InvalidOperationException(failure);
            }

            return Task.FromResult(new CommitPreparation
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                RepositoryId = session.RepositoryId,
                RepositoryPath = session.RepositoryPath,
                ProposedMessage = $"{Path.GetFileNameWithoutExtension(session.MilestonePath)}\n\n- 1 file changed",
                ScopeItems =
                [
                    new CommitScopeItem
                    {
                        Path = "src/changed.cs",
                        ChangeType = CommitChangeType.Modified,
                        Origin = CommitChangeOrigin.ExecutionGenerated,
                        IsSelected = true
                    }
                ],
                StatusSnapshot = new CommitStatusSnapshot
                {
                    Id = "snapshot",
                    Branch = "main",
                    DirtyState = dirtyState ?? new RepositoryDirtyState(),
                    CapturedAt = DateTimeOffset.UtcNow
                },
                GeneratedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<CommitStatusSnapshot> GetCommitStatusSnapshotAsync(Repository repository)
        {
            if (failure is not null)
            {
                throw new InvalidOperationException(failure);
            }

            return Task.FromResult(new CommitStatusSnapshot
            {
                Id = CurrentSnapshotId,
                Branch = "main",
                DirtyState = dirtyState ?? new RepositoryDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<CommitResult> CommitAsync(
            Repository repository,
            string message,
            IReadOnlyList<string> selectedPaths,
            string preparationSnapshotId)
        {
            if (CommitFailure is not null)
            {
                throw new InvalidOperationException(CommitFailure);
            }

            LastCommittedPaths = selectedPaths;
            return Task.FromResult(new CommitResult
            {
                CommitSha = "commit-sha",
                CommittedAt = DateTimeOffset.UtcNow,
                CommitMessage = message,
                PreparationSnapshotId = preparationSnapshotId,
                SelectedPaths = selectedPaths
            });
        }

        public Task<PushResult> PushAsync(Repository repository, string? commitSha)
        {
            if (PushFailure is not null)
            {
                throw new InvalidOperationException(PushFailure);
            }

            LastPushedCommitSha = commitSha;
            return Task.FromResult(new PushResult
            {
                PushAttemptedAt = DateTimeOffset.UtcNow,
                PushedAt = DateTimeOffset.UtcNow,
                PushedCommitSha = commitSha,
                BranchName = "main"
            });
        }
    }

    private sealed class StatefulFakeGitService : IGitService
    {
        private int commitCount;
        private string currentSnapshotId = "snapshot-initial";

        public Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository)
        {
            return Task.FromResult(new ExecutionRepositorySnapshot
            {
                Branch = "main",
                DirtyState = BuildDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<RepositoryGitStatus> GetStatusAsync(Repository repository)
        {
            return Task.FromResult(new RepositoryGitStatus
            {
                Branch = "main",
                DirtyState = BuildDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<CommitPreparation> PrepareCommitAsync(Repository repository, ExecutionSession session)
        {
            currentSnapshotId = $"snapshot-{session.Id:N}";
            return Task.FromResult(new CommitPreparation
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                RepositoryId = session.RepositoryId,
                RepositoryPath = session.RepositoryPath,
                ProposedMessage = $"{Path.GetFileNameWithoutExtension(session.MilestonePath)}\n\n- 1 file changed",
                ScopeItems =
                [
                    new CommitScopeItem
                    {
                        Path = $"src/{Path.GetFileNameWithoutExtension(session.MilestonePath)}.cs",
                        ChangeType = CommitChangeType.Modified,
                        Origin = CommitChangeOrigin.ExecutionGenerated,
                        IsSelected = true
                    }
                ],
                StatusSnapshot = new CommitStatusSnapshot
                {
                    Id = currentSnapshotId,
                    Branch = "main",
                    DirtyState = BuildDirtyState(),
                    CapturedAt = DateTimeOffset.UtcNow
                },
                GeneratedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<CommitStatusSnapshot> GetCommitStatusSnapshotAsync(Repository repository)
        {
            return Task.FromResult(new CommitStatusSnapshot
            {
                Id = currentSnapshotId,
                Branch = "main",
                DirtyState = BuildDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<CommitResult> CommitAsync(
            Repository repository,
            string message,
            IReadOnlyList<string> selectedPaths,
            string preparationSnapshotId)
        {
            commitCount++;
            return Task.FromResult(new CommitResult
            {
                CommitSha = $"commit-sha-{commitCount}",
                CommittedAt = DateTimeOffset.UtcNow,
                CommitMessage = message,
                PreparationSnapshotId = preparationSnapshotId,
                SelectedPaths = selectedPaths
            });
        }

        public Task<PushResult> PushAsync(Repository repository, string? commitSha)
        {
            return Task.FromResult(new PushResult
            {
                PushAttemptedAt = DateTimeOffset.UtcNow,
                PushedAt = DateTimeOffset.UtcNow,
                PushedCommitSha = commitSha,
                BranchName = "main"
            });
        }

        private static RepositoryDirtyState BuildDirtyState()
        {
            return new RepositoryDirtyState
            {
                ModifiedPaths = ["src/changed.cs"],
                IsClean = false
            };
        }
    }

    private sealed class MetadataExecutionProvider : IExecutionProvider
    {
        public string Name => "codex";

        public bool SupportsReattach => false;

        public Task<ExecutionProviderStartResult> StartAsync(
            ExecutionPrompt prompt,
            ExecutionSession session,
            IExecutionProviderObserver observer)
        {
            return Task.FromResult(new ExecutionProviderStartResult
            {
                ProviderName = Name,
                ExecutablePath = "C:\\tools\\codex.exe",
                ProcessId = 7890,
                StartedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<bool> TryReattachAsync(
            ExecutionSession session,
            IExecutionProviderObserver observer)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class FailingExecutionProvider(Exception exception) : IExecutionProvider
    {
        public string Name => "codex";

        public bool SupportsReattach => false;

        public Task<ExecutionProviderStartResult> StartAsync(
            ExecutionPrompt prompt,
            ExecutionSession session,
            IExecutionProviderObserver observer)
        {
            throw exception;
        }

        public Task<bool> TryReattachAsync(
            ExecutionSession session,
            IExecutionProviderObserver observer)
        {
            return Task.FromResult(false);
        }
    }
}
