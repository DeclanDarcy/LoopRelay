using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Tests.Persistence;

public sealed class CanonicalWorkflowPersistenceStoreTests
{
    [Fact]
    public async Task Ensure_schema_creates_canonical_tables_at_current_version()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Equal(LoopRelayWorkspaceDatabase.CurrentSchemaVersion.ToString(), await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        foreach (string table in CanonicalTables)
        {
            Assert.True(await TableExistsAsync(connection, table), $"Expected table {table}.");
        }
    }

    [Fact]
    public async Task Ensure_schema_migrates_v1_database_to_current_version()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE schema_metadata(key text primary key, value text not null);
            CREATE TABLE workspace_metadata(key text primary key, value text not null);
            INSERT INTO schema_metadata(key, value) VALUES ('schema_version', '1');
            INSERT INTO workspace_metadata(key, value) VALUES ('persistence_state', 'imported');
            """);

        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Equal(LoopRelayWorkspaceDatabase.CurrentSchemaVersion.ToString(), await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        Assert.Equal("imported", await ScalarStringAsync(connection, "SELECT value FROM workspace_metadata WHERE key = 'persistence_state';"));
        foreach (string table in CanonicalTables)
        {
            Assert.True(await TableExistsAsync(connection, table), $"Expected table {table}.");
        }
    }

    [Fact]
    public async Task Ensure_schema_rejects_future_schema_without_repairing_it()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE schema_metadata(key text primary key, value text not null);
            INSERT INTO schema_metadata(key, value) VALUES ('schema_version', '999');
            """);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection));

        Assert.Contains("Unsupported SQLite schema version", exception.Message, StringComparison.Ordinal);
        Assert.Equal("999", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
    }

    [Fact]
    public async Task Store_round_trips_every_canonical_workflow_row_family()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
        var workflow = WorkflowIdentity.Plan;
        var stage = new WorkflowStageIdentity("Planning");
        var transition = new WorkflowTransitionIdentity("CreateExecutablePlan");
        var runId = "run-001";

        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            workflow,
            WorkflowResolutionState.Active,
            stage,
            RuntimeOutcomeKind.Waiting,
            now,
            ["workflow-state.md"]));
        await store.UpsertStageStateAsync(new CanonicalStageStateRecord(
            workflow,
            stage,
            WorkflowResolutionState.Active,
            now,
            ["stage-state.md"]));
        await store.UpsertTransitionRunAsync(new CanonicalTransitionRunRecord(
            runId,
            workflow,
            stage,
            transition,
            TransitionDurableState.OutputValidated,
            RuntimeOutcomeKind.Waiting,
            now,
            now.AddMinutes(1),
            "input-hash",
            "transition waiting",
            ["transition.md"]));
        await store.AppendTransitionEvidenceAsync(new CanonicalTransitionEvidenceRecord(
            0,
            runId,
            transition,
            "OutputValidated",
            now.AddSeconds(10),
            TransitionDurableState.OutputValidated,
            "output validated",
            ["output.md"],
            """{"kind":"output"}"""));
        await store.UpsertProductAsync(new ProductRecord(
            ProductIdentity.ExecutablePlan,
            workflow,
            transition,
            [WorkflowIdentity.Execute],
            "repository-owned",
            "canonical",
            [".agents/plan.md"],
            "causal-hash",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            ["product.md"]));
        await store.AppendGateEvaluationAsync(new CanonicalGateEvaluationRecord(
            0,
            workflow,
            stage,
            transition,
            new GateIdentity("CreateExecutablePlan.Output"),
            GateStatus.Satisfied,
            now.AddSeconds(20),
            [new GateRequirementResult("plan.exists", GateStatus.Satisfied, "plan exists", [".agents/plan.md"])],
            "gate satisfied",
            ["gate.md"]));
        await store.AppendEffectRecordAsync(new CanonicalEffectRecord(
            0,
            runId,
            new EffectIdentity("persist-plan"),
            EffectCategory.ProductPersistence,
            EffectExecutionStatus.Succeeded,
            now.AddSeconds(30),
            "effect applied",
            ["effect.md"]));
        await store.UpsertBlockerAsync(new CanonicalBlockerRecord(
            "blocker-001",
            workflow,
            stage,
            transition,
            new ResolutionBlocker(
                BlockerCategory.Human,
                "review required",
                "workflow resolver",
                "approve review",
                Recoverable: true,
                ["blocker.md"]),
            now.AddSeconds(40),
            null));
        await store.UpsertRecoveryMarkerAsync(new CanonicalRecoveryMarkerRecord(
            "recovery-001",
            workflow,
            stage,
            transition,
            new RecoveryDefinition("PlanRecovery", "resume from output validation", ["resume"], ["silent repair"]),
            ["recovery.md"],
            now.AddSeconds(50)));
        await store.UpsertWorkflowChainRunAsync(new CanonicalWorkflowChainRunRecord(
            "chain-001",
            "TraditionalRoadmapChain",
            workflow,
            RuntimeOutcomeKind.Waiting,
            now,
            null,
            "chain waiting",
            ["chain.md"]));

        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync();

        Assert.Single(snapshot.WorkflowStates);
        Assert.Equal(WorkflowResolutionState.Active, snapshot.WorkflowStates[0].State);
        Assert.Single(snapshot.StageStates);
        Assert.Single(snapshot.TransitionRuns);
        Assert.Equal(TransitionDurableState.OutputValidated, snapshot.TransitionRuns[0].State);
        Assert.Single(snapshot.TransitionEvidence);
        Assert.Single(snapshot.Products);
        Assert.Equal(ProductIdentity.ExecutablePlan, snapshot.Products[0].Identity);
        Assert.Single(snapshot.GateEvaluations);
        Assert.Single(snapshot.EffectRecords);
        Assert.Single(snapshot.Blockers);
        Assert.Single(snapshot.RecoveryMarkers);
        Assert.Single(snapshot.WorkflowChainRuns);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        Assert.Equal("canonical", await ScalarStringAsync(connection, "SELECT value FROM workspace_metadata WHERE key = 'persistence_state';"));
    }

    private static readonly string[] CanonicalTables =
    [
        "canonical_workflow_states",
        "canonical_stage_states",
        "canonical_transition_runs",
        "canonical_transition_evidence",
        "canonical_product_records",
        "canonical_gate_evaluations",
        "canonical_effect_records",
        "canonical_blockers",
        "canonical_recovery_markers",
        "canonical_workflow_chain_runs",
        "session_continuity_profiles",
        "decision_session_scopes",
        "decision_session_lineage",
        "decision_session_active",
        "session_recovery_attempts",
        "session_recovery_plans",
        "session_recovery_sources",
        "decision_session_turns",
        "session_transition_correlations",
        "decision_session_legacy_imports",
    ];

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-canonical-persistence-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);
        object? scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt64(scalar) == 1;
    }

    private static async Task<string?> ScalarStringAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? scalar = await command.ExecuteScalarAsync();
        return scalar is null or DBNull ? null : Convert.ToString(scalar);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
