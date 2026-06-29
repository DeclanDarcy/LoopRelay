using CommandCenter.Core.Repositories;
using CommandCenter.Continuity.Services;
using CommandCenter.Core.Artifacts;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.Decisions.Services;
using CommandCenter.DecisionSessions.Services;
using CommandCenter.Persistence.Sqlite;
using CommandCenter.Persistence.Sqlite.Abstractions;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Persistence;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;
using Dapper;
using Microsoft.Data.Sqlite;

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
    public async Task RecoveryResultPersistsToSqliteAndHistoryReadsFromSqlite()
    {
        // Phase 4 (refactor-lazy-sqlite.md): recovery-result audit rows move from
        // .agents/decision-sessions/recovery/*.json to the per-repo SQLite recovery_result table. With the
        // store wired, an explicit POST /recovery (persist:true) writes a row to SQLite and GetHistoryAsync
        // reads it back from SQLite — shape-identical, with the file path untouched (proving a clean migration).
        string repositoryPath = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repositoryPath);
        string globalDatabasePath = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", $"cc-{Guid.NewGuid():N}.db");
        try
        {
            var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = repositoryPath };
            var store = new FileSystemArtifactStore();
            var repositoryService = new DecisionSessionTestRepositoryService(repository);
            var sessionRepository = new FileSystemDecisionSessionRepository(store);
            var registry = new DecisionSessionRegistry(repositoryService, sessionRepository);
            var connectionFactory = new SqliteConnectionFactory(new SqliteDatabaseOptions(globalDatabasePath));
            var recoveryResultStore = new SqliteRecoveryResultStore(connectionFactory, DecisionSessionJson.Options);

            DecisionSession created = await registry.CreateSessionAsync(repository.Id, "test");
            DecisionSession active = await registry.ActivateSessionAsync(repository.Id, created.Id);

            var recovery = new DecisionSessionRecoveryService(
                repositoryService,
                sessionRepository,
                TimeProvider.System,
                recoveryResultStore: recoveryResultStore);

            DecisionSessionRecoveryResult result = await recovery.RecoverAsync(repository.Id);
            DecisionSessionRecoveryHistory history = await recovery.GetHistoryAsync(repository.Id);

            // History is served from SQLite, shape-identical to the result just written.
            Assert.True(result.Succeeded);
            Assert.Single(history.Results);
            Assert.Equal(result.RecoveryId, history.Results[0].RecoveryId);
            Assert.Equal(active.Id, history.Results[0].ActiveSessionId);
            Assert.Equal(result.RecoveredAt, history.Results[0].RecoveredAt);

            // The SQLite recovery_result table holds the row; the file plane stays EMPTY (clean migration).
            await using (SqliteConnection connection = await connectionFactory.OpenRepositoryConnectionAsync(repository, CancellationToken.None))
            {
                long rows = await connection.ExecuteScalarAsync<long>(
                    "SELECT COUNT(*) FROM recovery_result WHERE repository_id = @Repo;",
                    new { Repo = repository.Id.ToString() });
                Assert.Equal(1, rows);
            }

            IReadOnlyList<DecisionSessionRecoveryResult> fileResults =
                await sessionRepository.ListRecoveryResultsAsync(repository);
            Assert.Empty(fileResults);
        }
        finally
        {
            TryDeleteDirectory(repositoryPath);
            TryDeleteFile(globalDatabasePath);
        }
    }

    [Fact]
    public async Task SqliteRecoveryResultStoreUpsertsByRecoveryId()
    {
        // The store is keyed (repository_id, recovery_id) with a last-writer-wins UPSERT, so re-writing the
        // same recovery_id replaces the row rather than appending a duplicate — preserving GetHistoryAsync's
        // one-row-per-explicit-recovery cadence.
        string repositoryPath = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repositoryPath);
        string globalDatabasePath = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", $"cc-{Guid.NewGuid():N}.db");
        try
        {
            var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = repositoryPath };
            var connectionFactory = new SqliteConnectionFactory(new SqliteDatabaseOptions(globalDatabasePath));
            var recoveryResultStore = new SqliteRecoveryResultStore(connectionFactory, DecisionSessionJson.Options);

            DateTimeOffset occurredAt = DateTimeOffset.Parse("2026-06-24T10:00:00Z");
            var first = new DecisionSessionRecoveryResult(
                "recovery.20260624T100000.0000000Z.json", repository.Id, true, null, 0, [], NoDiagnostics(repository.Id, occurredAt), [], occurredAt);
            var second = first with { ActiveSessionCount = 7 };

            await recoveryResultStore.WriteAsync(repository, first.RecoveryId, first.RecoveredAt, first, CancellationToken.None);
            await recoveryResultStore.WriteAsync(repository, second.RecoveryId, second.RecoveredAt, second, CancellationToken.None);

            IReadOnlyList<DecisionSessionRecoveryResult> rows =
                await recoveryResultStore.ListAsync<DecisionSessionRecoveryResult>(repository, CancellationToken.None);

            Assert.Single(rows);
            Assert.Equal(7, rows[0].ActiveSessionCount);
        }
        finally
        {
            TryDeleteDirectory(repositoryPath);
            TryDeleteFile(globalDatabasePath);
        }
    }

    private static DecisionSessionRecoveryDiagnostics NoDiagnostics(Guid repositoryId, DateTimeOffset at) =>
        new(repositoryId, at, new DecisionSessionDiagnostics(repositoryId, true, 0, 0, [], [], at), [], []);

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

    // Phase 3 retarget (refactor-lazy-sqlite.md): derived snapshots are no longer persisted as files, so the
    // recovery rebuild no longer leaves files on disk. The preserved invariant is unchanged: a cold derived
    // cache (and any leftover corrupt analysis files, now irrelevant) drives a full rebuild that emits the
    // Rebuilt findings, AND the served snapshots compute cleanly afterward (proving the analysis was rebuilt
    // from authoritative evidence rather than from corrupt state).
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

        AnalysisStack stack = CreateAnalysisStack(harness);

        DecisionSessionRecoveryResult result = await stack.Recovery.RecoverAsync(harness.Repository.Id);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Findings, finding => finding.Code == "MetricsSnapshotRebuilt");
        Assert.Contains(result.Findings, finding => finding.Code == "EconomicsSnapshotRebuilt");
        Assert.Contains(result.Findings, finding => finding.Code == "CoherenceSnapshotRebuilt");
        Assert.Contains(result.Findings, finding => finding.Code == "LifecyclePolicySnapshotRebuilt");
        Assert.Contains(result.Findings, finding => finding.Code == "TransferEligibilitySnapshotRebuilt");

        // The analysis recomputes cleanly from authoritative evidence (the served read path, now active compute).
        Assert.NotNull(await stack.Metrics.GetMetricsAsync(harness.Repository.Id));
        Assert.NotNull(await stack.Economics.GetEconomicsAsync(harness.Repository.Id));
        Assert.NotNull(await stack.Coherence.GetCoherenceAsync(harness.Repository.Id));
        Assert.NotNull(await stack.Policy.EvaluateAsync(harness.Repository.Id));
    }

    [Fact]
    public async Task HostedRecoveryContinuesAfterRepositoryFailure()
    {
        Repository first = new() { Id = Guid.NewGuid(), Name = "first", Path = "first" };
        Repository second = new() { Id = Guid.NewGuid(), Name = "second", Path = "second" };
        var repositoryService = new DecisionSessionTestRepositoryService(first, second);
        var recoveryService = new ThrowingOnceRecoveryService(first.Id);
        // Phase 5 retarget (refactor-lazy-sqlite.md): the deleted DecisionSessionRecoveryHostedService body now
        // lives verbatim in the on-demand DecisionSessionRecoveryRunner. Driving RecoverAllAsync preserves the
        // original invariant — one repository's recovery failure must not strand the others.
        var runner = new DecisionSessionRecoveryRunner(
            repositoryService, recoveryService, new PerRepositoryRecoveryGate());

        await runner.RecoverAllAsync(CancellationToken.None);

        Assert.Contains(second.Id, recoveryService.RecoveredRepositories);
    }

    [Fact]
    public async Task RecoverySkipsRebuildWhenSourceFingerprintUnchangedButRebuildsWhenContentChanges()
    {
        // Phase 2 of the Derivation Cache refactor (refactor-lazy-sqlite.md): the warm-restart staleness KEY
        // is a deterministic per-family source CONTENT fingerprint, NOT the fragile mtime probe. As of Phase 3
        // the cached SOURCE-PURE metrics base IS the warm-restart record (a cache HIT keyed by that fingerprint
        // is the skip), replacing the metrics snapshot FILE stamp. This test proves three invariants:
        //   (1) skip-on-unchanged   — re-running recovery with byte-identical source is a cache hit and skips;
        //   (2) rebuild-on-change   — adding real source CONTENT shifts the fingerprint to a cache MISS and
        //                             forces a rebuild WITHOUT touching any mtime (the old probe's lever);
        //   (3) rebuild-on-lost-base — a recovery whose cache has no base for the current fingerprint rebuilds.
        string repositoryPath = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repositoryPath);
        string globalDatabasePath = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", $"cc-{Guid.NewGuid():N}.db");
        try
        {
            var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = repositoryPath };
            var store = new FileSystemArtifactStore();
            var repositoryService = new DecisionSessionTestRepositoryService(repository);
            var sessionRepository = new FileSystemDecisionSessionRepository(store);
            var registry = new DecisionSessionRegistry(repositoryService, sessionRepository);
            var reasoningRepository = new FileSystemReasoningRepository(store, new ReasoningArtifactProjectionService());

            // Real fingerprint provider over a per-test global DB; the per-repo derived-cache.db that memoizes
            // the source fingerprint lands inside this temp repo's .agents and is dropped with the repo dir.
            var connectionFactory = new SqliteConnectionFactory(new SqliteDatabaseOptions(globalDatabasePath));
            var fingerprintProvider = new DefaultSourceFingerprintProvider(connectionFactory, TimeProvider.System);
            var cache = new MemorySqliteSnapshotCache();

            DecisionSession created = await registry.CreateSessionAsync(repository.Id, "test");
            await registry.ActivateSessionAsync(repository.Id, created.Id);

            // Write one real source file so the source tree has content to fingerprint.
            await reasoningRepository.CreateEventAsync(repository, EventCommand("Initial reasoning evidence."));

            DecisionSessionRecoveryService recovery = CreateFileSystemRecoveryStack(
                repository,
                store,
                repositoryService,
                sessionRepository,
                registry,
                reasoningRepository,
                fingerprintProvider,
                cache);

            // First recovery: computes and caches the source-pure metrics base under the content fingerprint.
            DecisionSessionRecoveryResult first = await recovery.RecoverAsync(repository.Id);
            Assert.True(first.Succeeded);
            Assert.Contains(first.Findings, finding => finding.Code == "MetricsSnapshotRebuilt");

            // The cached base now exists under the current source-content fingerprint (the Phase 3 warm-restart key).
            string currentFingerprint = await fingerprintProvider.ForFamiliesAsync(
                repository, DecisionSessionAnalysisCache.MetricsFamilies, CancellationToken.None);
            DecisionSessionMetricsBase? cachedBase = await cache.TryGetAsync<DecisionSessionMetricsBase>(
                repository.Id,
                DecisionSessionAnalysisCache.MetricsKind,
                currentFingerprint,
                DecisionSessionAnalysisCache.FormulaVersion,
                CancellationToken.None);
            Assert.NotNull(cachedBase);

            // (1) Second recovery with source byte-identical: the four count-derived snapshots are skipped
            // (cache hit), but transfer eligibility is always rebuilt.
            DecisionSessionRecoveryResult second = await recovery.RecoverAsync(repository.Id);
            Assert.True(second.Succeeded);
            Assert.Contains(second.Findings, finding => finding.Code == "MetricsSnapshotNotRebuilt");
            Assert.Contains(second.Findings, finding => finding.Code == "EconomicsSnapshotNotRebuilt");
            Assert.Contains(second.Findings, finding => finding.Code == "CoherenceSnapshotNotRebuilt");
            Assert.Contains(second.Findings, finding => finding.Code == "LifecyclePolicySnapshotNotRebuilt");
            Assert.Contains(second.Findings, finding => finding.Code == "TransferEligibilitySnapshotRebuilt");
            Assert.DoesNotContain(second.Findings, finding => finding.Code == "MetricsSnapshotRebuilt");

            // (2) Add real source CONTENT (a new reasoning event). This shifts the content fingerprint to a
            // cache MISS and forces a rebuild — WITHOUT any mtime manipulation, proving the key is content.
            await reasoningRepository.CreateEventAsync(repository, EventCommand("New reasoning evidence."));

            DecisionSessionRecoveryResult third = await recovery.RecoverAsync(repository.Id);
            Assert.True(third.Succeeded);
            Assert.Contains(third.Findings, finding => finding.Code == "MetricsSnapshotRebuilt");
            Assert.Contains(third.Findings, finding => finding.Code == "EconomicsSnapshotRebuilt");
            Assert.Contains(third.Findings, finding => finding.Code == "CoherenceSnapshotRebuilt");
            Assert.Contains(third.Findings, finding => finding.Code == "LifecyclePolicySnapshotRebuilt");

            // (3) A recovery whose cache has no base for the current fingerprint (here an empty cache, the
            // analogue of the old lost-stamp case) cannot skip and must rebuild.
            DecisionSessionRecoveryService coldCacheRecovery = CreateFileSystemRecoveryStack(
                repository,
                store,
                repositoryService,
                sessionRepository,
                registry,
                reasoningRepository,
                fingerprintProvider,
                new MemorySqliteSnapshotCache());

            DecisionSessionRecoveryResult fourth = await coldCacheRecovery.RecoverAsync(repository.Id);
            Assert.True(fourth.Succeeded);
            Assert.Contains(fourth.Findings, finding => finding.Code == "MetricsSnapshotRebuilt");
        }
        finally
        {
            TryDeleteDirectory(repositoryPath);
            TryDeleteFile(globalDatabasePath);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
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
        FileSystemReasoningRepository reasoningRepository,
        ISourceFingerprintProvider? fingerprintProvider,
        IDerivedSnapshotCache cache)
    {
        var decisionRepository = new InMemoryDecisionRepository();
        var contextStore = new FileSystemOperationalContextProposalStore(store);
        var artifactService = new ArtifactService(store);
        var evidenceReader = new DecisionSessionEvidenceReader(decisionRepository, reasoningRepository, contextStore, artifactService);
        // The services and the recovery skip-check share the SAME cache + fingerprint provider, so a populated
        // metrics base is a warm-restart hit and a source-content change is a miss (a full rebuild).
        var derivedReader = fingerprintProvider is null
            ? null
            : new DerivedSnapshotReader(cache, fingerprintProvider, TimeProvider.System);
        var metricsService = new DecisionSessionMetricsService(
            repositoryService,
            registry,
            sessionRepository,
            evidenceReader,
            new DeterministicTokenEstimator(),
            TimeProvider.System,
            derivedReader);
        var economicsService = new DecisionSessionEconomicsService(
            repositoryService,
            sessionRepository,
            metricsService,
            new DecisionSessionEconomicsOptions(),
            TimeProvider.System,
            derivedReader);
        var graphService = new ReasoningGraphService(repositoryService, reasoningRepository, store);
        var coherenceService = new DecisionSessionCoherenceService(
            repositoryService,
            sessionRepository,
            metricsService,
            economicsService,
            graphService,
            new DecisionSessionCoherenceOptions(),
            TimeProvider.System,
            derivedReader);
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
            evidenceReader,
            fingerprintProvider,
            cache);
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

    private sealed record AnalysisStack(
        DecisionSessionRecoveryService Recovery,
        IDecisionSessionMetricsService Metrics,
        IDecisionSessionEconomicsService Economics,
        IDecisionSessionCoherenceService Coherence,
        IDecisionSessionLifecyclePolicy Policy);

    // Phase 3: the analysis services route their source-pure base through a derived-snapshot reader (here a
    // memory cache), so a cold cache rebuilds and a populated cache is a warm-restart hit. The recovery service
    // shares the SAME cache so its skip decision keys on the cached base, not a metrics snapshot file.
    private static AnalysisStack CreateAnalysisStack(DecisionSessionTestHarness harness)
    {
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(harness.Store, new ReasoningArtifactProjectionService());
        var contextStore = new FileSystemOperationalContextProposalStore(harness.Store);
        var artifactService = new ArtifactService(harness.Store);
        var evidenceReader = new DecisionSessionEvidenceReader(decisionRepository, reasoningRepository, contextStore, artifactService);

        var cache = new MemorySqliteSnapshotCache();
        var globalDatabasePath = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", $"cc-{Guid.NewGuid():N}.db");
        var connectionFactory = new SqliteConnectionFactory(new SqliteDatabaseOptions(globalDatabasePath));
        var fingerprintProvider = new DefaultSourceFingerprintProvider(connectionFactory, TimeProvider.System);
        var derivedReader = new DerivedSnapshotReader(cache, fingerprintProvider, TimeProvider.System);

        var metricsService = new DecisionSessionMetricsService(
            harness.RepositoryService,
            harness.Registry,
            harness.RepositoryStore,
            evidenceReader,
            new DeterministicTokenEstimator(),
            TimeProvider.System,
            derivedReader);
        var economicsService = new DecisionSessionEconomicsService(
            harness.RepositoryService,
            harness.RepositoryStore,
            metricsService,
            new DecisionSessionEconomicsOptions(),
            TimeProvider.System,
            derivedReader);
        var graphService = new ReasoningGraphService(harness.RepositoryService, reasoningRepository, harness.Store);
        var coherenceService = new DecisionSessionCoherenceService(
            harness.RepositoryService,
            harness.RepositoryStore,
            metricsService,
            economicsService,
            graphService,
            new DecisionSessionCoherenceOptions(),
            TimeProvider.System,
            derivedReader);
        var lifecyclePolicy = new DecisionSessionLifecyclePolicy(
            harness.RepositoryService,
            harness.RepositoryStore,
            metricsService,
            economicsService,
            coherenceService,
            new DecisionSessionLifecyclePolicyOptions(),
            TimeProvider.System);
        var recovery = new DecisionSessionRecoveryService(
            harness.RepositoryService,
            harness.RepositoryStore,
            TimeProvider.System,
            metricsService,
            economicsService,
            coherenceService,
            lifecyclePolicy,
            evidenceReader,
            fingerprintProvider,
            cache);
        return new AnalysisStack(recovery, metricsService, economicsService, coherenceService, lifecyclePolicy);
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
