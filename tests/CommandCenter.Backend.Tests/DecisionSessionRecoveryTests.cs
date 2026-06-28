using CommandCenter.Core.Repositories;
using CommandCenter.Continuity.Services;
using CommandCenter.Core.Artifacts;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.Decisions.Services;
using CommandCenter.DecisionSessions.Services;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Persistence;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionSessionRecoveryTests
{
    [Fact]
    public async Task ActiveSessionRecoversAfterRestart()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        DecisionSession active = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
        var restartedRecovery = new DecisionSessionRecoveryService(
            harness.RepositoryService,
            harness.RepositoryStore,
            TimeProvider.System);

        DecisionSessionRecoveryResult result = await restartedRecovery.RecoverAsync(harness.Repository.Id);
        DecisionSessionRecoveryHistory history = await restartedRecovery.GetHistoryAsync(harness.Repository.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(active.Id, result.ActiveSessionId);
        Assert.Equal(1, result.ActiveSessionCount);
        Assert.Single(history.Results);
    }

    [Fact]
    public async Task CompletedTransferRecoversReplacementAsActive()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession source = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        source = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, source.Id);
        source = await harness.Registry.MarkTransferPendingAsync(harness.Repository.Id, source.Id, "transfer pressure");
        DecisionSession replacement = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "decision-session-transfer");
        replacement = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, replacement.Id);
        source = await harness.Registry.MarkTransferredAsync(harness.Repository.Id, source.Id, replacement.Id, "transfer pressure");
        await harness.RepositoryStore.WriteTransferAsync(
            harness.Repository,
            CreateTransfer(harness.Repository.Id, source.Id, replacement.Id, now, succeeded: true));

        DecisionSessionRecoveryResult result = await harness.Recovery.RecoverAsync(harness.Repository.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(replacement.Id, result.ActiveSessionId);
        Assert.Contains(result.Diagnostics.TransferAssessments, assessment => assessment.Status == "Completed");
    }

    [Fact]
    public async Task TransferPendingAfterRestartEmitsDiagnostics()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        DecisionSession active = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
        await harness.Registry.MarkTransferPendingAsync(harness.Repository.Id, active.Id, "transfer pressure");

        DecisionSessionRecoveryResult result = await harness.Recovery.RecoverAsync(harness.Repository.Id);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Findings, finding => finding.Code == "NoActiveSession");
        Assert.Contains(result.Findings, finding => finding.Code == "PendingBeforeArtifact");
        Assert.Contains(result.Diagnostics.TransferAssessments, assessment => assessment.Status == "PendingBeforeArtifact");
    }

    [Fact]
    public async Task DuplicateActiveSessionsProduceRecoveryFinding()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession first = DecisionSession.Create(harness.Repository.Id, "test", now) with
        {
            State = DecisionSessionState.Active,
            ActivatedAt = now
        };
        DecisionSession second = DecisionSession.Create(harness.Repository.Id, "test", now.AddMinutes(1)) with
        {
            State = DecisionSessionState.Active,
            ActivatedAt = now.AddMinutes(1)
        };
        await harness.WriteRegistryAsync([first, second]);

        DecisionSessionRecoveryResult result = await harness.Recovery.RecoverAsync(harness.Repository.Id);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Findings, finding =>
            finding.Code == "RegistryInvalid" &&
            finding.Message.Contains("More than one active decision session", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecoveryRebuildsMissingAndCorruptDerivedSnapshots()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
        await harness.Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, DecisionSessionArtifactPaths.MetricsSnapshotJson()),
            "{ not valid json");
        await harness.Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, DecisionSessionArtifactPaths.EconomicsSnapshotJson()),
            "{ not valid json");
        await harness.Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, DecisionSessionArtifactPaths.CoherenceSnapshotJson()),
            "{ not valid json");
        await harness.Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, DecisionSessionArtifactPaths.LifecyclePolicySnapshotJson()),
            "{ not valid json");

        DecisionSessionRecoveryService recovery = CreateRecoveryWithAnalysisStack(harness);

        DecisionSessionRecoveryResult result = await recovery.RecoverAsync(harness.Repository.Id);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Findings, finding => finding.Code == "MetricsSnapshotRebuilt");
        Assert.Contains(result.Findings, finding => finding.Code == "EconomicsSnapshotRebuilt");
        Assert.Contains(result.Findings, finding => finding.Code == "CoherenceSnapshotRebuilt");
        Assert.Contains(result.Findings, finding => finding.Code == "LifecyclePolicySnapshotRebuilt");
        Assert.Contains(result.Findings, finding => finding.Code == "TransferEligibilitySnapshotRebuilt");
        Assert.NotNull(await harness.RepositoryStore.ReadMetricsSnapshotAsync(harness.Repository));
        Assert.NotNull(await harness.RepositoryStore.ReadEconomicsSnapshotAsync(harness.Repository));
        Assert.NotNull(await harness.RepositoryStore.ReadCoherenceSnapshotAsync(harness.Repository));
        Assert.NotNull(await harness.RepositoryStore.ReadLifecyclePolicySnapshotAsync(harness.Repository));
        Assert.NotNull(await harness.RepositoryStore.ReadTransferEligibilitySnapshotAsync(harness.Repository));
    }

    [Fact]
    public async Task HostedRecoveryContinuesAfterRepositoryFailure()
    {
        Repository first = new() { Id = Guid.NewGuid(), Name = "first", Path = "first" };
        Repository second = new() { Id = Guid.NewGuid(), Name = "second", Path = "second" };
        var repositoryService = new DecisionSessionTestRepositoryService(first, second);
        var recoveryService = new ThrowingOnceRecoveryService(first.Id);
        var hosted = new DecisionSessionRecoveryHostedService(repositoryService, recoveryService);

        await hosted.StartAsync(CancellationToken.None);

        Assert.Contains(second.Id, recoveryService.RecoveredRepositories);
    }

    [Fact]
    public async Task RecoverySkipsRebuildWhenSourceUnchangedButRebuildsWhenSourceChanges()
    {
        string repositoryPath = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repositoryPath);
        var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = repositoryPath };
        var store = new FileSystemArtifactStore();
        var repositoryService = new DecisionSessionTestRepositoryService(repository);
        var sessionRepository = new FileSystemDecisionSessionRepository(store);
        var registry = new DecisionSessionRegistry(repositoryService, sessionRepository);
        var reasoningRepository = new FileSystemReasoningRepository(store, new ReasoningArtifactProjectionService());

        DecisionSession created = await registry.CreateSessionAsync(repository.Id, "test");
        await registry.ActivateSessionAsync(repository.Id, created.Id);

        // Write one real source file so the source tree is probeable.
        await reasoningRepository.CreateEventAsync(repository, EventCommand("Initial reasoning evidence."));

        DecisionSessionRecoveryService recovery = CreateFileSystemRecoveryStack(
            repository,
            store,
            repositoryService,
            sessionRepository,
            registry,
            reasoningRepository);

        // First recovery: stamps the derived snapshots from source evidence.
        DecisionSessionRecoveryResult first = await recovery.RecoverAsync(repository.Id);
        Assert.True(first.Succeeded);
        Assert.Contains(first.Findings, finding => finding.Code == "MetricsSnapshotRebuilt");

        // Second recovery with source untouched: the four count-derived snapshots are skipped, but transfer
        // eligibility is always rebuilt.
        DecisionSessionRecoveryResult second = await recovery.RecoverAsync(repository.Id);
        Assert.True(second.Succeeded);
        Assert.Contains(second.Findings, finding => finding.Code == "MetricsSnapshotNotRebuilt");
        Assert.Contains(second.Findings, finding => finding.Code == "EconomicsSnapshotNotRebuilt");
        Assert.Contains(second.Findings, finding => finding.Code == "CoherenceSnapshotNotRebuilt");
        Assert.Contains(second.Findings, finding => finding.Code == "LifecyclePolicySnapshotNotRebuilt");
        Assert.Contains(second.Findings, finding => finding.Code == "TransferEligibilitySnapshotRebuilt");
        Assert.DoesNotContain(second.Findings, finding => finding.Code == "MetricsSnapshotRebuilt");

        // Mutate a source file (write a new reasoning event) and force a strictly-later write time so the
        // probe deterministically observes a changed source tree.
        ReasoningEvent mutation = await reasoningRepository.CreateEventAsync(repository, EventCommand("New reasoning evidence."));
        string mutationPath = ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.EventJson(mutation.Id));
        File.SetLastWriteTimeUtc(mutationPath, DateTime.UtcNow.AddHours(1));

        DecisionSessionRecoveryResult third = await recovery.RecoverAsync(repository.Id);
        Assert.True(third.Succeeded);
        Assert.Contains(third.Findings, finding => finding.Code == "MetricsSnapshotRebuilt");
        Assert.Contains(third.Findings, finding => finding.Code == "EconomicsSnapshotRebuilt");
        Assert.Contains(third.Findings, finding => finding.Code == "CoherenceSnapshotRebuilt");
        Assert.Contains(third.Findings, finding => finding.Code == "LifecyclePolicySnapshotRebuilt");

        // Corrupt the metrics snapshot: recovery cannot read the stamp and must rebuild.
        await store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.MetricsSnapshotJson()),
            "{ not valid json");

        DecisionSessionRecoveryResult fourth = await recovery.RecoverAsync(repository.Id);
        Assert.True(fourth.Succeeded);
        Assert.Contains(fourth.Findings, finding => finding.Code == "MetricsSnapshotRebuilt");
    }

    private static CreateReasoningEventCommand EventCommand(string title)
    {
        return new CreateReasoningEventCommand(
            ReasoningEventFamily.DecisionEvolution,
            ReasoningEventType.DecisionReframed,
            title,
            new ReasoningNarrative("The session evidence changed."),
            [],
            new ReasoningProvenance("test", "test"),
            [],
            ["decision-session"]);
    }

    private static DecisionSessionRecoveryService CreateFileSystemRecoveryStack(
        Repository repository,
        FileSystemArtifactStore store,
        DecisionSessionTestRepositoryService repositoryService,
        FileSystemDecisionSessionRepository sessionRepository,
        DecisionSessionRegistry registry,
        FileSystemReasoningRepository reasoningRepository)
    {
        var decisionRepository = new InMemoryDecisionRepository();
        var contextStore = new FileSystemOperationalContextProposalStore(store);
        var artifactService = new ArtifactService(store);
        var evidenceReader = new DecisionSessionEvidenceReader(decisionRepository, reasoningRepository, contextStore, artifactService);
        var metricsService = new DecisionSessionMetricsService(
            repositoryService,
            registry,
            sessionRepository,
            evidenceReader,
            new DeterministicTokenEstimator(),
            TimeProvider.System);
        var economicsService = new DecisionSessionEconomicsService(
            repositoryService,
            sessionRepository,
            metricsService,
            new DecisionSessionEconomicsOptions(),
            TimeProvider.System);
        var graphService = new ReasoningGraphService(repositoryService, reasoningRepository, store);
        var coherenceService = new DecisionSessionCoherenceService(
            repositoryService,
            sessionRepository,
            metricsService,
            economicsService,
            graphService,
            new DecisionSessionCoherenceOptions(),
            TimeProvider.System);
        var lifecyclePolicy = new DecisionSessionLifecyclePolicy(
            repositoryService,
            sessionRepository,
            metricsService,
            economicsService,
            coherenceService,
            new DecisionSessionLifecyclePolicyOptions(),
            TimeProvider.System);
        return new DecisionSessionRecoveryService(
            repositoryService,
            sessionRepository,
            TimeProvider.System,
            metricsService,
            economicsService,
            coherenceService,
            lifecyclePolicy,
            evidenceReader);
    }

    private static DecisionSessionTransfer CreateTransfer(
        Guid repositoryId,
        CommandCenter.DecisionSessions.Primitives.DecisionSessionId sourceSessionId,
        CommandCenter.DecisionSessions.Primitives.DecisionSessionId targetSessionId,
        DateTimeOffset now,
        bool succeeded)
    {
        string transferId = $"transfer.{now.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.{sourceSessionId}.json";
        var started = new DecisionSessionTransferEvent(
            $"{transferId}.started",
            DecisionSessionTransferEventType.Started,
            repositoryId,
            sourceSessionId,
            targetSessionId,
            null,
            now,
            "Decision session transfer started.",
            []);
        var completed = new DecisionSessionTransferEvent(
            $"{transferId}.completed",
            succeeded ? DecisionSessionTransferEventType.Completed : DecisionSessionTransferEventType.Failed,
            repositoryId,
            sourceSessionId,
            targetSessionId,
            null,
            now.AddSeconds(1),
            "Decision session transfer completed.",
            []);
        return new DecisionSessionTransfer(
            transferId,
            repositoryId,
            sourceSessionId,
            targetSessionId,
            null,
            now,
            now.AddSeconds(1),
            succeeded,
            [started, completed],
            []);
    }

    private static DecisionSessionRecoveryService CreateRecoveryWithAnalysisStack(DecisionSessionTestHarness harness)
    {
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(harness.Store, new ReasoningArtifactProjectionService());
        var contextStore = new FileSystemOperationalContextProposalStore(harness.Store);
        var artifactService = new ArtifactService(harness.Store);
        var evidenceReader = new DecisionSessionEvidenceReader(decisionRepository, reasoningRepository, contextStore, artifactService);
        var metricsService = new DecisionSessionMetricsService(
            harness.RepositoryService,
            harness.Registry,
            harness.RepositoryStore,
            evidenceReader,
            new DeterministicTokenEstimator(),
            TimeProvider.System);
        var economicsService = new DecisionSessionEconomicsService(
            harness.RepositoryService,
            harness.RepositoryStore,
            metricsService,
            new DecisionSessionEconomicsOptions(),
            TimeProvider.System);
        var graphService = new ReasoningGraphService(harness.RepositoryService, reasoningRepository, harness.Store);
        var coherenceService = new DecisionSessionCoherenceService(
            harness.RepositoryService,
            harness.RepositoryStore,
            metricsService,
            economicsService,
            graphService,
            new DecisionSessionCoherenceOptions(),
            TimeProvider.System);
        var lifecyclePolicy = new DecisionSessionLifecyclePolicy(
            harness.RepositoryService,
            harness.RepositoryStore,
            metricsService,
            economicsService,
            coherenceService,
            new DecisionSessionLifecyclePolicyOptions(),
            TimeProvider.System);
        return new DecisionSessionRecoveryService(
            harness.RepositoryService,
            harness.RepositoryStore,
            TimeProvider.System,
            metricsService,
            economicsService,
            coherenceService,
            lifecyclePolicy,
            evidenceReader);
    }

    private sealed class ThrowingOnceRecoveryService(Guid repositoryIdToThrow) : IDecisionSessionRecoveryService
    {
        public List<Guid> RecoveredRepositories { get; } = [];

        public Task<DecisionSessionDiagnostics> GetDiagnosticsAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }

        public Task<DecisionSessionRecoveryResult> RecoverAsync(Guid repositoryId)
        {
            if (repositoryId == repositoryIdToThrow)
            {
                throw new InvalidOperationException("repository recovery failed");
            }

            RecoveredRepositories.Add(repositoryId);
            return Task.FromResult(new DecisionSessionRecoveryResult(
                "recovery.test.json",
                repositoryId,
                true,
                null,
                0,
                [],
                new DecisionSessionRecoveryDiagnostics(
                    repositoryId,
                    DateTimeOffset.UtcNow,
                    new DecisionSessionDiagnostics(repositoryId, true, 0, 0, [], [], DateTimeOffset.UtcNow),
                    [],
                    []),
                [],
                DateTimeOffset.UtcNow));
        }

        public Task<DecisionSessionRecoveryResult> GetRecoveryAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }

        public Task<DecisionSessionRecoveryHistory> GetHistoryAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }

        public Task<DecisionSessionRecoveryDiagnostics> GetRecoveryDiagnosticsAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }
    }
}
