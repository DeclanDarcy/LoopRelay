using System.Globalization;
using System.Text.Json;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Archive;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Models.Splits;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Roadmap.Cli.Services.Persistence;

internal enum WorkflowPersistenceUnit
{
    RoadmapTransitionSave,
    DecisionRecordingAndStateUpdate,
    SplitLineageChildArtifactsLifecycle,
    ExecutionPreparationProvenanceUpdate,
    JournalEventEmission,
    LoopHistoryEvidenceWrite,
    CompletedEpicArchive,
}

internal enum WorkflowPersistenceMarkerStatus
{
    Started,
    Completed,
    Failed,
}

internal enum WorkflowRecoveryClassification
{
    Valid,
    RetryablePartial,
    Corrupt,
    Unsupported,
    Conflict,
}

internal sealed record WorkflowPersistenceResult(
    string TransactionId,
    WorkflowPersistenceMarkerStatus Status);

internal sealed record WorkflowRecoveryFinding(
    WorkflowRecoveryClassification Classification,
    string TransactionId,
    string WorkflowName,
    string CorrelationId,
    string Reason);

internal interface IWorkflowPersistenceCoordinator
{
    Task<WorkflowPersistenceResult> ExecuteAsync(
        Repository repository,
        WorkflowPersistenceUnit unit,
        string correlationId,
        Func<CancellationToken, Task> persistencePhase,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowRecoveryFinding>> ClassifyAsync(
        Repository repository,
        CancellationToken cancellationToken = default);
}

internal sealed class NullWorkflowPersistenceCoordinator : IWorkflowPersistenceCoordinator
{
    public static NullWorkflowPersistenceCoordinator Instance { get; } = new();

    private NullWorkflowPersistenceCoordinator()
    {
    }

    public async Task<WorkflowPersistenceResult> ExecuteAsync(
        Repository repository,
        WorkflowPersistenceUnit unit,
        string correlationId,
        Func<CancellationToken, Task> persistencePhase,
        CancellationToken cancellationToken = default)
    {
        await persistencePhase(cancellationToken);
        return new WorkflowPersistenceResult(string.Empty, WorkflowPersistenceMarkerStatus.Completed);
    }

    public Task<IReadOnlyList<WorkflowRecoveryFinding>> ClassifyAsync(
        Repository repository,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkflowRecoveryFinding>>([]);
}

internal sealed class WorkflowPersistenceCoordinator : IWorkflowPersistenceCoordinator
{
    public async Task<WorkflowPersistenceResult> ExecuteAsync(
        Repository repository,
        WorkflowPersistenceUnit unit,
        string correlationId,
        Func<CancellationToken, Task> persistencePhase,
        CancellationToken cancellationToken = default)
    {
        string transactionId = Guid.NewGuid().ToString("N");
        await WriteMarkerAsync(
            repository,
            transactionId,
            unit,
            correlationId,
            WorkflowPersistenceMarkerStatus.Started,
            completedAt: null,
            marker: new Dictionary<string, string>
            {
                ["unit"] = unit.ToString(),
                ["phase"] = "started",
            },
            cancellationToken);

        try
        {
            await persistencePhase(cancellationToken);
            await WriteMarkerAsync(
                repository,
                transactionId,
                unit,
                correlationId,
                WorkflowPersistenceMarkerStatus.Completed,
                DateTimeOffset.UtcNow,
                new Dictionary<string, string>
                {
                    ["unit"] = unit.ToString(),
                    ["phase"] = "completed",
                },
                cancellationToken);
            return new WorkflowPersistenceResult(transactionId, WorkflowPersistenceMarkerStatus.Completed);
        }
        catch
        {
            await WriteMarkerAsync(
                repository,
                transactionId,
                unit,
                correlationId,
                WorkflowPersistenceMarkerStatus.Failed,
                DateTimeOffset.UtcNow,
                new Dictionary<string, string>
                {
                    ["unit"] = unit.ToString(),
                    ["phase"] = "failed",
                },
                CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<WorkflowRecoveryFinding>> ClassifyAsync(
        Repository repository,
        CancellationToken cancellationToken = default)
    {
        string databasePath = WorkspaceDatabaseLocator.Resolve(repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        var findings = new List<WorkflowRecoveryFinding>();
        await using SqliteConnection connection = WorkspaceSqliteStore.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT transaction_id, workflow_name, correlation_id, status, completed_at
            FROM workflow_transactions
            ORDER BY started_at, transaction_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string transactionId = reader.GetString(0);
            string workflowName = reader.GetString(1);
            string correlationId = reader.GetString(2);
            string status = reader.GetString(3);
            bool hasCompletion = !reader.IsDBNull(4);

            if (string.Equals(status, WorkflowPersistenceMarkerStatus.Started.ToString(), StringComparison.Ordinal))
            {
                findings.Add(new WorkflowRecoveryFinding(
                    WorkflowRecoveryClassification.RetryablePartial,
                    transactionId,
                    workflowName,
                    correlationId,
                    "Workflow transaction marker was started but never completed."));
            }
            else if (string.Equals(status, WorkflowPersistenceMarkerStatus.Failed.ToString(), StringComparison.Ordinal))
            {
                findings.Add(new WorkflowRecoveryFinding(
                    WorkflowRecoveryClassification.Corrupt,
                    transactionId,
                    workflowName,
                    correlationId,
                    "Workflow transaction marker recorded a failed persistence phase."));
            }
            else if (string.Equals(status, WorkflowPersistenceMarkerStatus.Completed.ToString(), StringComparison.Ordinal) && !hasCompletion)
            {
                findings.Add(new WorkflowRecoveryFinding(
                    WorkflowRecoveryClassification.Corrupt,
                    transactionId,
                    workflowName,
                    correlationId,
                    "Workflow transaction marker completed without a completion timestamp."));
            }
        }

        return findings;
    }

    private static async Task WriteMarkerAsync(
        Repository repository,
        string transactionId,
        WorkflowPersistenceUnit unit,
        string correlationId,
        WorkflowPersistenceMarkerStatus status,
        DateTimeOffset? completedAt,
        IReadOnlyDictionary<string, string> marker,
        CancellationToken cancellationToken)
    {
        string databasePath = WorkspaceDatabaseLocator.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using SqliteConnection connection = WorkspaceSqliteStore.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await WorkspaceSqliteStore.EnsureSchemaAsync(connection, cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO workflow_transactions (
                transaction_id, workflow_name, correlation_id, status, started_at, completed_at, marker_json)
            VALUES (
                $transaction_id, $workflow_name, $correlation_id, $status, $started_at, $completed_at, $marker_json)
            ON CONFLICT(transaction_id) DO UPDATE SET
                status = excluded.status,
                completed_at = excluded.completed_at,
                marker_json = excluded.marker_json;
            """;
        command.Parameters.AddWithValue("$transaction_id", transactionId);
        command.Parameters.AddWithValue("$workflow_name", unit.ToString());
        command.Parameters.AddWithValue("$correlation_id", correlationId);
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$started_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$completed_at", completedAt?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$marker_json", JsonSerializer.Serialize(marker));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

internal sealed class RetainedArtifactStagingArea(RoadmapArtifacts _artifacts)
{
    private readonly List<StagedWrite> _writes = [];
    private readonly List<string> _deletes = [];

    public async Task StageWriteAsync(string targetPath, string content)
    {
        if (await _artifacts.ExistsAsync(targetPath))
        {
            throw new InvalidOperationException($"Staged artifact target already exists: {targetPath}");
        }

        _writes.Add(new StagedWrite(targetPath, content));
    }

    public async Task StageDeleteIfPresentAsync(string sourcePath)
    {
        if (await _artifacts.ExistsAsync(sourcePath))
        {
            _deletes.Add(sourcePath);
        }
    }

    public async Task CommitAsync()
    {
        foreach (StagedWrite write in _writes)
        {
            await _artifacts.WriteAsync(write.Path, write.Content);
        }

        foreach (string path in _deletes)
        {
            await _artifacts.DeleteAsync(path);
        }
    }

    public void Rollback()
    {
        _writes.Clear();
        _deletes.Clear();
    }

    private sealed record StagedWrite(string Path, string Content);
}

internal sealed class CoordinatedDecisionLedgerStore(
    IDecisionLedgerStore inner,
    Repository repository,
    IWorkflowPersistenceCoordinator coordinator) : IDecisionLedgerStore
{
    public async Task<string> AppendAsync(DecisionLedgerEntry entry)
    {
        string? id = null;
        await coordinator.ExecuteAsync(
            repository,
            WorkflowPersistenceUnit.DecisionRecordingAndStateUpdate,
            entry.DecisionId,
            async _ => id = await inner.AppendAsync(entry));
        return id ?? entry.DecisionId;
    }

    public Task<string> NextDecisionIdAsync() => inner.NextDecisionIdAsync();

    public Task<string> LastDecisionIdAsync() => inner.LastDecisionIdAsync();
}

internal sealed class CoordinatedSplitFamilyStore(
    ISplitFamilyStore inner,
    Repository repository,
    IWorkflowPersistenceCoordinator coordinator) : ISplitFamilyStore
{
    public async Task<string> WriteAsync(SplitFamily family)
    {
        string? path = null;
        await coordinator.ExecuteAsync(
            repository,
            WorkflowPersistenceUnit.SplitLineageChildArtifactsLifecycle,
            family.FamilyId,
            async _ => path = await inner.WriteAsync(family));
        return path ?? RoadmapArtifactPaths.SplitFamilyJson(family.FamilyId);
    }

    public Task<bool> ExistsForChildAsync(string childEpicPath) => inner.ExistsForChildAsync(childEpicPath);

    public Task<int> CountAsync() => inner.CountAsync();
}

internal sealed class CoordinatedExecutionPreparationManifestStore(
    IExecutionPreparationManifestStore inner,
    Repository repository,
    IWorkflowPersistenceCoordinator coordinator) : IExecutionPreparationManifestStore
{
    public Task<ExecutionPreparationManifest> LoadAsync() => inner.LoadAsync();

    public Task SaveAsync(ExecutionPreparationManifest manifest) =>
        coordinator.ExecuteAsync(
            repository,
            WorkflowPersistenceUnit.ExecutionPreparationProvenanceUpdate,
            RoadmapArtifactPaths.ExecutionPreparationManifest,
            token => inner.SaveAsync(manifest),
            CancellationToken.None);
}

internal sealed class CoordinatedTransitionJournalStore(
    ITransitionJournalStore inner,
    Repository repository,
    IWorkflowPersistenceCoordinator coordinator) : ITransitionJournalStore
{
    public Task AppendAsync(TransitionJournalRecord record) =>
        coordinator.ExecuteAsync(
            repository,
            WorkflowPersistenceUnit.JournalEventEmission,
            record.CorrelationId,
            token => inner.AppendAsync(record),
            CancellationToken.None);
}

internal sealed class CoordinatedCompletedEpicArchiveService(
    ICompletedEpicArchiveService inner,
    IWorkflowPersistenceCoordinator coordinator) : ICompletedEpicArchiveService
{
    public async Task<CompletedEpicArchiveResult> ArchiveAndSynthesizeAsync(
        CompletedEpicArchiveRequest request,
        CancellationToken cancellationToken = default)
    {
        CompletedEpicArchiveResult? result = null;
        await coordinator.ExecuteAsync(
            request.Repository,
            WorkflowPersistenceUnit.CompletedEpicArchive,
            request.ArchiveRoot,
            async token => result = await inner.ArchiveAndSynthesizeAsync(request, token),
            cancellationToken);
        return result ?? throw new InvalidOperationException("Completed epic archive workflow did not return a result.");
    }
}
