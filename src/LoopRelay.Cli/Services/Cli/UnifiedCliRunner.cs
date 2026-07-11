using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Primitives.Diagnostics;
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
    private static readonly JsonSerializerOptions ProvenanceJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<int> RunAsync(
        UnifiedCliInvocation invocation,
        CancellationToken cancellationToken)
    {
        int? migrationExitCode = await MigrateExistingWorkspaceDatabaseAsync(cancellationToken);
        if (migrationExitCode is not null)
        {
            return migrationExitCode.Value;
        }

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
            observation.StorageVerification.IsUnusable)
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

            _error.WriteLine($"{invocation.Command.Kind} is parsed by the unified CLI but is not wired to an implementation yet.");
            return 2;
        }

        // M7: runtime prerequisites are inspected before any agent launches — an Error aborts
        // with the typed MissingRuntimePrerequisite outcome instead of the raw resolver
        // exception the first send would otherwise throw. Only the production composition
        // inspects: injected runtimes have no provider prerequisites.
        IReadOnlyList<RuntimeDiagnostic> runtimeDiagnostics =
            await _composition.InspectRuntimePrerequisitesAsync(cancellationToken);
        foreach (RuntimeDiagnostic diagnostic in runtimeDiagnostics)
        {
            TextWriter target = diagnostic.Severity == RuntimeDiagnosticSeverity.Error ? _error : _output;
            target.WriteLine($"Runtime prerequisite [{diagnostic.Severity}] {diagnostic.Id}: {diagnostic.Message}");
        }

        if (runtimeDiagnostics.Any(diagnostic => diagnostic.Severity == RuntimeDiagnosticSeverity.Error))
        {
            _output.WriteLine($"Stop reason: {WorkflowStopReason.MissingRuntimePrerequisite}");
            return ExitCodeFor(WorkflowStopReason.MissingRuntimePrerequisite);
        }

        return await RunWorkflowAsync(invocation, observation, cancellationToken);
    }

    // Every command migrates an existing workspace database in place before the first observation so
    // legacy durable labels (for example 'Blocked') are rewritten before any read-only snapshot parse
    // can observe them. A missing database file is left absent; fresh workspaces keep current behavior.
    private async Task<int?> MigrateExistingWorkspaceDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            string databasePath = LoopRelayWorkspaceDatabase.Resolve(_composition.Repository);
            if (!File.Exists(databasePath))
            {
                return null;
            }

            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
            await connection.OpenAsync(cancellationToken);
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
            return null;
        }
        catch (InvalidOperationException exception)
        {
            _error.WriteLine(exception.Message);
            return 4;
        }
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
            WorkflowStopReason.MissingRequiredInput => 4,
            WorkflowStopReason.DirtyInputSurface => 4,
            WorkflowStopReason.UnversionedInputSurface => 4,
            WorkflowStopReason.StorageUnusable => 4,
            WorkflowStopReason.MissingRuntimePrerequisite => 4,
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
        IReadOnlyList<ConsumedInputDrift> inputDrift = observation.StorageAuthority.UsableAuthority
            ? await ReadReceiptStaleness.ProjectAsync(invocation.Repository, cancellationToken)
            : [];
        _output.WriteLine(UnifiedCliStatusFormatter.Format(invocation, observation, resolution, inputDrift));
        return 0;
    }

    private async Task<int> RunStorageVerifyAsync(CancellationToken cancellationToken)
    {
        RepositoryObservation observation = await _composition.ObserveAsync(cancellationToken);
        PrintStorageVerification(observation);
        return observation.StorageVerification.IsUnusable ? 4 : 0;
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
                    _output.WriteLine("Storage export stopped because the workspace database is missing.");
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
            : _composition.Policy.MaxUnboundedContinuationSteps;

        RunIdentity run = RunIdentity.New();
        DateTimeOffset runStartedAt = DateTimeOffset.UtcNow;
        string chainIdentity = _composition.SelectChain(invocation.WorkflowInvocation, observation).Identity;
        string invocationMode = invocation.WorkflowInvocation.Mode.ToString();
        string workspaceId = string.Empty;
        try
        {
            workspaceId = await _composition.Persistence.ReadWorkspaceIdentityAsync(cancellationToken);
        }
        catch
        {
            // Run bookkeeping is best-effort supporting evidence; the CLI never fails because of it.
        }

        try
        {
            await _composition.Persistence.InterruptLingeringActiveRunsAsync(run.Value, cancellationToken);
        }
        catch
        {
        }

        try
        {
            await _composition.Persistence.UpsertRunAsync(
                new RunRecord(
                    run.Value,
                    workspaceId,
                    chainIdentity,
                    invocationMode,
                    "Active",
                    runStartedAt,
                    null,
                    null,
                    string.Empty),
                cancellationToken);
        }
        catch
        {
        }

        try
        {
            // The policy-resolution fact backs every attempt's policy_id for this run: the full
            // resolved values (the canonical JSON the identity hash covers) plus per-field
            // provenance. Same best-effort posture as the run record it accompanies.
            await _composition.Persistence.AppendPolicyResolutionAsync(
                new CanonicalPolicyResolutionRecord(
                    CausalUlid.NewId("res"),
                    _composition.Policy.PolicyId,
                    _composition.Policy.SchemaVersion,
                    _composition.Policy.ResolvedJson,
                    JsonSerializer.Serialize(_composition.Policy.Provenance, ProvenanceJsonOptions),
                    _composition.Policy.SourceDescription,
                    runStartedAt),
                cancellationToken);
        }
        catch
        {
        }

        WorkflowStopReason? lastStopReason = null;
        try
        {
            for (int step = 0; step < guard; step++)
            {
                WorkflowChainDefinition chain = _composition.SelectChain(invocation.WorkflowInvocation, observation);
                WorkflowChainRunResult result = await _composition.WorkflowChainRunner.RunAsync(
                    new WorkflowChainRunRequest(
                        invocation.WorkflowInvocation,
                        observation,
                        chain,
                        _composition.WorkflowDefinitions,
                        run),
                    cancellationToken);
                lastStopReason = result.StopReason;
                PrintRunResult(result);

                if (invocation.WorkflowInvocation.IsBounded ||
                    result.StopReason != WorkflowStopReason.TransitionCompleted)
                {
                    return ExitCodeFor(result.StopReason);
                }

                observation = await _composition.ObserveAsync(cancellationToken);
                if (invocation.Command.RequiresStorageVerification &&
                    observation.StorageVerification.IsUnusable)
                {
                    lastStopReason = WorkflowStopReason.StorageUnusable;
                    PrintStorageVerification(observation);
                    return 4;
                }
            }

            lastStopReason = WorkflowStopReason.Stalled;
            _output.WriteLine($"Stop reason: {WorkflowStopReason.Stalled}");
            _output.WriteLine($"Explanation: Unbounded workflow continuation guard exhausted after {guard} completed transitions.");
            return ExitCodeFor(WorkflowStopReason.Stalled);
        }
        catch (OperationCanceledException)
        {
            lastStopReason = WorkflowStopReason.Cancelled;
            throw;
        }
        catch
        {
            lastStopReason = WorkflowStopReason.Failed;
            throw;
        }
        finally
        {
            WorkflowStopReason terminal = lastStopReason ?? WorkflowStopReason.Failed;
            try
            {
                await _composition.Persistence.UpsertRunAsync(
                    new RunRecord(
                        run.Value,
                        workspaceId,
                        chainIdentity,
                        invocationMode,
                        terminal.ToString(),
                        runStartedAt,
                        DateTimeOffset.UtcNow,
                        terminal.ToString(),
                        string.Empty),
                    CancellationToken.None);
            }
            catch
            {
                // Run finalization is best-effort: a failed spine write must not mask the run outcome.
            }
        }
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
            PrintGateWarnings(transition.InputGate);
            PrintGateWarnings(transition.OutputGate);
        }
    }

    private void PrintGateWarnings(GateResult? gate)
    {
        if (gate is null || gate.IsSatisfied)
        {
            return;
        }

        foreach (GateRequirementResult requirement in gate.Requirements)
        {
            if (requirement.Status != GateStatus.Satisfied)
            {
                _output.WriteLine($"Warning: {requirement.RequirementIdentity}: {CollapseToSingleLine(requirement.Explanation)}");
            }
        }
    }

    // Warning lines are line-oriented output; explanation text that embeds process output (for example
    // multi-line git stderr) must not be able to break one warning across several lines.
    private static string CollapseToSingleLine(string text) =>
        string.Join(
            " ",
            text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

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
        _output.WriteLine($"Warnings: {FormatList(verification.BlockingConditions.Select(warning => warning.Concern).ToArray())}");
    }

    private static string FormatList(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(none)" : string.Join(", ", values);
}
