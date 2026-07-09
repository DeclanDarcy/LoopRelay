using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Archive;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Models.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.Splits;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Persistence;
using LoopRelay.Roadmap.Cli.Services.Splits;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Persistence;

public sealed class WorkflowPersistenceCoordinatorTests
{
    [Fact]
    public async Task Coordinator_records_completed_marker_for_successful_persistence_phase()
    {
        using var repo = new TempRepo();
        var coordinator = new WorkflowPersistenceCoordinator();
        bool executed = false;

        WorkflowPersistenceResult result = await coordinator.ExecuteAsync(
            repo.Repository,
            WorkflowPersistenceUnit.JournalEventEmission,
            "correlation-1",
            _ =>
            {
                executed = true;
                return Task.CompletedTask;
            });

        IReadOnlyList<WorkflowRecoveryFinding> findings = await coordinator.ClassifyAsync(repo.Repository);
        Assert.True(executed);
        Assert.Equal(WorkflowPersistenceMarkerStatus.Completed, result.Status);
        Assert.Empty(findings);
    }

    [Fact]
    public async Task Coordinator_records_failed_marker_for_failed_persistence_phase()
    {
        using var repo = new TempRepo();
        var coordinator = new WorkflowPersistenceCoordinator();

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync(
            repo.Repository,
            WorkflowPersistenceUnit.RoadmapTransitionSave,
            "correlation-2",
            _ => throw new InvalidOperationException("boom")));

        WorkflowRecoveryFinding finding = Assert.Single(await coordinator.ClassifyAsync(repo.Repository));
        Assert.Equal(WorkflowRecoveryClassification.Corrupt, finding.Classification);
        Assert.Equal(WorkflowPersistenceUnit.RoadmapTransitionSave.ToString(), finding.WorkflowName);
        Assert.Equal("correlation-2", finding.CorrelationId);
        Assert.Contains("failed persistence phase", finding.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Covered_workflow_boundaries_execute_through_coordinator()
    {
        using var repo = new TempRepo();
        var coordinator = new RecordingWorkflowPersistenceCoordinator();

        await new CoordinatedDecisionLedgerStore(
            new InMemoryDecisionLedgerStore(),
            repo.Repository,
            coordinator).AppendAsync(Decision("D0001"));
        await new CoordinatedTransitionJournalStore(
            new RecordingTransitionJournalStore(),
            repo.Repository,
            coordinator).AppendAsync(JournalRecord());
        await new CoordinatedExecutionPreparationManifestStore(
            new InMemoryExecutionPreparationManifestStore(),
            repo.Repository,
            coordinator).SaveAsync(ExecutionPreparationManifest.Empty);
        await new CoordinatedSplitFamilyStore(
            new RecordingSplitFamilyStore(),
            repo.Repository,
            coordinator).WriteAsync(SplitFamily());
        await new RoadmapArtifacts(
            repo.Store,
            repo.Repository,
            workflowCoordinator: coordinator).WriteNumberedEvidenceAsync(
                RoadmapArtifactPaths.BlockerEvidenceDirectory,
                "coordinated-evidence",
                "body");
        await new CoordinatedCompletedEpicArchiveService(
            new StubCompletedEpicArchiveService(),
            coordinator).ArchiveAndSynthesizeAsync(new CompletedEpicArchiveRequest(repo.Repository));

        Assert.Contains(WorkflowPersistenceUnit.DecisionRecordingAndStateUpdate, coordinator.Units);
        Assert.Contains(WorkflowPersistenceUnit.JournalEventEmission, coordinator.Units);
        Assert.Contains(WorkflowPersistenceUnit.ExecutionPreparationProvenanceUpdate, coordinator.Units);
        Assert.Contains(WorkflowPersistenceUnit.SplitLineageChildArtifactsLifecycle, coordinator.Units);
        Assert.Contains(WorkflowPersistenceUnit.LoopHistoryEvidenceWrite, coordinator.Units);
        Assert.Contains(WorkflowPersistenceUnit.CompletedEpicArchive, coordinator.Units);
    }

    [Fact]
    public async Task Retained_artifact_staging_writes_before_deletes_and_can_rollback()
    {
        using var repo = new TempRepo();
        repo.Write(".agents/source.md", "source");
        var staging = new RetainedArtifactStagingArea(repo.Artifacts);

        await staging.StageWriteAsync(".agents/archive/source.md", "archived");
        await staging.StageDeleteIfPresentAsync(".agents/source.md");
        staging.Rollback();
        await staging.CommitAsync();

        Assert.Equal("source", repo.Read(".agents/source.md"));
        Assert.False(await repo.Artifacts.ExistsAsync(".agents/archive/source.md"));

        await staging.StageWriteAsync(".agents/archive/source.md", "archived");
        await staging.StageDeleteIfPresentAsync(".agents/source.md");
        await staging.CommitAsync();

        Assert.Equal("archived", repo.Read(".agents/archive/source.md"));
        Assert.False(await repo.Artifacts.ExistsAsync(".agents/source.md"));
    }

    [Fact]
    public async Task Injected_split_failure_does_not_leave_incomplete_split_family_state()
    {
        using var repo = new TempRepo();
        var splitFamilies = new RecordingSplitFamilyStore { ThrowOnWrite = true };
        var persistence = new SplitLineagePersistence(
            repo.Artifacts,
            new InMemoryArtifactLifecycleStore(),
            splitFamilies,
            new RecordingWorkflowPersistenceCoordinator());

        await Assert.ThrowsAsync<InvalidOperationException>(() => persistence.PersistAsync(
            BundleExtractionResult.Extracted(
            [
                new ExtractedBundleFile(".agents/splits/child-a.md", "# Child A", "hash-a"),
                new ExtractedBundleFile(".agents/splits/child-b.md", "# Child B", "hash-b"),
            ]),
            "SplitEpic",
            ".agents/projections/split-epic.md",
            SplitFamily()));

        Assert.Empty(splitFamilies.Families);
    }

    [Fact]
    public async Task Verification_reports_incomplete_workflow_marker()
    {
        using var repo = new TempRepo();
        await new WorkspaceSqliteStore().InitializeAsync(repo.Repository);
        await InsertStartedMarkerAsync(repo);

        WorkspaceVerificationResult result = await new WorkspaceVerificationService().VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding =>
            finding.Kind == WorkspaceVerificationFindingKind.MutationRequired &&
            finding.Domain == "workflow");
    }

    [Fact]
    public async Task Evidence_write_with_interrupted_state_update_classifies_retryable_partial()
    {
        using var repo = new TempRepo();
        await new WorkspaceSqliteStore().InitializeAsync(repo.Repository);
        await new SqliteExecutionEvidenceStore(repo.Repository).WriteAsync("execution", "evidence body");
        await InsertStartedMarkerAsync(
            repo,
            transactionId: "tx-evidence",
            workflowName: WorkflowPersistenceUnit.LoopHistoryEvidenceWrite.ToString(),
            correlationId: "evidence-state-update");

        WorkspaceVerificationResult result = await new WorkspaceVerificationService().VerifyAsync(
            new RoadmapArtifacts(
                repo.Store,
                repo.Repository,
                new SqliteExecutionEvidenceStore(repo.Repository)));

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding =>
            finding.Kind == WorkspaceVerificationFindingKind.MutationRequired &&
            finding.Rule == "database-integrity" &&
            finding.CurrentState.Contains("IncompatiblePartialState", StringComparison.Ordinal));
    }

    private static async Task InsertStartedMarkerAsync(
        TempRepo repo,
        string transactionId = "tx-started",
        string workflowName = "JournalEventEmission",
        string correlationId = "correlation-3")
    {
        string databasePath = WorkspaceDatabaseLocator.Resolve(repo.Repository);
        await using var connection = WorkspaceSqliteStore.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO workflow_transactions (
                transaction_id, workflow_name, correlation_id, status, started_at, completed_at, marker_json)
            VALUES (
                $transaction_id, $workflow_name, $correlation_id, 'Started',
                '2026-01-01T00:00:00.0000000+00:00', NULL, '{}');
            """;
        command.Parameters.AddWithValue("$transaction_id", transactionId);
        command.Parameters.AddWithValue("$workflow_name", workflowName);
        command.Parameters.AddWithValue("$correlation_id", correlationId);
        await command.ExecuteNonQueryAsync();
    }

    private static DecisionLedgerEntry Decision(string decisionId) =>
        new(
            decisionId,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            RoadmapState.SelectNextStrategicInitiative,
            "SelectNextEpic",
            "SelectNextEpic",
            "projection",
            [],
            [RoadmapArtifactPaths.Selection],
            "Select Existing Epic",
            "High",
            "reason");

    private static TransitionJournalRecord JournalRecord() =>
        new(
            "TransitionCompleted",
            "correlation",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            RoadmapState.CoreReady,
            RoadmapState.SelectNextStrategicInitiative,
            "SelectNextEpic",
            "projection",
            "contract",
            new Dictionary<string, string>(StringComparer.Ordinal),
            [RoadmapArtifactPaths.Selection],
            1,
            "Completed",
            "Select Existing Epic",
            null);

    private static SplitFamily SplitFamily() =>
        new(
            "family-1",
            "proposal",
            [".agents/splits/child-a.md", ".agents/splits/child-b.md"],
            [".agents/splits/child-a.md", ".agents/splits/child-b.md"],
            ".agents/splits/child-a.md",
            "best child",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    private sealed class RecordingWorkflowPersistenceCoordinator : IWorkflowPersistenceCoordinator
    {
        public List<WorkflowPersistenceUnit> Units { get; } = [];

        public async Task<WorkflowPersistenceResult> ExecuteAsync(
            Repository repository,
            WorkflowPersistenceUnit unit,
            string correlationId,
            Func<CancellationToken, Task> persistencePhase,
            CancellationToken cancellationToken = default)
        {
            Units.Add(unit);
            await persistencePhase(cancellationToken);
            return new WorkflowPersistenceResult(correlationId, WorkflowPersistenceMarkerStatus.Completed);
        }

        public Task<IReadOnlyList<WorkflowRecoveryFinding>> ClassifyAsync(
            Repository repository,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkflowRecoveryFinding>>([]);
    }

    private sealed class InMemoryDecisionLedgerStore : IDecisionLedgerStore
    {
        private readonly List<DecisionLedgerEntry> _entries = [];

        public Task<string> AppendAsync(DecisionLedgerEntry entry)
        {
            _entries.Add(entry);
            return Task.FromResult(entry.DecisionId);
        }

        public Task<string> NextDecisionIdAsync() =>
            Task.FromResult($"D{_entries.Count + 1:0000}");

        public Task<string> LastDecisionIdAsync() =>
            Task.FromResult(_entries.LastOrDefault()?.DecisionId ?? "None");
    }

    private sealed class RecordingTransitionJournalStore : ITransitionJournalStore
    {
        public Task AppendAsync(TransitionJournalRecord record) => Task.CompletedTask;
    }

    private sealed class InMemoryExecutionPreparationManifestStore : IExecutionPreparationManifestStore
    {
        private ExecutionPreparationManifest _manifest = ExecutionPreparationManifest.Empty;

        public Task<ExecutionPreparationManifest> LoadAsync() => Task.FromResult(_manifest);

        public Task SaveAsync(ExecutionPreparationManifest manifest)
        {
            _manifest = manifest;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSplitFamilyStore : ISplitFamilyStore
    {
        public bool ThrowOnWrite { get; init; }

        public List<SplitFamily> Families { get; } = [];

        public Task<string> WriteAsync(SplitFamily family)
        {
            if (ThrowOnWrite)
            {
                throw new InvalidOperationException("split failure");
            }

            Families.Add(family);
            return Task.FromResult(RoadmapArtifactPaths.SplitFamilyJson(family.FamilyId));
        }

        public Task<bool> ExistsForChildAsync(string childEpicPath) =>
            Task.FromResult(Families.Any(family => family.ChildEpicPaths.Contains(childEpicPath, StringComparer.Ordinal)));

        public Task<int> CountAsync() => Task.FromResult(Families.Count);
    }

    private sealed class InMemoryArtifactLifecycleStore : IArtifactLifecycleStore
    {
        private readonly List<ArtifactLifecycleEntry> _entries = [];

        public Task<IReadOnlyList<ArtifactLifecycleEntry>> LoadAsync() =>
            Task.FromResult<IReadOnlyList<ArtifactLifecycleEntry>>(_entries);

        public Task UpsertAsync(string path, ArtifactLifecycleState state, string notes = "")
        {
            _entries.RemoveAll(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
            _entries.Add(new ArtifactLifecycleEntry(path, state, DateTimeOffset.UtcNow, notes));
            return Task.CompletedTask;
        }

        public Task SaveAsync(IReadOnlyList<ArtifactLifecycleEntry> entries)
        {
            _entries.Clear();
            _entries.AddRange(entries);
            return Task.CompletedTask;
        }
    }

    private sealed class StubCompletedEpicArchiveService : ICompletedEpicArchiveService
    {
        public Task<CompletedEpicArchiveResult> ArchiveAndSynthesizeAsync(
            CompletedEpicArchiveRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CompletedEpicArchiveResult(
                1,
                $"{request.ArchiveRoot}/1",
                $"{request.ArchiveRoot}/1.md",
                "synthesis"));
    }
}
