using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Persistence;

public sealed class CanonicalWorkflowPersistenceStore(Repository _repository)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task UpsertWorkflowStateAsync(
        CanonicalWorkflowStateRecord state,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_workflow_states (
                workflow_identity, state, current_stage, outcome, updated_at, evidence_json
            )
            VALUES (
                $workflow_identity, $state, $current_stage, $outcome, $updated_at, $evidence_json
            )
            ON CONFLICT(workflow_identity) DO UPDATE SET
                state = excluded.state,
                current_stage = excluded.current_stage,
                outcome = excluded.outcome,
                updated_at = excluded.updated_at,
                evidence_json = excluded.evidence_json;
            """,
            cancellationToken,
            ("$workflow_identity", state.Workflow.Value),
            ("$state", state.State.ToString()),
            ("$current_stage", state.CurrentStage?.Value),
            ("$outcome", state.Outcome?.ToString()),
            ("$updated_at", Format(state.UpdatedAt)),
            ("$evidence_json", Json(state.Evidence)));
    }

    public async Task UpsertStageStateAsync(
        CanonicalStageStateRecord state,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_stage_states (
                workflow_identity, stage_identity, state, updated_at, evidence_json
            )
            VALUES (
                $workflow_identity, $stage_identity, $state, $updated_at, $evidence_json
            )
            ON CONFLICT(workflow_identity, stage_identity) DO UPDATE SET
                state = excluded.state,
                updated_at = excluded.updated_at,
                evidence_json = excluded.evidence_json;
            """,
            cancellationToken,
            ("$workflow_identity", state.Workflow.Value),
            ("$stage_identity", state.Stage.Value),
            ("$state", state.State.ToString()),
            ("$updated_at", Format(state.UpdatedAt)),
            ("$evidence_json", Json(state.Evidence)));
    }

    public async Task UpsertTransitionRunAsync(
        CanonicalTransitionRunRecord run,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_transition_runs (
                run_id, workflow_identity, stage_identity, transition_identity, state, outcome,
                started_at, completed_at, input_snapshot_hash, explanation, evidence_json
            )
            VALUES (
                $run_id, $workflow_identity, $stage_identity, $transition_identity, $state, $outcome,
                $started_at, $completed_at, $input_snapshot_hash, $explanation, $evidence_json
            )
            ON CONFLICT(run_id) DO UPDATE SET
                workflow_identity = excluded.workflow_identity,
                stage_identity = excluded.stage_identity,
                transition_identity = excluded.transition_identity,
                state = excluded.state,
                outcome = excluded.outcome,
                started_at = excluded.started_at,
                completed_at = excluded.completed_at,
                input_snapshot_hash = excluded.input_snapshot_hash,
                explanation = excluded.explanation,
                evidence_json = excluded.evidence_json;
            """,
            cancellationToken,
            ("$run_id", run.RunId),
            ("$workflow_identity", run.Workflow.Value),
            ("$stage_identity", run.Stage.Value),
            ("$transition_identity", run.Transition.Value),
            ("$state", run.State.ToString()),
            ("$outcome", run.Outcome.ToString()),
            ("$started_at", Format(run.StartedAt)),
            ("$completed_at", run.CompletedAt is null ? null : Format(run.CompletedAt.Value)),
            ("$input_snapshot_hash", run.InputSnapshotHash),
            ("$explanation", run.Explanation),
            ("$evidence_json", Json(run.Evidence)));
    }

    public async Task AppendTransitionEvidenceAsync(
        CanonicalTransitionEvidenceRecord evidence,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_transition_evidence (
                run_id, transition_identity, event_name, recorded_at, state,
                explanation, evidence_json, document_json
            )
            VALUES (
                $run_id, $transition_identity, $event_name, $recorded_at, $state,
                $explanation, $evidence_json, $document_json
            );
            """,
            cancellationToken,
            ("$run_id", evidence.RunId),
            ("$transition_identity", evidence.Transition.Value),
            ("$event_name", evidence.EventName),
            ("$recorded_at", Format(evidence.RecordedAt)),
            ("$state", evidence.State.ToString()),
            ("$explanation", evidence.Explanation),
            ("$evidence_json", Json(evidence.Evidence)),
            ("$document_json", evidence.DocumentJson));
    }

    public async Task UpsertProductAsync(
        ProductRecord product,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_product_records (
                product_identity, producer_workflow, producer_transition, intended_consumers_json,
                repository_ownership, authority, storage_representations_json, causal_identity,
                freshness, validation_state, lifecycle, evidence_locations_json, updated_at
            )
            VALUES (
                $product_identity, $producer_workflow, $producer_transition, $intended_consumers_json,
                $repository_ownership, $authority, $storage_representations_json, $causal_identity,
                $freshness, $validation_state, $lifecycle, $evidence_locations_json, $updated_at
            )
            ON CONFLICT(product_identity) DO UPDATE SET
                producer_workflow = excluded.producer_workflow,
                producer_transition = excluded.producer_transition,
                intended_consumers_json = excluded.intended_consumers_json,
                repository_ownership = excluded.repository_ownership,
                authority = excluded.authority,
                storage_representations_json = excluded.storage_representations_json,
                causal_identity = excluded.causal_identity,
                freshness = excluded.freshness,
                validation_state = excluded.validation_state,
                lifecycle = excluded.lifecycle,
                evidence_locations_json = excluded.evidence_locations_json,
                updated_at = excluded.updated_at;
            """,
            cancellationToken,
            ("$product_identity", product.Identity.Value),
            ("$producer_workflow", product.ProducerWorkflow.Value),
            ("$producer_transition", product.ProducerTransition.Value),
            ("$intended_consumers_json", Json(product.IntendedConsumers.Select(consumer => consumer.Value).ToArray())),
            ("$repository_ownership", product.RepositoryOwnership),
            ("$authority", product.Authority),
            ("$storage_representations_json", Json(product.StorageRepresentations)),
            ("$causal_identity", product.CausalIdentity),
            ("$freshness", product.Freshness.ToString()),
            ("$validation_state", product.ValidationState.ToString()),
            ("$lifecycle", product.Lifecycle.ToString()),
            ("$evidence_locations_json", Json(product.EvidenceLocations)),
            ("$updated_at", Format(DateTimeOffset.UtcNow)));
    }

    public async Task AppendGateEvaluationAsync(
        CanonicalGateEvaluationRecord evaluation,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_gate_evaluations (
                workflow_identity, stage_identity, transition_identity, gate_identity, status,
                evaluated_at, requirements_json, explanation, evidence_json
            )
            VALUES (
                $workflow_identity, $stage_identity, $transition_identity, $gate_identity, $status,
                $evaluated_at, $requirements_json, $explanation, $evidence_json
            );
            """,
            cancellationToken,
            ("$workflow_identity", evaluation.Workflow.Value),
            ("$stage_identity", evaluation.Stage?.Value),
            ("$transition_identity", evaluation.Transition?.Value),
            ("$gate_identity", evaluation.Gate.Value),
            ("$status", evaluation.Status.ToString()),
            ("$evaluated_at", Format(evaluation.EvaluatedAt)),
            ("$requirements_json", Json(evaluation.Requirements)),
            ("$explanation", evaluation.Explanation),
            ("$evidence_json", Json(evaluation.Evidence)));
    }

    public async Task AppendEffectRecordAsync(
        CanonicalEffectRecord effect,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_effect_records (
                run_id, effect_identity, category, status, recorded_at, explanation, evidence_json
            )
            VALUES (
                $run_id, $effect_identity, $category, $status, $recorded_at, $explanation, $evidence_json
            );
            """,
            cancellationToken,
            ("$run_id", effect.RunId),
            ("$effect_identity", effect.Effect.Value),
            ("$category", effect.Category.ToString()),
            ("$status", effect.Status.ToString()),
            ("$recorded_at", Format(effect.RecordedAt)),
            ("$explanation", effect.Explanation),
            ("$evidence_json", Json(effect.Evidence)));
    }

    public async Task UpsertBlockerAsync(
        CanonicalBlockerRecord blocker,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_blockers (
                blocker_id, workflow_identity, stage_identity, transition_identity, category,
                reason, authority, required_action, recoverable, evidence_json, created_at, resolved_at
            )
            VALUES (
                $blocker_id, $workflow_identity, $stage_identity, $transition_identity, $category,
                $reason, $authority, $required_action, $recoverable, $evidence_json, $created_at, $resolved_at
            )
            ON CONFLICT(blocker_id) DO UPDATE SET
                workflow_identity = excluded.workflow_identity,
                stage_identity = excluded.stage_identity,
                transition_identity = excluded.transition_identity,
                category = excluded.category,
                reason = excluded.reason,
                authority = excluded.authority,
                required_action = excluded.required_action,
                recoverable = excluded.recoverable,
                evidence_json = excluded.evidence_json,
                created_at = excluded.created_at,
                resolved_at = excluded.resolved_at;
            """,
            cancellationToken,
            ("$blocker_id", blocker.BlockerId),
            ("$workflow_identity", blocker.Workflow.Value),
            ("$stage_identity", blocker.Stage?.Value),
            ("$transition_identity", blocker.Transition?.Value),
            ("$category", blocker.Blocker.Category.ToString()),
            ("$reason", blocker.Blocker.Reason),
            ("$authority", blocker.Blocker.Authority),
            ("$required_action", blocker.Blocker.RequiredAction),
            ("$recoverable", blocker.Blocker.Recoverable ? 1 : 0),
            ("$evidence_json", Json(blocker.Blocker.Evidence)),
            ("$created_at", Format(blocker.CreatedAt)),
            ("$resolved_at", blocker.ResolvedAt is null ? null : Format(blocker.ResolvedAt.Value)));
    }

    public async Task UpsertRecoveryMarkerAsync(
        CanonicalRecoveryMarkerRecord marker,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_recovery_markers (
                marker_id, workflow_identity, stage_identity, transition_identity, semantics,
                supported_actions_json, unsupported_actions_json, evidence_json, recorded_at
            )
            VALUES (
                $marker_id, $workflow_identity, $stage_identity, $transition_identity, $semantics,
                $supported_actions_json, $unsupported_actions_json, $evidence_json, $recorded_at
            )
            ON CONFLICT(marker_id) DO UPDATE SET
                workflow_identity = excluded.workflow_identity,
                stage_identity = excluded.stage_identity,
                transition_identity = excluded.transition_identity,
                semantics = excluded.semantics,
                supported_actions_json = excluded.supported_actions_json,
                unsupported_actions_json = excluded.unsupported_actions_json,
                evidence_json = excluded.evidence_json,
                recorded_at = excluded.recorded_at;
            """,
            cancellationToken,
            ("$marker_id", marker.MarkerId),
            ("$workflow_identity", marker.Workflow.Value),
            ("$stage_identity", marker.Stage?.Value),
            ("$transition_identity", marker.Transition?.Value),
            ("$semantics", marker.Recovery.Semantics),
            ("$supported_actions_json", Json(marker.Recovery.SupportedActions)),
            ("$unsupported_actions_json", Json(marker.Recovery.UnsupportedActions)),
            ("$evidence_json", Json(marker.Evidence)),
            ("$recorded_at", Format(marker.RecordedAt)));
    }

    public async Task UpsertWorkflowChainRunAsync(
        CanonicalWorkflowChainRunRecord run,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_workflow_chain_runs (
                chain_run_id, chain_identity, current_workflow, status, started_at,
                completed_at, explanation, evidence_json
            )
            VALUES (
                $chain_run_id, $chain_identity, $current_workflow, $status, $started_at,
                $completed_at, $explanation, $evidence_json
            )
            ON CONFLICT(chain_run_id) DO UPDATE SET
                chain_identity = excluded.chain_identity,
                current_workflow = excluded.current_workflow,
                status = excluded.status,
                started_at = excluded.started_at,
                completed_at = excluded.completed_at,
                explanation = excluded.explanation,
                evidence_json = excluded.evidence_json;
            """,
            cancellationToken,
            ("$chain_run_id", run.ChainRunId),
            ("$chain_identity", run.ChainIdentity),
            ("$current_workflow", run.CurrentWorkflow.Value),
            ("$status", run.Status.ToString()),
            ("$started_at", Format(run.StartedAt)),
            ("$completed_at", run.CompletedAt is null ? null : Format(run.CompletedAt.Value)),
            ("$explanation", run.Explanation),
            ("$evidence_json", Json(run.Evidence)));
    }

    public async Task<CanonicalWorkflowPersistenceSnapshot> LoadSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return new CanonicalWorkflowPersistenceSnapshot([], [], [], [], [], [], [], [], [], []);
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);

        return new CanonicalWorkflowPersistenceSnapshot(
            await ReadWorkflowStatesAsync(connection, cancellationToken),
            await ReadStageStatesAsync(connection, cancellationToken),
            await ReadTransitionRunsAsync(connection, cancellationToken),
            await ReadTransitionEvidenceAsync(connection, cancellationToken),
            await ReadProductsAsync(connection, cancellationToken),
            await ReadGateEvaluationsAsync(connection, cancellationToken),
            await ReadEffectRecordsAsync(connection, cancellationToken),
            await ReadBlockersAsync(connection, cancellationToken),
            await ReadRecoveryMarkersAsync(connection, cancellationToken),
            await ReadWorkflowChainRunsAsync(connection, cancellationToken));
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO workspace_metadata (key, value)
            VALUES ('persistence_state', 'canonical')
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """,
            cancellationToken);
        return connection;
    }

    private static async Task<IReadOnlyList<CanonicalWorkflowStateRecord>> ReadWorkflowStatesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CanonicalWorkflowStateRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT workflow_identity, state, current_stage, outcome, updated_at, evidence_json
            FROM canonical_workflow_states ORDER BY workflow_identity;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CanonicalWorkflowStateRecord(
                new WorkflowIdentity(reader.GetString(0)),
                ParseEnum<WorkflowResolutionState>(reader.GetString(1)),
                reader.IsDBNull(2) ? null : new WorkflowStageIdentity(reader.GetString(2)),
                reader.IsDBNull(3) ? null : ParseEnum<RuntimeOutcomeKind>(reader.GetString(3)),
                ParseDate(reader.GetString(4)),
                ReadJson<IReadOnlyList<string>>(reader.GetString(5))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CanonicalStageStateRecord>> ReadStageStatesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CanonicalStageStateRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT workflow_identity, stage_identity, state, updated_at, evidence_json
            FROM canonical_stage_states ORDER BY workflow_identity, stage_identity;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CanonicalStageStateRecord(
                new WorkflowIdentity(reader.GetString(0)),
                new WorkflowStageIdentity(reader.GetString(1)),
                ParseEnum<WorkflowResolutionState>(reader.GetString(2)),
                ParseDate(reader.GetString(3)),
                ReadJson<IReadOnlyList<string>>(reader.GetString(4))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CanonicalTransitionRunRecord>> ReadTransitionRunsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CanonicalTransitionRunRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT run_id, workflow_identity, stage_identity, transition_identity, state, outcome,
                   started_at, completed_at, input_snapshot_hash, explanation, evidence_json
            FROM canonical_transition_runs ORDER BY started_at, run_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CanonicalTransitionRunRecord(
                reader.GetString(0),
                new WorkflowIdentity(reader.GetString(1)),
                new WorkflowStageIdentity(reader.GetString(2)),
                new WorkflowTransitionIdentity(reader.GetString(3)),
                ParseEnum<TransitionDurableState>(reader.GetString(4)),
                ParseEnum<RuntimeOutcomeKind>(reader.GetString(5)),
                ParseDate(reader.GetString(6)),
                reader.IsDBNull(7) ? null : ParseDate(reader.GetString(7)),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9),
                ReadJson<IReadOnlyList<string>>(reader.GetString(10))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CanonicalTransitionEvidenceRecord>> ReadTransitionEvidenceAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CanonicalTransitionEvidenceRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT evidence_id, run_id, transition_identity, event_name, recorded_at,
                   state, explanation, evidence_json, document_json
            FROM canonical_transition_evidence ORDER BY evidence_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CanonicalTransitionEvidenceRecord(
                reader.GetInt64(0),
                reader.GetString(1),
                new WorkflowTransitionIdentity(reader.GetString(2)),
                reader.GetString(3),
                ParseDate(reader.GetString(4)),
                ParseEnum<TransitionDurableState>(reader.GetString(5)),
                reader.GetString(6),
                ReadJson<IReadOnlyList<string>>(reader.GetString(7)),
                reader.GetString(8)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ProductRecord>> ReadProductsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<ProductRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT product_identity, producer_workflow, producer_transition, intended_consumers_json,
                   repository_ownership, authority, storage_representations_json, causal_identity,
                   freshness, validation_state, lifecycle, evidence_locations_json
            FROM canonical_product_records ORDER BY product_identity;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ProductRecord(
                new ProductIdentity(reader.GetString(0)),
                new WorkflowIdentity(reader.GetString(1)),
                new WorkflowTransitionIdentity(reader.GetString(2)),
                ReadJson<IReadOnlyList<string>>(reader.GetString(3)).Select(value => new WorkflowIdentity(value)).ToArray(),
                reader.GetString(4),
                reader.GetString(5),
                ReadJson<IReadOnlyList<string>>(reader.GetString(6)),
                reader.GetString(7),
                ParseEnum<ProductFreshness>(reader.GetString(8)),
                ParseEnum<ProductValidationState>(reader.GetString(9)),
                ParseEnum<ProductLifecycle>(reader.GetString(10)),
                ReadJson<IReadOnlyList<string>>(reader.GetString(11))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CanonicalGateEvaluationRecord>> ReadGateEvaluationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CanonicalGateEvaluationRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT evaluation_id, workflow_identity, stage_identity, transition_identity,
                   gate_identity, status, evaluated_at, requirements_json, explanation, evidence_json
            FROM canonical_gate_evaluations ORDER BY evaluation_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CanonicalGateEvaluationRecord(
                reader.GetInt64(0),
                new WorkflowIdentity(reader.GetString(1)),
                reader.IsDBNull(2) ? null : new WorkflowStageIdentity(reader.GetString(2)),
                reader.IsDBNull(3) ? null : new WorkflowTransitionIdentity(reader.GetString(3)),
                new GateIdentity(reader.GetString(4)),
                ParseEnum<GateStatus>(reader.GetString(5)),
                ParseDate(reader.GetString(6)),
                ReadJson<IReadOnlyList<GateRequirementResult>>(reader.GetString(7)),
                reader.GetString(8),
                ReadJson<IReadOnlyList<string>>(reader.GetString(9))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CanonicalEffectRecord>> ReadEffectRecordsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CanonicalEffectRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT record_id, run_id, effect_identity, category, status, recorded_at, explanation, evidence_json
            FROM canonical_effect_records ORDER BY record_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CanonicalEffectRecord(
                reader.GetInt64(0),
                reader.GetString(1),
                new EffectIdentity(reader.GetString(2)),
                ParseEnum<EffectCategory>(reader.GetString(3)),
                ParseEnum<EffectExecutionStatus>(reader.GetString(4)),
                ParseDate(reader.GetString(5)),
                reader.GetString(6),
                ReadJson<IReadOnlyList<string>>(reader.GetString(7))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CanonicalBlockerRecord>> ReadBlockersAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CanonicalBlockerRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT blocker_id, workflow_identity, stage_identity, transition_identity, category,
                   reason, authority, required_action, recoverable, evidence_json, created_at, resolved_at
            FROM canonical_blockers ORDER BY blocker_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CanonicalBlockerRecord(
                reader.GetString(0),
                new WorkflowIdentity(reader.GetString(1)),
                reader.IsDBNull(2) ? null : new WorkflowStageIdentity(reader.GetString(2)),
                reader.IsDBNull(3) ? null : new WorkflowTransitionIdentity(reader.GetString(3)),
                new ResolutionBlocker(
                    ParseEnum<BlockerCategory>(reader.GetString(4)),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetInt64(8) == 1,
                    ReadJson<IReadOnlyList<string>>(reader.GetString(9))),
                ParseDate(reader.GetString(10)),
                reader.IsDBNull(11) ? null : ParseDate(reader.GetString(11))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CanonicalRecoveryMarkerRecord>> ReadRecoveryMarkersAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CanonicalRecoveryMarkerRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT marker_id, workflow_identity, stage_identity, transition_identity, semantics,
                   supported_actions_json, unsupported_actions_json, evidence_json, recorded_at
            FROM canonical_recovery_markers ORDER BY marker_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CanonicalRecoveryMarkerRecord(
                reader.GetString(0),
                new WorkflowIdentity(reader.GetString(1)),
                reader.IsDBNull(2) ? null : new WorkflowStageIdentity(reader.GetString(2)),
                reader.IsDBNull(3) ? null : new WorkflowTransitionIdentity(reader.GetString(3)),
                new RecoveryDefinition(
                    reader.GetString(0),
                    reader.GetString(4),
                    ReadJson<IReadOnlyList<string>>(reader.GetString(5)),
                    ReadJson<IReadOnlyList<string>>(reader.GetString(6))),
                ReadJson<IReadOnlyList<string>>(reader.GetString(7)),
                ParseDate(reader.GetString(8))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CanonicalWorkflowChainRunRecord>> ReadWorkflowChainRunsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CanonicalWorkflowChainRunRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT chain_run_id, chain_identity, current_workflow, status,
                   started_at, completed_at, explanation, evidence_json
            FROM canonical_workflow_chain_runs ORDER BY started_at, chain_run_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CanonicalWorkflowChainRunRecord(
                reader.GetString(0),
                reader.GetString(1),
                new WorkflowIdentity(reader.GetString(2)),
                ParseEnum<RuntimeOutcomeKind>(reader.GetString(3)),
                ParseDate(reader.GetString(4)),
                reader.IsDBNull(5) ? null : ParseDate(reader.GetString(5)),
                reader.GetString(6),
                ReadJson<IReadOnlyList<string>>(reader.GetString(7))));
        }

        return rows;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Json<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static T ReadJson<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions) ??
            throw new JsonException($"Could not deserialize {typeof(T).Name}.");

    private static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct =>
        Enum.TryParse(value, ignoreCase: false, out TEnum parsed)
            ? parsed
            : throw new InvalidOperationException($"Invalid {typeof(TEnum).Name} value `{value}`.");

    private static string Format(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
