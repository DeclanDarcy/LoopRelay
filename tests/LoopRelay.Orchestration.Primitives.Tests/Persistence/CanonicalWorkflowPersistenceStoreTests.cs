using LoopRelay.Core.Models.Identity;
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
        foreach (string table in ExpectedTables)
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
        foreach (string table in ExpectedTables)
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

        Assert.Contains("Unsupported workspace schema", exception.Message, StringComparison.Ordinal);
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
        await store.AppendWarningAsync(new CanonicalWarningRecord(
            "warn_001",
            workflow,
            stage,
            transition,
            WarningCategory.Human,
            "review required",
            "workflow resolver",
            "approve review",
            ["warning.md"],
            now.AddSeconds(40)));
        await store.UpsertRecoveryMarkerAsync(new CanonicalRecoveryMarkerRecord(
            "recovery-001",
            workflow,
            stage,
            transition,
            new RecoveryDefinition("PlanRecovery", "resume from output validation", ["resume"], ["silent repair"]),
            ["recovery.md"],
            now.AddSeconds(50)));
        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync();

        Assert.Single(snapshot.WorkflowStates);
        Assert.Equal(WorkflowResolutionState.Active, snapshot.WorkflowStates[0].State);
        Assert.Single(snapshot.StageStates);
        Assert.Single(snapshot.TransitionRuns);
        Assert.Equal(TransitionDurableState.OutputValidated, snapshot.TransitionRuns[0].State);
        Assert.Single(snapshot.TransitionEvidence);
        Assert.Single(snapshot.Products);
        Assert.Equal(ProductIdentity.ExecutablePlan, snapshot.Products[0].Identity);
        Assert.Equal("1", snapshot.Products[0].SchemaVersion);
        Assert.Single(snapshot.GateEvaluations);
        Assert.Single(snapshot.EffectRecords);
        Assert.Single(snapshot.Warnings);
        Assert.Single(snapshot.RecoveryMarkers);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        Assert.Equal("canonical", await ScalarStringAsync(connection, "SELECT value FROM workspace_metadata WHERE key = 'persistence_state';"));
    }

    [Fact]
    public async Task Store_round_trips_read_receipts_ordered_by_ledger_insertion()
    {
        Repository repository = CreateRepository();
        CanonicalWorkflowPersistenceStore store = new(repository);
        DateTimeOffset consumedAt = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

        CanonicalReadReceiptRecord diskReceipt = new(
            CausalUlid.NewId("rcpt"),
            "run_001",
            "Plan",
            "WriteExecutablePlan",
            "att_001",
            "0123456789abcdef0123456789abcdef01234567",
            [".agents/plan.md", ".agents/projections/"],
            new Dictionary<string, string?>
            {
                [".agents/plan.md"] = "fedcba9876543210fedcba9876543210fedcba98",
                [".agents/projections/"] = null,
            },
            [
                new CanonicalReadReceiptFile(".agents/plan.md", "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678901234567890123456789012"),
                new CanonicalReadReceiptFile(".agents/projections/next.md", "1111111111111111111111111111111111111111111111111111111111111111"),
            ],
            [new CanonicalReadReceiptProduct("ExecutablePlan", "causal-hash-plan", "Valid")],
            "Usable",
            consumedAt.AddMinutes(1));
        CanonicalReadReceiptRecord ledgerOnlyReceipt = new(
            CausalUlid.NewId("rcpt"),
            "run_001",
            "Execute",
            "CollectExecutionDetails",
            null,
            null,
            [],
            null,
            [],
            [new CanonicalReadReceiptProduct("ExecutionMilestones", "causal-hash-milestones", "Stale")],
            "ledger product `ExecutionMilestones` is Stale",
            consumedAt);
        CanonicalReadReceiptRecord tieBreakReceipt = ledgerOnlyReceipt with
        {
            ReceiptId = "rcpt_00000000000000000000000000",
        };

        await store.AppendReadReceiptAsync(diskReceipt);
        await store.AppendReadReceiptAsync(ledgerOnlyReceipt);
        await store.AppendReadReceiptAsync(tieBreakReceipt);

        IReadOnlyList<CanonicalReadReceiptRecord> receipts = await store.ReadReadReceiptsAsync();

        // Ledger sequence (insertion order) is the ordering authority: neither the earlier
        // consumed_at of the ledger-only receipt nor the lexically-smallest receipt id of the
        // last-appended receipt reorders the history.
        Assert.Equal(3, receipts.Count);
        Assert.Equal(diskReceipt.ReceiptId, receipts[0].ReceiptId);
        Assert.Equal(ledgerOnlyReceipt.ReceiptId, receipts[1].ReceiptId);
        Assert.Equal(tieBreakReceipt.ReceiptId, receipts[2].ReceiptId);

        CanonicalReadReceiptRecord readLedgerOnly = receipts[1];
        Assert.StartsWith("rcpt_", readLedgerOnly.ReceiptId, StringComparison.Ordinal);
        Assert.Equal("run_001", readLedgerOnly.RunId);
        Assert.Equal("Execute", readLedgerOnly.WorkflowIdentity);
        Assert.Equal("CollectExecutionDetails", readLedgerOnly.TransitionIdentity);
        Assert.Null(readLedgerOnly.AttemptId);
        Assert.Null(readLedgerOnly.CommitHash);
        Assert.Empty(readLedgerOnly.InputSurfaces);
        Assert.Null(readLedgerOnly.SurfaceTreeHashes);
        Assert.Empty(readLedgerOnly.Files);
        Assert.Single(readLedgerOnly.Products);
        Assert.Equal(new CanonicalReadReceiptProduct("ExecutionMilestones", "causal-hash-milestones", "Stale"), readLedgerOnly.Products[0]);
        Assert.Equal("ledger product `ExecutionMilestones` is Stale", readLedgerOnly.Validation);
        Assert.Equal(consumedAt, readLedgerOnly.ConsumedAt);

        CanonicalReadReceiptRecord readDisk = receipts[0];
        Assert.Equal("Plan", readDisk.WorkflowIdentity);
        Assert.Equal("WriteExecutablePlan", readDisk.TransitionIdentity);
        Assert.Equal("att_001", readDisk.AttemptId);
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", readDisk.CommitHash);
        Assert.Equal(diskReceipt.InputSurfaces, readDisk.InputSurfaces);
        Assert.NotNull(readDisk.SurfaceTreeHashes);
        Assert.Equal(2, readDisk.SurfaceTreeHashes!.Count);
        Assert.Equal("fedcba9876543210fedcba9876543210fedcba98", readDisk.SurfaceTreeHashes[".agents/plan.md"]);
        Assert.Null(readDisk.SurfaceTreeHashes[".agents/projections/"]);
        Assert.Equal(diskReceipt.Files, readDisk.Files);
        Assert.Equal(diskReceipt.Products, readDisk.Products);
        Assert.Equal("Usable", readDisk.Validation);
        Assert.Equal(consumedAt.AddMinutes(1), readDisk.ConsumedAt);
    }

    [Fact]
    public async Task Read_receipt_reads_return_empty_when_database_predates_read_receipts_table()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '4');
                """);
        }

        CanonicalWorkflowPersistenceStore store = new(repository);

        Assert.Empty(await store.ReadReadReceiptsAsync());
    }

    [Fact]
    public async Task Receipts_warnings_and_gate_evaluations_carry_their_transition_run_lineage()
    {
        Repository repository = CreateRepository();
        CanonicalWorkflowPersistenceStore store = new(repository);
        DateTimeOffset now = new(2026, 7, 11, 9, 0, 0, TimeSpan.Zero);

        await store.AppendReadReceiptAsync(new CanonicalReadReceiptRecord(
            CausalUlid.NewId("rcpt"),
            "run_lineage",
            "Plan",
            "WriteExecutablePlan",
            "att_lineage",
            null,
            [],
            null,
            [],
            [],
            "Usable",
            now,
            "tr_lineage"));
        await store.AppendWarningAsync(new CanonicalWarningRecord(
            CausalUlid.NewId("warn"),
            new WorkflowIdentity("Plan"),
            new WorkflowStageIdentity("Planning"),
            new WorkflowTransitionIdentity("WriteExecutablePlan"),
            WarningCategory.Validation,
            "input surface dirty",
            "canonical transition runtime",
            "commit the surface",
            [],
            now,
            "tr_lineage"));
        await store.AppendGateEvaluationAsync(new CanonicalGateEvaluationRecord(
            0,
            new WorkflowIdentity("Plan"),
            new WorkflowStageIdentity("Planning"),
            new WorkflowTransitionIdentity("WriteExecutablePlan"),
            new GateIdentity("WriteExecutablePlan.Input"),
            GateStatus.Satisfied,
            now,
            [],
            "gate satisfied",
            [],
            "tr_lineage"));

        CanonicalReadReceiptRecord receipt = Assert.Single(await store.ReadReadReceiptsAsync());
        Assert.Equal("tr_lineage", receipt.TransitionRunId);
        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync();
        Assert.Equal("tr_lineage", Assert.Single(snapshot.Warnings).TransitionRunId);
        Assert.Equal("tr_lineage", Assert.Single(snapshot.GateEvaluations).TransitionRunId);
    }

    [Fact]
    public async Task Chain_boundary_events_append_and_read_back_in_ledger_insertion_order()
    {
        Repository repository = CreateRepository();
        CanonicalWorkflowPersistenceStore store = new(repository);
        DateTimeOffset now = new(2026, 7, 11, 9, 0, 0, TimeSpan.Zero);

        var advanced = new CanonicalChainBoundaryEventRecord(
            CausalUlid.NewId("bnd"),
            "run_chain",
            "CanonicalDelivery",
            new WorkflowIdentity("Plan"),
            new WorkflowIdentity("Execute"),
            GateStatus.Satisfied,
            GateStatus.Satisfied,
            GateStatus.Satisfied,
            "Advanced",
            "Advanced from Plan to Execute.",
            ["plan-exit.md"],
            """{"canAdvance":true}""",
            now);
        var stopped = new CanonicalChainBoundaryEventRecord(
            CausalUlid.NewId("bnd"),
            null,
            "CanonicalDelivery",
            new WorkflowIdentity("Execute"),
            null,
            GateStatus.Unsatisfied,
            null,
            null,
            "StoppedAtBoundary",
            "Stopped before Completion because a workflow boundary gate was unsatisfied.",
            [],
            """{"canAdvance":false}""",
            now.AddMinutes(1));

        await store.AppendChainBoundaryEventAsync(advanced);
        await store.AppendChainBoundaryEventAsync(stopped);

        IReadOnlyList<CanonicalChainBoundaryEventRecord> events = await store.ReadChainBoundaryEventsAsync();

        Assert.Equal(2, events.Count);
        CanonicalChainBoundaryEventRecord readAdvanced = events[0];
        Assert.Equal(advanced.BoundaryId, readAdvanced.BoundaryId);
        Assert.Equal("run_chain", readAdvanced.RunId);
        Assert.Equal("CanonicalDelivery", readAdvanced.ChainIdentity);
        Assert.Equal(new WorkflowIdentity("Plan"), readAdvanced.SourceWorkflow);
        Assert.Equal(new WorkflowIdentity("Execute"), readAdvanced.TargetWorkflow);
        Assert.Equal(GateStatus.Satisfied, readAdvanced.ExitGateStatus);
        Assert.Equal(GateStatus.Satisfied, readAdvanced.EntryGateStatus);
        Assert.Equal(GateStatus.Satisfied, readAdvanced.TransferGateStatus);
        Assert.Equal("Advanced", readAdvanced.Decision);
        Assert.Equal("Advanced from Plan to Execute.", readAdvanced.Explanation);
        Assert.Equal(["plan-exit.md"], readAdvanced.Evidence);
        Assert.Equal("""{"canAdvance":true}""", readAdvanced.BoundaryJson);
        Assert.Equal(now, readAdvanced.RecordedAt);

        CanonicalChainBoundaryEventRecord readStopped = events[1];
        Assert.Equal(stopped.BoundaryId, readStopped.BoundaryId);
        Assert.Null(readStopped.RunId);
        Assert.Null(readStopped.TargetWorkflow);
        Assert.Equal(GateStatus.Unsatisfied, readStopped.ExitGateStatus);
        Assert.Null(readStopped.EntryGateStatus);
        Assert.Null(readStopped.TransferGateStatus);
        Assert.Equal("StoppedAtBoundary", readStopped.Decision);
        Assert.Empty(readStopped.Evidence);
    }

    [Fact]
    public async Task Chain_boundary_reads_return_empty_when_database_predates_the_table()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '5');
                """);
        }

        CanonicalWorkflowPersistenceStore store = new(repository);

        Assert.Empty(await store.ReadChainBoundaryEventsAsync());
    }

    [Fact]
    public async Task Pre_v6_fact_rows_read_back_with_null_lineage_without_migrating()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '5');

                CREATE TABLE read_receipts(
                    receipt_id text primary key,
                    run_id text not null,
                    workflow_identity text not null,
                    transition_identity text not null,
                    attempt_id text,
                    commit_hash text,
                    input_surfaces_json text not null,
                    surface_tree_hashes_json text,
                    files_json text not null,
                    products_json text not null,
                    validation text not null,
                    consumed_at text not null
                );
                INSERT INTO read_receipts (
                    receipt_id, run_id, workflow_identity, transition_identity, attempt_id, commit_hash,
                    input_surfaces_json, surface_tree_hashes_json, files_json, products_json, validation, consumed_at)
                VALUES (
                    'rcpt_pre_v6', '', 'Plan', 'WriteExecutablePlan', NULL, NULL,
                    '[]', NULL, '[]', '[]', 'Usable', '2026-07-10T12:00:00.0000000Z');

                CREATE TABLE evaluation_warnings(
                    warning_id text primary key,
                    workflow_identity text not null,
                    stage_identity text,
                    transition_identity text,
                    category text not null,
                    concern text not null,
                    authority text not null,
                    remediation text not null,
                    evidence_json text not null,
                    created_at text not null
                );
                INSERT INTO evaluation_warnings (
                    warning_id, workflow_identity, stage_identity, transition_identity, category,
                    concern, authority, remediation, evidence_json, created_at)
                VALUES (
                    'warn_pre_v6', 'Plan', 'Planning', 'WriteExecutablePlan', 'Validation',
                    'pre-v6 concern', 'legacy', 'rerun', '[]', '2026-07-10T12:00:00.0000000Z');

                CREATE TABLE canonical_gate_evaluations(
                    evaluation_id integer primary key autoincrement,
                    workflow_identity text not null,
                    stage_identity text,
                    transition_identity text,
                    gate_identity text not null,
                    status text not null,
                    evaluated_at text not null,
                    requirements_json text not null,
                    explanation text not null,
                    evidence_json text not null
                );
                INSERT INTO canonical_gate_evaluations (
                    workflow_identity, stage_identity, transition_identity, gate_identity, status,
                    evaluated_at, requirements_json, explanation, evidence_json)
                VALUES (
                    'Plan', 'Planning', 'WriteExecutablePlan', 'WriteExecutablePlan.Input', 'Satisfied',
                    '2026-07-10T12:00:00.0000000Z', '[]', 'pre-v6 gate', '[]');

                CREATE TABLE canonical_workflow_states(
                    workflow_identity text primary key, state text not null, current_stage text,
                    outcome text, updated_at text not null, evidence_json text not null);
                CREATE TABLE canonical_stage_states(
                    workflow_identity text not null, stage_identity text not null, state text not null,
                    updated_at text not null, evidence_json text not null,
                    primary key(workflow_identity, stage_identity));
                CREATE TABLE canonical_transition_runs(
                    run_id text primary key, workflow_identity text not null, stage_identity text not null,
                    transition_identity text not null, state text not null, outcome text not null,
                    started_at text not null, completed_at text, input_snapshot_hash text,
                    explanation text not null, evidence_json text not null);
                CREATE TABLE canonical_transition_evidence(
                    evidence_id integer primary key autoincrement, run_id text not null,
                    transition_identity text not null, event_name text not null, recorded_at text not null,
                    state text not null, explanation text not null, evidence_json text not null,
                    document_json text not null);
                CREATE TABLE canonical_product_records(
                    product_identity text primary key, producer_workflow text not null,
                    producer_transition text not null, intended_consumers_json text not null,
                    repository_ownership text not null, authority text not null,
                    storage_representations_json text not null, causal_identity text not null,
                    freshness text not null, validation_state text not null, lifecycle text not null,
                    evidence_locations_json text not null, updated_at text not null);
                CREATE TABLE canonical_effect_records(
                    record_id integer primary key autoincrement, run_id text not null,
                    effect_identity text not null, category text not null, status text not null,
                    recorded_at text not null, explanation text not null, evidence_json text not null);
                CREATE TABLE canonical_recovery_markers(
                    marker_id text primary key, workflow_identity text not null, stage_identity text,
                    transition_identity text, semantics text not null, supported_actions_json text not null,
                    unsupported_actions_json text not null, evidence_json text not null, recorded_at text not null);
                """);
        }

        // Reads open the database read-only and never migrate, so the pre-v6 shape must read
        // back intact with null lineage rather than crashing on the missing column.
        CanonicalWorkflowPersistenceStore store = new(repository);

        CanonicalReadReceiptRecord receipt = Assert.Single(await store.ReadReadReceiptsAsync());
        Assert.Equal("rcpt_pre_v6", receipt.ReceiptId);
        Assert.Null(receipt.TransitionRunId);
        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync();
        CanonicalWarningRecord warning = Assert.Single(snapshot.Warnings);
        Assert.Equal("pre-v6 concern", warning.Concern);
        Assert.Null(warning.TransitionRunId);
        CanonicalGateEvaluationRecord gate = Assert.Single(snapshot.GateEvaluations);
        Assert.Equal("pre-v6 gate", gate.Explanation);
        Assert.Null(gate.TransitionRunId);
    }

    [Fact]
    public async Task Policy_resolutions_append_and_read_back_in_ledger_insertion_order()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 11, 9, 0, 0, TimeSpan.Zero);
        // The first-appended record carries a lexically LARGER id and a LATER wall-clock stamp
        // than the second, so only ledger insertion order (rowid) can return them append-first.
        var first = new CanonicalPolicyResolutionRecord(
            "res_zzzz_appended_first",
            "pol_v1_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "policy-v1",
            """{"schemaVersion":"policy-v1"}""",
            """[{"field":"decisions.sessionResume","layer":"builtIn","origin":"built-in"}]""",
            "settings:/workspace/settings.json",
            now.AddMinutes(1));
        var second = new CanonicalPolicyResolutionRecord(
            "res_aaaa_appended_second",
            "pol_v1_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            "policy-v1",
            """{"schemaVersion":"policy-v1","changed":true}""",
            "[]",
            "built-in",
            now);

        await store.AppendPolicyResolutionAsync(first);
        await store.AppendPolicyResolutionAsync(second);

        IReadOnlyList<CanonicalPolicyResolutionRecord> resolutions = await store.ReadPolicyResolutionsAsync();

        Assert.Equal(2, resolutions.Count);
        CanonicalPolicyResolutionRecord readFirst = resolutions[0];
        Assert.Equal(first.ResolutionId, readFirst.ResolutionId);
        Assert.Equal(first.PolicyId, readFirst.PolicyId);
        Assert.Equal("policy-v1", readFirst.SchemaVersion);
        Assert.Equal(first.ResolvedJson, readFirst.ResolvedJson);
        Assert.Equal(first.ProvenanceJson, readFirst.ProvenanceJson);
        Assert.Equal("settings:/workspace/settings.json", readFirst.SourceDescription);
        Assert.Equal(now.AddMinutes(1), readFirst.RecordedAt);
        Assert.Equal(second.ResolutionId, resolutions[1].ResolutionId);
    }

    [Fact]
    public async Task Rendered_prompts_append_and_read_back_in_ledger_insertion_order()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 11, 10, 0, 0, TimeSpan.Zero);
        // The first-appended record carries a lexically LARGER id and a LATER wall-clock stamp
        // than the second, so only ledger insertion order (rowid) can return them append-first.
        var first = new CanonicalRenderedPromptRecord(
            "rp_zzzz_appended_first",
            "tr_001",
            "att_001",
            "GenerateSystemPromptForFirstExecutionAgent",
            "aaaa1111",
            "feedfeed",
            "PROMPT ONE",
            [new CanonicalReadReceiptFile(".agents/operational_context.md", "hash-a")],
            "pol_v1_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            now.AddMinutes(1),
            PersistenceId: "rpp_001",
            PromptPolicyProfileId: "prompt-policy-001",
            ConsumedInputManifestId: "inputs_001",
            RenderedEncoding: "utf-8");
        var second = new CanonicalRenderedPromptRecord(
            "rp_aaaa_appended_second",
            "tr_001",
            null,
            "ContinueExecution",
            null,
            "beefbeef",
            "PROMPT TWO",
            [],
            null,
            now,
            "ses_001",
            "turn_001");

        await store.AppendRenderedPromptAsync(first);
        await store.AppendRenderedPromptAsync(second);

        IReadOnlyList<CanonicalRenderedPromptRecord> prompts = await store.ReadRenderedPromptsAsync();

        Assert.Equal(2, prompts.Count);
        CanonicalRenderedPromptRecord readFirst = prompts[0];
        Assert.Equal(first.RenderedPromptId, readFirst.RenderedPromptId);
        Assert.Equal("tr_001", readFirst.TransitionRunId);
        Assert.Equal("att_001", readFirst.AttemptId);
        Assert.Equal("GenerateSystemPromptForFirstExecutionAgent", readFirst.PromptIdentity);
        Assert.Equal("aaaa1111", readFirst.TemplateSourceHash);
        Assert.Equal("feedfeed", readFirst.RenderedSha256);
        Assert.Equal("PROMPT ONE", readFirst.RenderedText);
        CanonicalReadReceiptFile consumed = Assert.Single(readFirst.ConsumedInputs);
        Assert.Equal(".agents/operational_context.md", consumed.Path);
        Assert.Equal("hash-a", consumed.Sha256);
        Assert.Equal("pol_v1_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", readFirst.PolicyId);
        Assert.Equal("rpp_001", readFirst.PersistenceId);
        Assert.Equal("prompt-policy-001", readFirst.PromptPolicyProfileId);
        Assert.Equal("inputs_001", readFirst.ConsumedInputManifestId);
        Assert.Equal("utf-8", readFirst.RenderedEncoding);
        Assert.Equal(now.AddMinutes(1), readFirst.RenderedAt);
        Assert.Null(readFirst.SessionId);
        CanonicalRenderedPromptRecord readSecond = prompts[1];
        Assert.Equal(second.RenderedPromptId, readSecond.RenderedPromptId);
        Assert.Null(readSecond.AttemptId);
        Assert.Null(readSecond.TemplateSourceHash);
        Assert.Empty(readSecond.ConsumedInputs);
        Assert.Null(readSecond.PolicyId);
        Assert.Equal("ses_001", readSecond.SessionId);
        Assert.Equal("turn_001", readSecond.TurnId);
    }

    [Fact]
    public async Task Prompt_dispatch_lifecycle_events_append_and_read_back_in_order()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 11, 10, 0, 0, TimeSpan.Zero);

        foreach (PromptDispatchState state in new[]
                 {
                     PromptDispatchState.Planned,
                     PromptDispatchState.Authorized,
                     PromptDispatchState.Started,
                     PromptDispatchState.Observed,
                 })
        {
            await store.AppendPromptDispatchEventAsync(new CanonicalPromptDispatchEventRecord(
                0, "dispatch_001", "rpf_001", "rpp_001", "ws_001", "run_001",
                "wfi_001", "tr_001", "att_001", "runtime_001", "ses_001", "turn_001",
                state, now, [state.ToString()]));
        }

        IReadOnlyList<CanonicalPromptDispatchEventRecord> events =
            await store.ReadPromptDispatchEventsAsync();

        Assert.Equal(
            [PromptDispatchState.Planned, PromptDispatchState.Authorized,
             PromptDispatchState.Started, PromptDispatchState.Observed],
            events.Select(item => item.State));
        Assert.All(events, item => Assert.Equal("rpf_001", item.RenderedPromptId));
        Assert.All(events, item => Assert.Equal("ses_001", item.SessionId));
        Assert.All(events, item => Assert.Equal("turn_001", item.TurnId));
        Assert.Equal(["Observed"], events[^1].Evidence);
    }

    [Fact]
    public async Task Agent_turns_append_and_read_back_with_v9_evidence_fields()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 11, 11, 0, 0, TimeSpan.Zero);
        var completed = new AgentTurnRecord(
            "turn_evidence_1",
            "ses_evidence_1",
            0,
            now,
            "Completed",
            "sha-of-transport-text",
            1200,
            340,
            800,
            null,
            null);
        var failed = new AgentTurnRecord(
            "turn_evidence_2",
            "ses_evidence_1",
            1,
            now.AddMinutes(1),
            "Failed",
            "sha-of-second-send",
            10,
            0,
            0,
            "UsageLimit",
            "You've hit your usage limit. Try again at Jul 12, 2026 9:13 AM.");

        await store.AppendAgentTurnAsync(completed);
        await store.AppendAgentTurnAsync(failed);

        IReadOnlyList<AgentTurnRecord> turns = await store.ReadAgentTurnsAsync();

        Assert.Equal(2, turns.Count);
        AgentTurnRecord readCompleted = turns[0];
        Assert.Equal("turn_evidence_1", readCompleted.TurnId);
        Assert.Equal("Completed", readCompleted.State);
        Assert.Equal("sha-of-transport-text", readCompleted.PromptSha256);
        Assert.Equal(1200L, readCompleted.PromptTokens);
        Assert.Equal(340L, readCompleted.OutputTokens);
        Assert.Equal(800L, readCompleted.CachedInputTokens);
        Assert.Null(readCompleted.DiagnosticsKind);
        Assert.Null(readCompleted.Diagnostics);
        AgentTurnRecord readFailed = turns[1];
        Assert.Equal("Failed", readFailed.State);
        Assert.Equal("UsageLimit", readFailed.DiagnosticsKind);
        Assert.Equal(failed.Diagnostics, readFailed.Diagnostics);
    }

    [Fact]
    public async Task Pre_v9_agent_turns_read_back_with_null_evidence_without_migrating()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '8');

                CREATE TABLE agent_turns(
                    turn_id text primary key,
                    session_id text not null,
                    turn_index integer not null,
                    recorded_at text not null,
                    unique(session_id, turn_index)
                );
                INSERT INTO agent_turns (turn_id, session_id, turn_index, recorded_at)
                VALUES ('turn_pre_v9', 'ses_pre_v9', 0, '2026-07-11T10:00:00.0000000Z');
                """);
        }

        // Reads open the database read-only and never migrate, so the pre-v9 shape must read
        // back intact with null evidence rather than crashing on the missing columns.
        CanonicalWorkflowPersistenceStore store = new(repository);

        AgentTurnRecord turn = Assert.Single(await store.ReadAgentTurnsAsync());
        Assert.Equal("turn_pre_v9", turn.TurnId);
        Assert.Null(turn.State);
        Assert.Null(turn.PromptSha256);
        Assert.Null(turn.PromptTokens);
        Assert.Null(turn.DiagnosticsKind);
    }

    [Fact]
    public async Task Runtime_prerequisites_append_and_read_back_in_ledger_insertion_order()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        // The first-appended record carries a lexically LARGER id and a LATER wall-clock stamp
        // than the second, so only ledger insertion order (rowid) can return them append-first.
        var first = new CanonicalRuntimePrerequisiteRecord(
            "pre_zzzz_appended_first",
            null,
            now.AddMinutes(1),
            """[{"id":"runtime.codex_executable.missing","severity":"Error","message":"CODEX_EXECUTABLE is not set."}]""");
        var second = new CanonicalRuntimePrerequisiteRecord(
            "pre_aaaa_appended_second",
            "run_001",
            now,
            "[]");

        await store.AppendRuntimePrerequisiteAsync(first);
        await store.AppendRuntimePrerequisiteAsync(second);

        IReadOnlyList<CanonicalRuntimePrerequisiteRecord> checks = await store.ReadRuntimePrerequisitesAsync();

        Assert.Equal(2, checks.Count);
        Assert.Equal(first.PrerequisiteCheckId, checks[0].PrerequisiteCheckId);
        Assert.Null(checks[0].RunId);
        Assert.Equal(now.AddMinutes(1), checks[0].CheckedAt);
        Assert.Equal(first.DiagnosticsJson, checks[0].DiagnosticsJson);
        Assert.Equal("run_001", checks[1].RunId);
    }

    [Fact]
    public async Task Runtime_prerequisite_reads_return_empty_when_database_predates_the_table()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '8');
                """);
        }

        CanonicalWorkflowPersistenceStore store = new(repository);

        Assert.Empty(await store.ReadRuntimePrerequisitesAsync());
    }

    [Fact]
    public async Task Rendered_prompt_reads_return_empty_when_database_predates_the_table()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '7');
                """);
        }

        CanonicalWorkflowPersistenceStore store = new(repository);

        Assert.Empty(await store.ReadRenderedPromptsAsync());
    }

    [Fact]
    public async Task Policy_resolution_reads_return_empty_when_database_predates_the_table()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '6');
                """);
        }

        CanonicalWorkflowPersistenceStore store = new(repository);

        Assert.Empty(await store.ReadPolicyResolutionsAsync());
    }

    [Fact]
    public async Task Pre_v7_attempts_read_back_with_null_policy_identity_without_migrating()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '6');

                CREATE TABLE attempts(
                    attempt_id text primary key,
                    transition_run_id text not null,
                    workflow_instance_id text not null,
                    run_id text not null,
                    attempt_index integer not null,
                    started_at text not null,
                    completed_at text,
                    outcome text,
                    unique(transition_run_id, attempt_index)
                );
                INSERT INTO attempts (
                    attempt_id, transition_run_id, workflow_instance_id, run_id, attempt_index,
                    started_at, completed_at, outcome)
                VALUES (
                    'att_pre_v7', 'tr_pre_v7', 'wfi_pre_v7', 'run_pre_v7', 1,
                    '2026-07-10T12:00:00.0000000Z', NULL, NULL);
                """);
        }

        // Reads open the database read-only and never migrate, so the pre-v7 shape must read
        // back intact with a null policy identity rather than crashing on the missing column.
        CanonicalWorkflowPersistenceStore store = new(repository);

        AttemptRecord attempt = Assert.Single(await store.ReadAttemptsAsync());
        Assert.Equal("att_pre_v7", attempt.AttemptId);
        Assert.Null(attempt.PolicyId);
    }

    private static readonly string[] ExpectedTables =
    [
        "canonical_workflow_states",
        "canonical_stage_states",
        "canonical_transition_runs",
        "canonical_transition_evidence",
        "canonical_product_records",
        "canonical_gate_evaluations",
        "canonical_effect_records",
        "evaluation_warnings",
        "canonical_recovery_markers",
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
        "canonical_chain_boundary_events",
        "canonical_policy_resolutions",
        "canonical_rendered_prompts",
        "prompt_dispatch_events",
        "execution_recommendation_evidence",
        "runtime_profile_evaluations",
        "canonical_runtime_prerequisites",
        "workspace_identity",
        "runs",
        "workflow_instances",
        "attempts",
        "agent_sessions",
        "agent_turns",
        "read_receipts",
        "history_evidence_sets",
        "history_evidence_items",
        "compatibility_import_operations",
        "compatibility_import_events",
        "canonical_projection_effects",
        "persistence_projection_checkpoints",
        "workspace_schema_migrations",
        "workspace_schema_convergences",
        "workspace_identity_metadata",
    ];

    [Fact]
    public async Task Product_schema_version_roundtrips_through_upsert_and_snapshot()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        await store.UpsertProductAsync(new ProductRecord(
            ProductIdentity.ExecutablePlan,
            WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity("WriteExecutablePlan"),
            [WorkflowIdentity.Execute],
            "repository-owned",
            "canonical",
            [".agents/plan.md"],
            "causal-hash",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            ["product.md"],
            SchemaVersion: "2"));

        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync();

        ProductRecord product = Assert.Single(snapshot.Products);
        Assert.Equal("2", product.SchemaVersion);
    }

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
