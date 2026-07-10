using System.Globalization;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Cli.Services.Cli;

internal sealed class UnifiedCliRunner(
    UnifiedCliComposition _composition,
    TextWriter _output,
    TextWriter _error)
{
    private const int MaxUnboundedContinuationSteps = 32;

    public async Task<int> RunAsync(
        UnifiedCliInvocation invocation,
        CancellationToken cancellationToken)
    {
        if (invocation.Command.Kind == UnifiedCliCommandKind.Status)
        {
            return await RunStatusAsync(invocation, cancellationToken);
        }

        if (invocation.Command.Kind == UnifiedCliCommandKind.StorageVerify)
        {
            return await RunStorageVerifyAsync(cancellationToken);
        }

        RepositoryObservation observation = await _composition.ObserveAsync(cancellationToken);
        if (invocation.Command.RequiresStorageVerification &&
            observation.StorageVerification.IsBlocked)
        {
            PrintStorageVerification(observation);
            return 4;
        }

        if (invocation.Command.Kind != UnifiedCliCommandKind.Run)
        {
            if (invocation.Command.Kind == UnifiedCliCommandKind.StorageInit)
            {
                return await RunStorageInitAsync(cancellationToken);
            }

            if (invocation.Command.Kind is UnifiedCliCommandKind.StorageImport
                or UnifiedCliCommandKind.StorageExport
                or UnifiedCliCommandKind.StorageSync)
            {
                return await RunStorageSyncAsync(invocation, cancellationToken);
            }

            if (invocation.Command.Kind == UnifiedCliCommandKind.Unblock)
            {
                return await RunUnblockAsync(invocation, cancellationToken);
            }

            _error.WriteLine($"{invocation.Command.Kind} is parsed by the unified CLI but is not wired to an implementation yet.");
            return 2;
        }

        return await RunWorkflowAsync(invocation, observation, cancellationToken);
    }

    public static int ExitCodeFor(WorkflowStopReason stopReason) =>
        stopReason switch
        {
            WorkflowStopReason.ChainCompleted => 0,
            WorkflowStopReason.BoundedWorkflowCompleted => 0,
            WorkflowStopReason.Waiting => 0,
            WorkflowStopReason.TransitionCompleted => 0,
            WorkflowStopReason.Failed => 1,
            WorkflowStopReason.Stalled => 3,
            WorkflowStopReason.Blocked => 4,
            WorkflowStopReason.Ambiguous => 4,
            WorkflowStopReason.NoEligibleTransition => 4,
            WorkflowStopReason.Cancelled => 130,
            _ => 1,
        };

    private async Task<int> RunStatusAsync(
        UnifiedCliInvocation invocation,
        CancellationToken cancellationToken)
    {
        RepositoryObservation observation = await _composition.ObserveAsync(cancellationToken);
        WorkflowResolutionResult resolution = _composition.Resolve(invocation.WorkflowInvocation, observation);
        _output.WriteLine(UnifiedCliStatusFormatter.Format(invocation, observation, resolution));
        return 0;
    }

    private async Task<int> RunStorageVerifyAsync(CancellationToken cancellationToken)
    {
        RepositoryObservation observation = await _composition.ObserveAsync(cancellationToken);
        PrintStorageVerification(observation);
        return observation.StorageVerification.IsBlocked ? 4 : 0;
    }

    private async Task<int> RunStorageInitAsync(CancellationToken cancellationToken)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_composition.Repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        _output.WriteLine("Storage initialized.");
        _output.WriteLine($"Database: {databasePath}");
        return 0;
    }

    private async Task<int> RunStorageSyncAsync(
        UnifiedCliInvocation invocation,
        CancellationToken cancellationToken)
    {
        if (invocation.Command.Arguments.Count > 0)
        {
            _error.WriteLine($"Unexpected argument: {invocation.Command.Arguments[0]}");
            return 2;
        }

        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_composition.Repository);
        switch (invocation.Command.Kind)
        {
            case UnifiedCliCommandKind.StorageImport:
                await EnsureWorkspaceDatabaseAsync(databasePath, "imported", cancellationToken);
                _output.WriteLine("Storage import completed.");
                _output.WriteLine("Filesystem exports remain repository-owned observation inputs.");
                _output.WriteLine($"Database: {databasePath}");
                return 0;
            case UnifiedCliCommandKind.StorageExport:
                if (!File.Exists(databasePath))
                {
                    _output.WriteLine("Storage export blocked because the workspace database is missing.");
                    _output.WriteLine($"Database: {databasePath}");
                    return 4;
                }

                await EnsureWorkspaceDatabaseAsync(databasePath, null, cancellationToken);
                _output.WriteLine("Storage export completed with no filesystem mutations.");
                _output.WriteLine("Repository observation already reads filesystem exports directly.");
                _output.WriteLine($"Database: {databasePath}");
                return 0;
            case UnifiedCliCommandKind.StorageSync:
                await EnsureWorkspaceDatabaseAsync(
                    databasePath,
                    File.Exists(databasePath) ? null : "imported",
                    cancellationToken);
                _output.WriteLine("Storage sync completed.");
                _output.WriteLine("Shared workspace schema is usable for canonical orchestration.");
                _output.WriteLine($"Database: {databasePath}");
                return 0;
            default:
                throw new InvalidOperationException("Unsupported storage sync command.");
        }
    }

    private async Task<int> RunUnblockAsync(
        UnifiedCliInvocation invocation,
        CancellationToken cancellationToken)
    {
        if (invocation.Command.Arguments.Count > 0)
        {
            _error.WriteLine($"Unexpected argument: {invocation.Command.Arguments[0]}");
            return 2;
        }

        var store = new CanonicalWorkflowPersistenceStore(_composition.Repository);
        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync(cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string unblockEvidence = $"unified-cli/unblock/{now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}";
        CanonicalBlockerRecord[] recoverableBlockers = snapshot.Blockers
            .Where(blocker => blocker.ResolvedAt is null && blocker.Blocker.Recoverable)
            .ToArray();
        CanonicalBlockerRecord[] nonRecoverableBlockers = snapshot.Blockers
            .Where(blocker => blocker.ResolvedAt is null && !blocker.Blocker.Recoverable)
            .ToArray();

        foreach (CanonicalBlockerRecord blocker in recoverableBlockers)
        {
            await store.UpsertBlockerAsync(blocker with { ResolvedAt = now }, cancellationToken);
        }

        CanonicalWorkflowStateRecord[] restorableWorkflows = snapshot.WorkflowStates
            .Where(state => state.State == WorkflowResolutionState.Blocked &&
                state.CurrentStage is not null &&
                nonRecoverableBlockers.All(blocker => blocker.Workflow != state.Workflow))
            .ToArray();
        foreach (CanonicalWorkflowStateRecord workflow in restorableWorkflows)
        {
            IReadOnlyList<string> evidence = workflow.Evidence
                .Concat([unblockEvidence])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            await store.UpsertWorkflowStateAsync(
                workflow with
                {
                    State = WorkflowResolutionState.Resumable,
                    Outcome = RuntimeOutcomeKind.Waiting,
                    UpdatedAt = now,
                    Evidence = evidence,
                },
                cancellationToken);
            await store.UpsertStageStateAsync(
                new CanonicalStageStateRecord(
                    workflow.Workflow,
                    workflow.CurrentStage!.Value,
                    WorkflowResolutionState.Active,
                    now,
                    evidence),
                cancellationToken);
        }

        CanonicalWorkflowStateRecord[] stageLessBlockedWorkflows = snapshot.WorkflowStates
            .Where(state => state.State == WorkflowResolutionState.Blocked && state.CurrentStage is null)
            .ToArray();
        if (recoverableBlockers.Length == 0 &&
            restorableWorkflows.Length == 0 &&
            nonRecoverableBlockers.Length == 0 &&
            stageLessBlockedWorkflows.Length == 0)
        {
            _output.WriteLine("No canonical blockers or blocked workflow states were active.");
            return 0;
        }

        _output.WriteLine($"Resolved canonical blockers: {recoverableBlockers.Length}");
        _output.WriteLine($"Restored blocked workflows: {restorableWorkflows.Length}");
        if (nonRecoverableBlockers.Length > 0)
        {
            _output.WriteLine($"Non-recoverable blockers remain: {nonRecoverableBlockers.Length}");
        }

        if (stageLessBlockedWorkflows.Length > 0)
        {
            _output.WriteLine($"Blocked workflows without a resumable stage remain: {stageLessBlockedWorkflows.Length}");
        }

        return nonRecoverableBlockers.Length == 0 && stageLessBlockedWorkflows.Length == 0 ? 0 : 4;
    }

    private static async Task EnsureWorkspaceDatabaseAsync(
        string databasePath,
        string? persistenceState,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        if (!string.IsNullOrWhiteSpace(persistenceState))
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO workspace_metadata (key, value)
                VALUES ('persistence_state', $persistence_state)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            command.Parameters.AddWithValue("$persistence_state", persistenceState);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<int> RunWorkflowAsync(
        UnifiedCliInvocation invocation,
        RepositoryObservation observation,
        CancellationToken cancellationToken)
    {
        int guard = invocation.WorkflowInvocation.IsBounded
            ? 1
            : MaxUnboundedContinuationSteps;

        for (int step = 0; step < guard; step++)
        {
            WorkflowChainDefinition chain = _composition.SelectChain(invocation.WorkflowInvocation, observation);
            WorkflowChainRunResult result = await _composition.WorkflowChainRunner.RunAsync(
                new WorkflowChainRunRequest(
                    invocation.WorkflowInvocation,
                    observation,
                    chain,
                    _composition.WorkflowDefinitions),
                cancellationToken);
            PrintRunResult(result);

            if (invocation.WorkflowInvocation.IsBounded ||
                result.StopReason != WorkflowStopReason.TransitionCompleted)
            {
                return ExitCodeFor(result.StopReason);
            }

            observation = await _composition.ObserveAsync(cancellationToken);
            if (invocation.Command.RequiresStorageVerification &&
                observation.StorageVerification.IsBlocked)
            {
                PrintStorageVerification(observation);
                return 4;
            }
        }

        _output.WriteLine($"Stop reason: {WorkflowStopReason.Stalled}");
        _output.WriteLine($"Explanation: Unbounded workflow continuation guard exhausted after {guard} completed transitions.");
        return ExitCodeFor(WorkflowStopReason.Stalled);
    }

    private void PrintRunResult(WorkflowChainRunResult result)
    {
        _output.WriteLine($"Workflow: {result.LastWorkflow}");
        _output.WriteLine($"Stop reason: {result.StopReason}");
        _output.WriteLine($"Explanation: {result.Explanation}");
        if (result.ControllerResult?.Transition is { } transition)
        {
            _output.WriteLine($"Transition: {transition.Transition}");
            _output.WriteLine($"Outcome: {transition.Outcome}");
            _output.WriteLine($"Durable state: {transition.DurableState}");
        }
    }

    private void PrintStorageVerification(RepositoryObservation observation)
    {
        StorageVerificationResult verification = observation.StorageVerification;
        _output.WriteLine($"Storage authority: {verification.Authority}");
        _output.WriteLine($"Usable authority: {verification.UsableAuthority}");
        _output.WriteLine($"Evidence: {FormatList(verification.Evidence)}");
        _output.WriteLine($"Stale exports: {FormatList(verification.StaleExports)}");
        _output.WriteLine($"Conflicts: {FormatList(verification.Conflicts)}");
        _output.WriteLine($"Corruption: {FormatList(verification.Corruption)}");
        _output.WriteLine($"Unsupported schema: {FormatList(verification.UnsupportedSchema)}");
        _output.WriteLine($"Unresolved references: {FormatList(verification.UnresolvedReferences)}");
        _output.WriteLine($"Partial transactions: {FormatList(verification.PartialTransactions)}");
        _output.WriteLine($"Blocking conditions: {FormatList(verification.BlockingConditions.Select(blocker => blocker.Reason).ToArray())}");
    }

    private static string FormatList(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(none)" : string.Join(", ", values);
}
