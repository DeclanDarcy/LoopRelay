using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Identity;
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

    /// <summary>
    /// Authoritative post-validation transaction: promotes products, records the output gate and
    /// lifecycle fact, advances current-state projections, completes the attempt, and enqueues
    /// required effect intents atomically.
    /// </summary>
    public async Task CommitTransitionAsync(
        TransitionCommitCapture capture,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            foreach (ProductRecord product in capture.Validation.Products)
            {
                await ExecuteAsync(connection, transaction,
                    """
                    INSERT INTO canonical_product_records (
                        product_identity, producer_workflow, producer_transition, intended_consumers_json,
                        repository_ownership, authority, storage_representations_json, causal_identity,
                        freshness, validation_state, lifecycle, evidence_locations_json, updated_at, schema_version
                    ) VALUES (
                        $product, $workflow, $transition, $consumers, $ownership, $authority,
                        $representations, $causal, $freshness, $validation, $lifecycle, $evidence, $updated, $schema
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
                        updated_at = excluded.updated_at,
                        schema_version = excluded.schema_version;
                    """,
                    cancellationToken,
                    ("$product", product.Identity.Value),
                    ("$workflow", product.ProducerWorkflow.Value),
                    ("$transition", product.ProducerTransition.Value),
                    ("$consumers", Json(product.IntendedConsumers.Select(value => value.Value).ToArray())),
                    ("$ownership", product.RepositoryOwnership),
                    ("$authority", product.Authority),
                    ("$representations", Json(product.StorageRepresentations)),
                    ("$causal", product.CausalIdentity),
                    ("$freshness", product.Freshness.ToString()),
                    ("$validation", product.ValidationState.ToString()),
                    ("$lifecycle", product.Lifecycle.ToString()),
                    ("$evidence", Json(product.EvidenceLocations)),
                    ("$updated", Format(capture.CommittedAt)),
                    ("$schema", product.SchemaVersion));
            }

            await ExecuteAsync(connection, transaction,
                """
                INSERT INTO canonical_gate_evaluations (
                    workflow_identity, stage_identity, transition_identity, gate_identity, status,
                    evaluated_at, requirements_json, explanation, evidence_json, transition_run_id
                ) VALUES ($workflow, $stage, $transition, $gate, $status, $at, $requirements,
                          $explanation, $evidence, $run);
                """,
                cancellationToken,
                ("$workflow", capture.Request.Workflow.Value),
                ("$stage", capture.Request.Stage.Value),
                ("$transition", capture.Definition.Identity.Value),
                ("$gate", capture.Definition.OutputGate.Identity.Value),
                ("$status", capture.OutputGate.Status.ToString()),
                ("$at", Format(capture.CommittedAt)),
                ("$requirements", Json(capture.OutputGate.Requirements)),
                ("$explanation", capture.OutputGate.Explanation),
                ("$evidence", Json(capture.OutputGate.Evidence)),
                ("$run", capture.Causality.TransitionRun.Value));

            bool effectsPending = capture.Definition.Effects.Count > 0;
            string durableState = effectsPending
                ? TransitionDurableState.EffectsPending.ToString()
                : TransitionDurableState.Completed.ToString();
            string outcome = effectsPending
                ? RuntimeOutcomeKind.EffectsPending.ToString()
                : RuntimeOutcomeKind.Completed.ToString();
            string explanation = effectsPending
                ? "Attempt completed and required effect intents were enqueued."
                : "Transition completed without required external effects.";
            await ExecuteAsync(connection, transaction,
                """
                UPDATE canonical_transition_runs
                SET state = $state, outcome = $outcome, completed_at = $at,
                    explanation = $explanation, evidence_json = $evidence
                WHERE run_id = $run;
                UPDATE attempts
                SET completed_at = $at, outcome = $outcome
                WHERE attempt_id = $attempt;
                INSERT INTO canonical_transition_evidence (
                    run_id, transition_identity, event_name, recorded_at, state,
                    explanation, evidence_json, document_json
                ) VALUES ($run, $transition, 'TransitionStateCommitted', $at, $state,
                          $explanation, $evidence, $document);
                INSERT INTO canonical_workflow_states (
                    workflow_identity, state, current_stage, outcome, updated_at, evidence_json
                ) VALUES ($workflow, 'Active', $stage, $outcome, $at, $evidence)
                ON CONFLICT(workflow_identity) DO UPDATE SET
                    state = excluded.state, current_stage = excluded.current_stage,
                    outcome = excluded.outcome, updated_at = excluded.updated_at,
                    evidence_json = excluded.evidence_json;
                INSERT INTO canonical_stage_states (
                    workflow_identity, stage_identity, state, updated_at, evidence_json
                ) VALUES ($workflow, $stage, 'Active', $at, $evidence)
                ON CONFLICT(workflow_identity, stage_identity) DO UPDATE SET
                    state = excluded.state, updated_at = excluded.updated_at,
                    evidence_json = excluded.evidence_json;
                """,
                cancellationToken,
                ("$state", durableState),
                ("$outcome", outcome),
                ("$at", Format(capture.CommittedAt)),
                ("$explanation", explanation),
                ("$evidence", Json(capture.Validation.Evidence.Concat(capture.OutputGate.Evidence).ToArray())),
                ("$document", Json(capture)),
                ("$run", capture.Causality.TransitionRun.Value),
                ("$attempt", capture.Causality.Attempt.Value),
                ("$transition", capture.Definition.Identity.Value),
                ("$workflow", capture.Request.Workflow.Value),
                ("$stage", capture.Request.Stage.Value));

            foreach (EffectDefinition effect in capture.Definition.Effects.OrderBy(effect => effect.Order))
            {
                string intentId = CausalUlid.NewId("effectintent");
                string idempotencyKey =
                    $"transition-effect:{capture.Causality.TransitionRun.Value}:{effect.Identity.Value}";
                await ExecuteAsync(connection, transaction,
                    """
                    INSERT INTO canonical_effect_intents (
                        effect_intent_id, transition_run_id, attempt_id, effect_identity, category,
                        effect_order, idempotency_key, status, definition_json, planned_at
                    ) VALUES ($intent, $run, $attempt, $effect, $category, $order, $key,
                              'Planned', $definition, $at)
                    ON CONFLICT(idempotency_key) DO NOTHING;
                    INSERT INTO canonical_effect_records (
                        run_id, effect_identity, category, status, recorded_at, explanation, evidence_json
                    ) VALUES ($run, $effect, $category, 'Planned', $at,
                              'Required effect intent enqueued.', $effect_evidence);
                    """,
                    cancellationToken,
                    ("$intent", intentId),
                    ("$run", capture.Causality.TransitionRun.Value),
                    ("$attempt", capture.Causality.Attempt.Value),
                    ("$effect", effect.Identity.Value),
                    ("$category", effect.Category.ToString()),
                    ("$order", effect.Order),
                    ("$key", idempotencyKey),
                    ("$definition", Json(effect)),
                    ("$at", Format(capture.CommittedAt)),
                    ("$effect_evidence", Json(new[] { effect.Trigger })));
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task AppendRecoveryPlanAsync(
        TransitionRecoveryPlan plan,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO transition_recovery_plans (
                recovery_id, transition_run_id, source_attempt_id, classification, action,
                resulting_attempt_mode, next_attempt_index, evidence_json, preconditions_json, planned_at
            ) VALUES ($recovery, $run, $attempt, $classification, $action, $mode, $index,
                      $evidence, $preconditions, $at);
            """,
            cancellationToken,
            ("$recovery", plan.RecoveryIdentity.Value),
            ("$run", plan.SourceCausality.TransitionRun.Value),
            ("$attempt", plan.SourceCausality.Attempt.Value),
            ("$classification", plan.Classification),
            ("$action", plan.Action.ToString()),
            ("$mode", plan.ResultingAttemptMode.ToString()),
            ("$index", plan.NextAttemptIndex),
            ("$evidence", Json(plan.Evidence)),
            ("$preconditions", Json(plan.Preconditions)),
            ("$at", Format(DateTimeOffset.UtcNow)));
    }

    public async Task RecordEffectIntentStateAsync(
        TransitionRunIdentity transitionRun,
        EffectIdentity effect,
        EffectExecutionStatus status,
        string? failure,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        string now = Format(DateTimeOffset.UtcNow);
        await ExecuteAsync(
            connection,
            """
            UPDATE canonical_effect_intents
            SET status = $status,
                started_at = CASE WHEN $status = 'Started' THEN $now ELSE started_at END,
                completed_at = CASE WHEN $status IN ('Succeeded', 'Failed', 'PartiallyFailed') THEN $now ELSE completed_at END,
                failure = $failure
            WHERE transition_run_id = $run AND effect_identity = $effect;
            INSERT INTO canonical_effect_records (
                run_id, effect_identity, category, status, recorded_at, explanation, evidence_json
            )
            SELECT transition_run_id, effect_identity, category, $status, $now,
                   COALESCE($failure, 'Effect state updated.'), '[]'
            FROM canonical_effect_intents
            WHERE transition_run_id = $run AND effect_identity = $effect;
            UPDATE canonical_transition_runs
            SET state = CASE
                    WHEN $status IN ('Unknown', 'PartiallyFailed') THEN 'EffectsPartiallyApplied'
                    WHEN $status = 'Failed' THEN 'Failed'
                    ELSE state
                END,
                outcome = CASE
                    WHEN $status IN ('Unknown', 'PartiallyFailed') THEN 'RecoveryRequired'
                    WHEN $status = 'Failed' THEN 'Failed'
                    ELSE outcome
                END,
                completed_at = CASE
                    WHEN $status IN ('PartiallyFailed', 'Failed') THEN $now
                    ELSE completed_at
                END,
                explanation = CASE
                    WHEN $status IN ('Unknown', 'PartiallyFailed')
                        THEN 'An effect outcome is uncertain and requires reconciliation.'
                    WHEN $status = 'Failed' THEN COALESCE($failure, 'A required effect failed.')
                    ELSE explanation
                END
            WHERE run_id = $run
              AND $status IN ('Unknown', 'PartiallyFailed', 'Failed');
            UPDATE canonical_transition_runs
            SET state = 'Completed',
                outcome = 'Completed',
                completed_at = $now,
                explanation = 'All required effect intents completed.'
            WHERE run_id = $run
              AND $status = 'Succeeded'
              AND NOT EXISTS (
                  SELECT 1 FROM canonical_effect_intents
                  WHERE transition_run_id = $run AND status <> 'Succeeded'
              );
            UPDATE attempts
            SET outcome = 'Completed', completed_at = COALESCE(completed_at, $now)
            WHERE transition_run_id = $run
              AND $status = 'Succeeded'
              AND NOT EXISTS (
                  SELECT 1 FROM canonical_effect_intents
                  WHERE transition_run_id = $run AND status <> 'Succeeded'
              );
            """,
            cancellationToken,
            ("$status", status.ToString()),
            ("$now", now),
            ("$failure", failure),
            ("$run", transitionRun.Value),
            ("$effect", effect.Value));
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
                freshness, validation_state, lifecycle, evidence_locations_json, updated_at,
                schema_version
            )
            VALUES (
                $product_identity, $producer_workflow, $producer_transition, $intended_consumers_json,
                $repository_ownership, $authority, $storage_representations_json, $causal_identity,
                $freshness, $validation_state, $lifecycle, $evidence_locations_json, $updated_at,
                $schema_version
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
                updated_at = excluded.updated_at,
                schema_version = excluded.schema_version;
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
            ("$updated_at", Format(DateTimeOffset.UtcNow)),
            ("$schema_version", product.SchemaVersion));
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
                evaluated_at, requirements_json, explanation, evidence_json, transition_run_id
            )
            VALUES (
                $workflow_identity, $stage_identity, $transition_identity, $gate_identity, $status,
                $evaluated_at, $requirements_json, $explanation, $evidence_json, $transition_run_id
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
            ("$evidence_json", Json(evaluation.Evidence)),
            ("$transition_run_id", evaluation.TransitionRunId));
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

    public async Task AppendWarningAsync(
        CanonicalWarningRecord warning,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO evaluation_warnings (
                warning_id, workflow_identity, stage_identity, transition_identity, category,
                concern, authority, remediation, evidence_json, created_at, transition_run_id
            )
            VALUES (
                $warning_id, $workflow_identity, $stage_identity, $transition_identity, $category,
                $concern, $authority, $remediation, $evidence_json, $created_at, $transition_run_id
            );
            """,
            cancellationToken,
            ("$warning_id", warning.WarningId),
            ("$workflow_identity", warning.Workflow.Value),
            ("$stage_identity", warning.Stage?.Value),
            ("$transition_identity", warning.Transition?.Value),
            ("$category", warning.Category.ToString()),
            ("$concern", warning.Concern),
            ("$authority", warning.Authority),
            ("$remediation", warning.Remediation),
            ("$evidence_json", Json(warning.Evidence)),
            ("$created_at", Format(warning.CreatedAt)),
            ("$transition_run_id", warning.TransitionRunId));
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

    public async Task AppendChainBoundaryEventAsync(
        CanonicalChainBoundaryEventRecord boundary,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_chain_boundary_events (
                boundary_id, run_id, chain_identity, source_workflow, target_workflow,
                exit_gate_status, entry_gate_status, transfer_gate_status, decision,
                explanation, evidence_json, boundary_json, recorded_at
            )
            VALUES (
                $boundary_id, $run_id, $chain_identity, $source_workflow, $target_workflow,
                $exit_gate_status, $entry_gate_status, $transfer_gate_status, $decision,
                $explanation, $evidence_json, $boundary_json, $recorded_at
            );
            """,
            cancellationToken,
            ("$boundary_id", boundary.BoundaryId),
            ("$run_id", boundary.RunId),
            ("$chain_identity", boundary.ChainIdentity),
            ("$source_workflow", boundary.SourceWorkflow.Value),
            ("$target_workflow", boundary.TargetWorkflow?.Value),
            ("$exit_gate_status", boundary.ExitGateStatus.ToString()),
            ("$entry_gate_status", boundary.EntryGateStatus?.ToString()),
            ("$transfer_gate_status", boundary.TransferGateStatus?.ToString()),
            ("$decision", boundary.Decision),
            ("$explanation", boundary.Explanation),
            ("$evidence_json", Json(boundary.Evidence)),
            ("$boundary_json", boundary.BoundaryJson),
            ("$recorded_at", Format(boundary.RecordedAt)));
    }

    public async Task<IReadOnlyList<CanonicalChainBoundaryEventRecord>> ReadChainBoundaryEventsAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        return await ReadSpineRowsOrEmptyAsync(async () =>
        {
            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
            await connection.OpenAsync(cancellationToken);

            var rows = new List<CanonicalChainBoundaryEventRecord>();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT boundary_id, run_id, chain_identity, source_workflow, target_workflow,
                       exit_gate_status, entry_gate_status, transfer_gate_status, decision,
                       explanation, evidence_json, boundary_json, recorded_at
                FROM canonical_chain_boundary_events ORDER BY rowid;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new CanonicalChainBoundaryEventRecord(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.GetString(2),
                    new WorkflowIdentity(reader.GetString(3)),
                    reader.IsDBNull(4) ? null : new WorkflowIdentity(reader.GetString(4)),
                    ParseEnum<GateStatus>(reader.GetString(5)),
                    reader.IsDBNull(6) ? null : ParseEnum<GateStatus>(reader.GetString(6)),
                    reader.IsDBNull(7) ? null : ParseEnum<GateStatus>(reader.GetString(7)),
                    reader.GetString(8),
                    reader.GetString(9),
                    ReadJson<IReadOnlyList<string>>(reader.GetString(10)),
                    reader.GetString(11),
                    ParseDate(reader.GetString(12))));
            }

            return rows;
        });
    }

    public async Task AppendPolicyResolutionAsync(
        CanonicalPolicyResolutionRecord resolution,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_policy_resolutions (
                resolution_id, policy_id, schema_version, resolved_json, provenance_json,
                source_description, recorded_at
            )
            VALUES (
                $resolution_id, $policy_id, $schema_version, $resolved_json, $provenance_json,
                $source_description, $recorded_at
            );
            """,
            cancellationToken,
            ("$resolution_id", resolution.ResolutionId),
            ("$policy_id", resolution.PolicyId),
            ("$schema_version", resolution.SchemaVersion),
            ("$resolved_json", resolution.ResolvedJson),
            ("$provenance_json", resolution.ProvenanceJson),
            ("$source_description", resolution.SourceDescription),
            ("$recorded_at", Format(resolution.RecordedAt)));
    }

    public async Task<IReadOnlyList<CanonicalPolicyResolutionRecord>> ReadPolicyResolutionsAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        return await ReadSpineRowsOrEmptyAsync(async () =>
        {
            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
            await connection.OpenAsync(cancellationToken);

            var rows = new List<CanonicalPolicyResolutionRecord>();
            await using SqliteCommand command = connection.CreateCommand();
            // Ledger sequence (insertion order) is the ordering authority for appended facts.
            command.CommandText = """
                SELECT resolution_id, policy_id, schema_version, resolved_json, provenance_json,
                       source_description, recorded_at
                FROM canonical_policy_resolutions ORDER BY rowid;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new CanonicalPolicyResolutionRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    ParseDate(reader.GetString(6))));
            }

            return rows;
        });
    }

    public async Task AppendRenderedPromptAsync(
        CanonicalRenderedPromptRecord renderedPrompt,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_rendered_prompts (
                rendered_prompt_id, transition_run_id, attempt_id, session_id, turn_id,
                prompt_identity, template_source_hash, rendered_sha256, rendered_text,
                consumed_inputs_json, policy_id, rendered_at, persistence_id,
                prompt_policy_profile_id, consumed_input_manifest_id, rendered_encoding
            )
            VALUES (
                $rendered_prompt_id, $transition_run_id, $attempt_id, $session_id, $turn_id,
                $prompt_identity, $template_source_hash, $rendered_sha256, $rendered_text,
                $consumed_inputs_json, $policy_id, $rendered_at, $persistence_id,
                $prompt_policy_profile_id, $consumed_input_manifest_id, $rendered_encoding
            );
            """,
            cancellationToken,
            ("$rendered_prompt_id", renderedPrompt.RenderedPromptId),
            ("$transition_run_id", renderedPrompt.TransitionRunId),
            ("$attempt_id", renderedPrompt.AttemptId),
            ("$session_id", renderedPrompt.SessionId),
            ("$turn_id", renderedPrompt.TurnId),
            ("$prompt_identity", renderedPrompt.PromptIdentity),
            ("$template_source_hash", renderedPrompt.TemplateSourceHash),
            ("$rendered_sha256", renderedPrompt.RenderedSha256),
            ("$rendered_text", renderedPrompt.RenderedText),
            ("$consumed_inputs_json", Json(renderedPrompt.ConsumedInputs)),
            ("$policy_id", renderedPrompt.PolicyId),
            ("$rendered_at", Format(renderedPrompt.RenderedAt)),
            ("$persistence_id", renderedPrompt.PersistenceId),
            ("$prompt_policy_profile_id", renderedPrompt.PromptPolicyProfileId),
            ("$consumed_input_manifest_id", renderedPrompt.ConsumedInputManifestId),
            ("$rendered_encoding", renderedPrompt.RenderedEncoding));
    }

    public async Task<IReadOnlyList<CanonicalRenderedPromptRecord>> ReadRenderedPromptsAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        return await ReadSpineRowsOrEmptyAsync(async () =>
        {
            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
            await connection.OpenAsync(cancellationToken);

            var rows = new List<CanonicalRenderedPromptRecord>();
            await using SqliteCommand command = connection.CreateCommand();
            // Ledger sequence (insertion order) is the ordering authority for appended facts.
            command.CommandText = """
                SELECT rendered_prompt_id, transition_run_id, attempt_id, session_id, turn_id,
                       prompt_identity, template_source_hash, rendered_sha256, rendered_text,
                       consumed_inputs_json, policy_id, rendered_at, persistence_id,
                       prompt_policy_profile_id, consumed_input_manifest_id, rendered_encoding
                FROM canonical_rendered_prompts ORDER BY rowid;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new CanonicalRenderedPromptRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    ReadJson<List<CanonicalReadReceiptFile>>(reader.GetString(9)),
                    reader.IsDBNull(10) ? null : reader.GetString(10),
                    ParseDate(reader.GetString(11)),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(12) ? null : reader.GetString(12),
                    reader.IsDBNull(13) ? null : reader.GetString(13),
                    reader.IsDBNull(14) ? null : reader.GetString(14),
                    reader.IsDBNull(15) ? "utf-8" : reader.GetString(15)));
            }

            return rows;
        });
    }

    public async Task UpsertRunAsync(
        RunRecord run,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO runs (
                run_id, workspace_id, chain_identity, invocation_mode, status,
                started_at, completed_at, stop_reason, explanation
            )
            VALUES (
                $run_id, $workspace_id, $chain_identity, $invocation_mode, $status,
                $started_at, $completed_at, $stop_reason, $explanation
            )
            ON CONFLICT(run_id) DO UPDATE SET
                workspace_id = excluded.workspace_id,
                chain_identity = excluded.chain_identity,
                invocation_mode = excluded.invocation_mode,
                status = excluded.status,
                started_at = excluded.started_at,
                completed_at = excluded.completed_at,
                stop_reason = excluded.stop_reason,
                explanation = excluded.explanation;
            """,
            cancellationToken,
            ("$run_id", run.RunId),
            ("$workspace_id", run.WorkspaceId),
            ("$chain_identity", run.ChainIdentity),
            ("$invocation_mode", run.InvocationMode),
            ("$status", run.Status),
            ("$started_at", Format(run.StartedAt)),
            ("$completed_at", run.CompletedAt is null ? null : Format(run.CompletedAt.Value)),
            ("$stop_reason", run.StopReason),
            ("$explanation", run.Explanation));
    }

    public async Task InterruptLingeringActiveRunsAsync(
        string exceptRunId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        string completedAt = Format(DateTimeOffset.UtcNow);
        await ExecuteAsync(
            connection,
            """
            UPDATE runs SET
                status = 'Interrupted',
                completed_at = $completed_at,
                stop_reason = 'Interrupted'
            WHERE status = 'Active' AND run_id <> $except_run_id;
            """,
            cancellationToken,
            ("$completed_at", completedAt),
            ("$except_run_id", exceptRunId));
        await ExecuteAsync(
            connection,
            """
            UPDATE workflow_instances SET
                status = 'Interrupted',
                completed_at = $completed_at
            WHERE status = 'Active' AND run_id <> $except_run_id;
            """,
            cancellationToken,
            ("$completed_at", completedAt),
            ("$except_run_id", exceptRunId));
        await ExecuteAsync(
            connection,
            """
            UPDATE attempts SET
                outcome = 'Interrupted',
                completed_at = $completed_at
            WHERE completed_at IS NULL AND run_id <> $except_run_id;
            """,
            cancellationToken,
            ("$completed_at", completedAt),
            ("$except_run_id", exceptRunId));
    }

    public async Task UpsertWorkflowInstanceAsync(
        WorkflowInstanceRecord instance,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO workflow_instances (
                workflow_instance_id, run_id, workflow_identity, catalog_version, status,
                started_at, completed_at, outcome
            )
            VALUES (
                $workflow_instance_id, $run_id, $workflow_identity, $catalog_version, $status,
                $started_at, $completed_at, $outcome
            )
            ON CONFLICT(workflow_instance_id) DO UPDATE SET
                run_id = excluded.run_id,
                workflow_identity = excluded.workflow_identity,
                catalog_version = excluded.catalog_version,
                status = excluded.status,
                started_at = excluded.started_at,
                completed_at = excluded.completed_at,
                outcome = excluded.outcome;
            """,
            cancellationToken,
            ("$workflow_instance_id", instance.WorkflowInstanceId),
            ("$run_id", instance.RunId),
            ("$workflow_identity", instance.Workflow.Value),
            ("$catalog_version", instance.CatalogVersion),
            ("$status", instance.Status),
            ("$started_at", Format(instance.StartedAt)),
            ("$completed_at", instance.CompletedAt is null ? null : Format(instance.CompletedAt.Value)),
            ("$outcome", instance.Outcome));
    }

    public async Task CompleteWorkflowInstanceAsync(
        string workflowInstanceId,
        string status,
        string? outcome,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            UPDATE workflow_instances SET
                status = $status,
                completed_at = $completed_at,
                outcome = $outcome
            WHERE workflow_instance_id = $workflow_instance_id;
            """,
            cancellationToken,
            ("$status", status),
            ("$completed_at", Format(completedAt)),
            ("$outcome", outcome),
            ("$workflow_instance_id", workflowInstanceId));
    }

    public async Task UpsertAttemptAsync(
        AttemptRecord attempt,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO attempts (
                attempt_id, transition_run_id, workflow_instance_id, run_id, attempt_index,
                started_at, completed_at, outcome, policy_id
            )
            VALUES (
                $attempt_id, $transition_run_id, $workflow_instance_id, $run_id, $attempt_index,
                $started_at, $completed_at, $outcome, $policy_id
            )
            ON CONFLICT(attempt_id) DO UPDATE SET
                transition_run_id = excluded.transition_run_id,
                workflow_instance_id = excluded.workflow_instance_id,
                run_id = excluded.run_id,
                attempt_index = excluded.attempt_index,
                started_at = excluded.started_at,
                completed_at = excluded.completed_at,
                outcome = excluded.outcome,
                policy_id = excluded.policy_id;
            """,
            cancellationToken,
            ("$attempt_id", attempt.AttemptId),
            ("$transition_run_id", attempt.TransitionRunId),
            ("$workflow_instance_id", attempt.WorkflowInstanceId),
            ("$run_id", attempt.RunId),
            ("$attempt_index", attempt.AttemptIndex),
            ("$started_at", Format(attempt.StartedAt)),
            ("$completed_at", attempt.CompletedAt is null ? null : Format(attempt.CompletedAt.Value)),
            ("$outcome", attempt.Outcome),
            ("$policy_id", attempt.PolicyId));
    }

    public async Task CompleteAttemptAsync(
        string attemptId,
        DateTimeOffset completedAt,
        string outcome,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            UPDATE attempts SET
                completed_at = $completed_at,
                outcome = $outcome
            WHERE attempt_id = $attempt_id;
            """,
            cancellationToken,
            ("$completed_at", Format(completedAt)),
            ("$outcome", outcome),
            ("$attempt_id", attemptId));
    }

    public async Task UpsertAgentSessionAsync(
        AgentSessionRecord session,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO agent_sessions (
                session_id, attempt_id, workspace_id, provider, provider_thread_id,
                role, legacy_session_guid, started_at, completed_at, effort, sandbox
            )
            VALUES (
                $session_id, $attempt_id, $workspace_id, $provider, $provider_thread_id,
                $role, $legacy_session_guid, $started_at, $completed_at, $effort, $sandbox
            )
            ON CONFLICT(session_id) DO UPDATE SET
                attempt_id = excluded.attempt_id,
                workspace_id = excluded.workspace_id,
                provider = excluded.provider,
                provider_thread_id = excluded.provider_thread_id,
                role = excluded.role,
                legacy_session_guid = excluded.legacy_session_guid,
                started_at = excluded.started_at,
                completed_at = excluded.completed_at,
                effort = excluded.effort,
                sandbox = excluded.sandbox;
            """,
            cancellationToken,
            ("$session_id", session.SessionId),
            ("$attempt_id", session.AttemptId),
            ("$workspace_id", session.WorkspaceId),
            ("$provider", session.Provider),
            ("$provider_thread_id", session.ProviderThreadId),
            ("$role", session.Role),
            ("$legacy_session_guid", session.LegacySessionGuid),
            ("$started_at", Format(session.StartedAt)),
            ("$completed_at", session.CompletedAt is null ? null : Format(session.CompletedAt.Value)),
            ("$effort", session.Effort),
            ("$sandbox", session.Sandbox));
    }

    public async Task AppendAgentTurnAsync(
        AgentTurnRecord turn,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO agent_turns (
                turn_id, session_id, turn_index, recorded_at, state, prompt_sha256,
                prompt_tokens, output_tokens, cached_input_tokens, diagnostics_kind, diagnostics
            )
            VALUES (
                $turn_id, $session_id, $turn_index, $recorded_at, $state, $prompt_sha256,
                $prompt_tokens, $output_tokens, $cached_input_tokens, $diagnostics_kind, $diagnostics
            );
            """,
            cancellationToken,
            ("$turn_id", turn.TurnId),
            ("$session_id", turn.SessionId),
            ("$turn_index", turn.TurnIndex),
            ("$recorded_at", Format(turn.RecordedAt)),
            ("$state", turn.State),
            ("$prompt_sha256", turn.PromptSha256),
            ("$prompt_tokens", turn.PromptTokens),
            ("$output_tokens", turn.OutputTokens),
            ("$cached_input_tokens", turn.CachedInputTokens),
            ("$diagnostics_kind", turn.DiagnosticsKind),
            ("$diagnostics", turn.Diagnostics));
    }

    public async Task AppendRuntimePrerequisiteAsync(
        CanonicalRuntimePrerequisiteRecord record,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_runtime_prerequisites (
                prerequisite_check_id, run_id, checked_at, diagnostics_json
            )
            VALUES (
                $prerequisite_check_id, $run_id, $checked_at, $diagnostics_json
            );
            """,
            cancellationToken,
            ("$prerequisite_check_id", record.PrerequisiteCheckId),
            ("$run_id", record.RunId),
            ("$checked_at", Format(record.CheckedAt)),
            ("$diagnostics_json", record.DiagnosticsJson));
    }

    public async Task AppendReadReceiptAsync(
        CanonicalReadReceiptRecord receipt,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO read_receipts (
                receipt_id, run_id, workflow_identity, transition_identity, attempt_id,
                commit_hash, input_surfaces_json, surface_tree_hashes_json, files_json,
                products_json, validation, consumed_at, transition_run_id
            )
            VALUES (
                $receipt_id, $run_id, $workflow_identity, $transition_identity, $attempt_id,
                $commit_hash, $input_surfaces_json, $surface_tree_hashes_json, $files_json,
                $products_json, $validation, $consumed_at, $transition_run_id
            );
            """,
            cancellationToken,
            ("$receipt_id", receipt.ReceiptId),
            ("$run_id", receipt.RunId),
            ("$workflow_identity", receipt.WorkflowIdentity),
            ("$transition_identity", receipt.TransitionIdentity),
            ("$attempt_id", receipt.AttemptId),
            ("$commit_hash", receipt.CommitHash),
            ("$input_surfaces_json", Json(receipt.InputSurfaces)),
            ("$surface_tree_hashes_json", receipt.SurfaceTreeHashes is null ? null : Json(receipt.SurfaceTreeHashes)),
            ("$files_json", Json(receipt.Files)),
            ("$products_json", Json(receipt.Products)),
            ("$validation", receipt.Validation),
            ("$consumed_at", Format(receipt.ConsumedAt)),
            ("$transition_run_id", receipt.TransitionRunId));
    }

    public async Task<CanonicalWorkflowPersistenceSnapshot> LoadSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return new CanonicalWorkflowPersistenceSnapshot([], [], [], [], [], [], [], [], []);
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
            await ReadWarningsAsync(connection, cancellationToken),
            await ReadRecoveryMarkersAsync(connection, cancellationToken));
    }

    public async Task<IReadOnlyList<RunRecord>> ReadRunsAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        return await ReadSpineRowsOrEmptyAsync(async () =>
        {
            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
            await connection.OpenAsync(cancellationToken);

            var rows = new List<RunRecord>();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT run_id, workspace_id, chain_identity, invocation_mode, status,
                       started_at, completed_at, stop_reason, explanation
                FROM runs ORDER BY started_at, run_id;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new RunRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    ParseDate(reader.GetString(5)),
                    reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6)),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.GetString(8)));
            }

            return rows;
        });
    }

    public async Task<IReadOnlyList<WorkflowInstanceRecord>> ReadWorkflowInstancesAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        return await ReadSpineRowsOrEmptyAsync(async () =>
        {
            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
            await connection.OpenAsync(cancellationToken);

            var rows = new List<WorkflowInstanceRecord>();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT workflow_instance_id, run_id, workflow_identity, catalog_version, status,
                       started_at, completed_at, outcome
                FROM workflow_instances ORDER BY started_at, workflow_instance_id;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new WorkflowInstanceRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    new WorkflowIdentity(reader.GetString(2)),
                    reader.GetString(3),
                    reader.GetString(4),
                    ParseDate(reader.GetString(5)),
                    reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6)),
                    reader.IsDBNull(7) ? null : reader.GetString(7)));
            }

            return rows;
        });
    }

    public async Task<IReadOnlyList<AttemptRecord>> ReadAttemptsAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        return await ReadSpineRowsOrEmptyAsync(async () =>
        {
            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
            await connection.OpenAsync(cancellationToken);

            // Pre-v7 databases opened read-only have no policy_id column; those attempts read
            // back with a null policy identity without migrating the database.
            bool hasPolicyId = await ColumnExistsAsync(
                connection, "attempts", "policy_id", cancellationToken);

            var rows = new List<AttemptRecord>();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = hasPolicyId
                ? """
                  SELECT attempt_id, transition_run_id, workflow_instance_id, run_id, attempt_index,
                         started_at, completed_at, outcome, policy_id
                  FROM attempts ORDER BY started_at, attempt_id;
                  """
                : """
                  SELECT attempt_id, transition_run_id, workflow_instance_id, run_id, attempt_index,
                         started_at, completed_at, outcome, NULL AS policy_id
                  FROM attempts ORDER BY started_at, attempt_id;
                  """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new AttemptRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    ParseDate(reader.GetString(5)),
                    reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6)),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8)));
            }

            return rows;
        });
    }

    public async Task<IReadOnlyList<AgentSessionRecord>> ReadAgentSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        return await ReadSpineRowsOrEmptyAsync(async () =>
        {
            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
            await connection.OpenAsync(cancellationToken);

            // Pre-v8 databases opened read-only have no effort/sandbox columns; those sessions
            // read back with null profile evidence without migrating the database.
            bool hasSessionProfiles = await ColumnExistsAsync(
                connection, "agent_sessions", "effort", cancellationToken);

            var rows = new List<AgentSessionRecord>();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = hasSessionProfiles
                ? """
                  SELECT session_id, attempt_id, workspace_id, provider, provider_thread_id,
                         role, legacy_session_guid, started_at, completed_at, effort, sandbox
                  FROM agent_sessions ORDER BY started_at, session_id;
                  """
                : """
                  SELECT session_id, attempt_id, workspace_id, provider, provider_thread_id,
                         role, legacy_session_guid, started_at, completed_at,
                         NULL AS effort, NULL AS sandbox
                  FROM agent_sessions ORDER BY started_at, session_id;
                  """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new AgentSessionRecord(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    ParseDate(reader.GetString(7)),
                    reader.IsDBNull(8) ? null : ParseDate(reader.GetString(8)),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10)));
            }

            return rows;
        });
    }

    public async Task<IReadOnlyList<AgentTurnRecord>> ReadAgentTurnsAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        return await ReadSpineRowsOrEmptyAsync(async () =>
        {
            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
            await connection.OpenAsync(cancellationToken);

            // Pre-v9 databases opened read-only have no turn evidence columns; those turns read
            // back with null state/usage/diagnosis evidence without migrating the database.
            bool hasTurnEvidence = await ColumnExistsAsync(
                connection, "agent_turns", "state", cancellationToken);

            var rows = new List<AgentTurnRecord>();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = hasTurnEvidence
                ? """
                  SELECT turn_id, session_id, turn_index, recorded_at, state, prompt_sha256,
                         prompt_tokens, output_tokens, cached_input_tokens, diagnostics_kind, diagnostics
                  FROM agent_turns ORDER BY rowid;
                  """
                : """
                  SELECT turn_id, session_id, turn_index, recorded_at, NULL AS state, NULL AS prompt_sha256,
                         NULL AS prompt_tokens, NULL AS output_tokens, NULL AS cached_input_tokens,
                         NULL AS diagnostics_kind, NULL AS diagnostics
                  FROM agent_turns ORDER BY rowid;
                  """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new AgentTurnRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    ParseDate(reader.GetString(3)),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetInt64(6),
                    reader.IsDBNull(7) ? null : reader.GetInt64(7),
                    reader.IsDBNull(8) ? null : reader.GetInt64(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10)));
            }

            return rows;
        });
    }

    public async Task<IReadOnlyList<CanonicalRuntimePrerequisiteRecord>> ReadRuntimePrerequisitesAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        return await ReadSpineRowsOrEmptyAsync(async () =>
        {
            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
            await connection.OpenAsync(cancellationToken);

            var rows = new List<CanonicalRuntimePrerequisiteRecord>();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT prerequisite_check_id, run_id, checked_at, diagnostics_json
                FROM canonical_runtime_prerequisites ORDER BY rowid;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new CanonicalRuntimePrerequisiteRecord(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    ParseDate(reader.GetString(2)),
                    reader.GetString(3)));
            }

            return rows;
        });
    }

    public async Task<IReadOnlyList<CanonicalReadReceiptRecord>> ReadReadReceiptsAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        return await ReadSpineRowsOrEmptyAsync(async () =>
        {
            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
            await connection.OpenAsync(cancellationToken);

            // Ledger sequence (insertion order) is the ordering authority for appended facts;
            // ULIDs are not monotonic within a millisecond and wall-clock is display metadata.
            bool hasTransitionRunId = await ColumnExistsAsync(
                connection,
                "read_receipts",
                "transition_run_id",
                cancellationToken);
            List<CanonicalReadReceiptRecord> rows = [];
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = hasTransitionRunId
                ? """
                  SELECT receipt_id, run_id, workflow_identity, transition_identity, attempt_id,
                         commit_hash, input_surfaces_json, surface_tree_hashes_json, files_json,
                         products_json, validation, consumed_at, transition_run_id
                  FROM read_receipts ORDER BY rowid;
                  """
                : """
                  SELECT receipt_id, run_id, workflow_identity, transition_identity, attempt_id,
                         commit_hash, input_surfaces_json, surface_tree_hashes_json, files_json,
                         products_json, validation, consumed_at
                  FROM read_receipts ORDER BY rowid;
                  """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new CanonicalReadReceiptRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    ReadJson<IReadOnlyList<string>>(reader.GetString(6)),
                    reader.IsDBNull(7) ? null : ReadJson<IReadOnlyDictionary<string, string?>>(reader.GetString(7)),
                    ReadJson<IReadOnlyList<CanonicalReadReceiptFile>>(reader.GetString(8)),
                    ReadJson<IReadOnlyList<CanonicalReadReceiptProduct>>(reader.GetString(9)),
                    reader.GetString(10),
                    ParseDate(reader.GetString(11)),
                    hasTransitionRunId && !reader.IsDBNull(12) ? reader.GetString(12) : null));
            }

            return rows;
        });
    }

    public async Task<string> ReadWorkspaceIdentityAsync(
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        return await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection, cancellationToken);
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
        // The schema_version column arrived with schema v5; snapshots open read-only without
        // migrating, so a pre-v5 database is read with the column defaulted rather than crashing.
        bool hasSchemaVersion = await ColumnExistsAsync(
            connection,
            "canonical_product_records",
            "schema_version",
            cancellationToken);
        var rows = new List<ProductRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = hasSchemaVersion
            ? """
              SELECT product_identity, producer_workflow, producer_transition, intended_consumers_json,
                     repository_ownership, authority, storage_representations_json, causal_identity,
                     freshness, validation_state, lifecycle, evidence_locations_json, schema_version
              FROM canonical_product_records ORDER BY product_identity;
              """
            : """
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
                ReadJson<IReadOnlyList<string>>(reader.GetString(11)),
                hasSchemaVersion ? reader.GetString(12) : "1"));
        }

        return rows;
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $column;";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<IReadOnlyList<CanonicalGateEvaluationRecord>> ReadGateEvaluationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        // The transition_run_id column arrived with schema v6; snapshots open read-only without
        // migrating, so a pre-v6 database is read with null lineage rather than crashing.
        bool hasTransitionRunId = await ColumnExistsAsync(
            connection,
            "canonical_gate_evaluations",
            "transition_run_id",
            cancellationToken);
        var rows = new List<CanonicalGateEvaluationRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = hasTransitionRunId
            ? """
              SELECT evaluation_id, workflow_identity, stage_identity, transition_identity,
                     gate_identity, status, evaluated_at, requirements_json, explanation, evidence_json,
                     transition_run_id
              FROM canonical_gate_evaluations ORDER BY evaluation_id;
              """
            : """
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
                ReadJson<IReadOnlyList<string>>(reader.GetString(9)),
                hasTransitionRunId && !reader.IsDBNull(10) ? reader.GetString(10) : null));
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

    private static async Task<IReadOnlyList<CanonicalWarningRecord>> ReadWarningsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            // Ledger sequence (insertion order) is the ordering authority for appended facts;
            // the transition_run_id column arrived with schema v6 and reads null before it.
            bool hasTransitionRunId = await ColumnExistsAsync(
                connection,
                "evaluation_warnings",
                "transition_run_id",
                cancellationToken);
            var rows = new List<CanonicalWarningRecord>();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = hasTransitionRunId
                ? """
                  SELECT warning_id, workflow_identity, stage_identity, transition_identity, category,
                         concern, authority, remediation, evidence_json, created_at, transition_run_id
                  FROM evaluation_warnings ORDER BY rowid;
                  """
                : """
                  SELECT warning_id, workflow_identity, stage_identity, transition_identity, category,
                         concern, authority, remediation, evidence_json, created_at
                  FROM evaluation_warnings ORDER BY rowid;
                  """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new CanonicalWarningRecord(
                    reader.GetString(0),
                    new WorkflowIdentity(reader.GetString(1)),
                    reader.IsDBNull(2) ? null : new WorkflowStageIdentity(reader.GetString(2)),
                    reader.IsDBNull(3) ? null : new WorkflowTransitionIdentity(reader.GetString(3)),
                    ParseEnum<WarningCategory>(reader.GetString(4)),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7),
                    ReadJson<IReadOnlyList<string>>(reader.GetString(8)),
                    ParseDate(reader.GetString(9)),
                    hasTransitionRunId && !reader.IsDBNull(10) ? reader.GetString(10) : null));
            }

            return rows;
        }
        catch (SqliteException exception) when (exception.Message.Contains("no such table"))
        {
            // Pre-v4 databases lack evaluation_warnings; warning reads report empty evidence instead of failing.
            return [];
        }
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

    private static async Task<IReadOnlyList<TRow>> ReadSpineRowsOrEmptyAsync<TRow>(
        Func<Task<List<TRow>>> readRows)
    {
        try
        {
            return await readRows();
        }
        catch (SqliteException exception) when (exception.Message.Contains("no such table"))
        {
            // Pre-v3 databases lack the spine tables; spine reads report empty evidence instead of failing.
            return [];
        }
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

    public async Task AppendExecutionRecommendationEvidenceAsync(
        CanonicalExecutionRecommendationEvidenceRecord evidence,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO execution_recommendation_evidence (
                recommendation_id, decision_product_id, workspace_id, run_id,
                workflow_instance_id, transition_run_id, attempt_id, session_id, turn_id,
                recommended_model, recommended_effort, rationale, schema_version, created_at
            ) VALUES ($id, $decision, $workspace, $run, $workflow, $transition, $attempt,
                      $session, $turn, $model, $effort, $rationale, $schema, $created);
            """,
            cancellationToken,
            ("$id", evidence.RecommendationId),
            ("$decision", evidence.DecisionProductId),
            ("$workspace", evidence.WorkspaceId),
            ("$run", evidence.RunId),
            ("$workflow", evidence.WorkflowInstanceId),
            ("$transition", evidence.TransitionRunId),
            ("$attempt", evidence.AttemptId),
            ("$session", evidence.SessionId),
            ("$turn", evidence.TurnId),
            ("$model", evidence.RecommendedModel),
            ("$effort", evidence.RecommendedEffort),
            ("$rationale", evidence.Rationale),
            ("$schema", evidence.SchemaVersion),
            ("$created", Format(evidence.CreatedAt)));
    }

    public async Task<CanonicalExecutionRecommendationEvidenceRecord?> ReadExecutionRecommendationEvidenceAsync(
        string recommendationId,
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath)) return null;
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT recommendation_id, decision_product_id, workspace_id, run_id,
                   workflow_instance_id, transition_run_id, attempt_id, session_id, turn_id,
                   recommended_model, recommended_effort, rationale, schema_version, created_at
            FROM execution_recommendation_evidence WHERE recommendation_id = $id;
            """;
        command.Parameters.AddWithValue("$id", recommendationId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CanonicalExecutionRecommendationEvidenceRecord(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetString(11),
                reader.GetString(12), ParseDate(reader.GetString(13)))
            : null;
    }

    public async Task<CanonicalExecutionRecommendationEvidenceRecord?> ReadExecutionRecommendationForDecisionAsync(
        string decisionProductId,
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath)) return null;
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT recommendation_id, decision_product_id, workspace_id, run_id,
                   workflow_instance_id, transition_run_id, attempt_id, session_id, turn_id,
                   recommended_model, recommended_effort, rationale, schema_version, created_at
            FROM execution_recommendation_evidence
            WHERE decision_product_id = $decision ORDER BY rowid DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$decision", decisionProductId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CanonicalExecutionRecommendationEvidenceRecord(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetString(11),
                reader.GetString(12), ParseDate(reader.GetString(13)))
            : null;
    }

    public async Task<CanonicalExecutionRecommendationEvidenceRecord?> ReadLatestExecutionRecommendationAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath)) return null;
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT recommendation_id, decision_product_id, workspace_id, run_id,
                   workflow_instance_id, transition_run_id, attempt_id, session_id, turn_id,
                   recommended_model, recommended_effort, rationale, schema_version, created_at
            FROM execution_recommendation_evidence ORDER BY rowid DESC LIMIT 1;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CanonicalExecutionRecommendationEvidenceRecord(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetString(11),
                reader.GetString(12), ParseDate(reader.GetString(13)))
            : null;
    }

    public async Task AppendRuntimeProfileEvaluationAsync(
        CanonicalRuntimeProfileEvaluationRecord evaluation,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO runtime_profile_evaluations (
                evaluation_id, recommendation_id, decision_product_id, policy_id,
                provider_capability_id, provider_capability_json, outcome, runtime_profile_id, effective_profile_json,
                reasons_json, evaluated_at
            ) VALUES ($id, $recommendation, $decision, $policy, $capability, $capability_json, $outcome,
                      $profile, $profile_json, $reasons, $evaluated);
            """,
            cancellationToken,
            ("$id", evaluation.EvaluationId),
            ("$recommendation", evaluation.RecommendationId),
            ("$decision", evaluation.DecisionProductId),
            ("$policy", evaluation.PolicyId),
            ("$capability", evaluation.ProviderCapabilityId),
            ("$capability_json", evaluation.ProviderCapabilityJson),
            ("$outcome", evaluation.Outcome),
            ("$profile", evaluation.RuntimeProfileId),
            ("$profile_json", evaluation.EffectiveProfileJson),
            ("$reasons", Json(evaluation.Reasons)),
            ("$evaluated", Format(evaluation.EvaluatedAt)));
    }

    public async Task<CanonicalRuntimeProfileEvaluationRecord?> ReadRuntimeProfileEvaluationAsync(
        string evaluationId,
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath)) return null;
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT evaluation_id, recommendation_id, decision_product_id, policy_id,
                   provider_capability_id, provider_capability_json, outcome, runtime_profile_id, effective_profile_json,
                   reasons_json, evaluated_at
            FROM runtime_profile_evaluations WHERE evaluation_id = $id;
            """;
        command.Parameters.AddWithValue("$id", evaluationId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CanonicalRuntimeProfileEvaluationRecord(
                reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                reader.GetString(8), ReadJson<IReadOnlyList<string>>(reader.GetString(9)),
                ParseDate(reader.GetString(10)))
            : null;
    }

    public async Task<CanonicalRuntimeProfileEvaluationRecord?> ReadRuntimeProfileEvaluationByProfileAsync(
        string runtimeProfileId,
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath)) return null;
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT evaluation_id, recommendation_id, decision_product_id, policy_id,
                   provider_capability_id, provider_capability_json, outcome, runtime_profile_id, effective_profile_json,
                   reasons_json, evaluated_at
            FROM runtime_profile_evaluations
            WHERE runtime_profile_id = $profile ORDER BY rowid DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$profile", runtimeProfileId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CanonicalRuntimeProfileEvaluationRecord(
                reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                reader.GetString(8), ReadJson<IReadOnlyList<string>>(reader.GetString(9)),
                ParseDate(reader.GetString(10)))
            : null;
    }

    public async Task AppendPromptDispatchEventAsync(
        CanonicalPromptDispatchEventRecord dispatchEvent,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO prompt_dispatch_events (
                dispatch_id, rendered_prompt_id, persistence_id, workspace_id, run_id,
                workflow_instance_id, transition_run_id, attempt_id, runtime_profile_id,
                session_id, turn_id, state, recorded_at, evidence_json
            ) VALUES ($dispatch, $prompt, $persistence, $workspace, $run, $workflow,
                      $transition, $attempt, $runtime, $session, $turn, $state, $at, $evidence);
            """,
            cancellationToken,
            ("$dispatch", dispatchEvent.DispatchId),
            ("$prompt", dispatchEvent.RenderedPromptId),
            ("$persistence", dispatchEvent.PersistenceId),
            ("$workspace", dispatchEvent.WorkspaceId),
            ("$run", dispatchEvent.RunId),
            ("$workflow", dispatchEvent.WorkflowInstanceId),
            ("$transition", dispatchEvent.TransitionRunId),
            ("$attempt", dispatchEvent.AttemptId),
            ("$runtime", dispatchEvent.RuntimeProfileId),
            ("$session", dispatchEvent.SessionId),
            ("$turn", dispatchEvent.TurnId),
            ("$state", dispatchEvent.State.ToString()),
            ("$at", Format(dispatchEvent.RecordedAt)),
            ("$evidence", Json(dispatchEvent.Evidence)));
    }

    public async Task<IReadOnlyList<CanonicalPromptDispatchEventRecord>> ReadPromptDispatchEventsAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        var rows = new List<CanonicalPromptDispatchEventRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT event_id, dispatch_id, rendered_prompt_id, persistence_id, workspace_id,
                   run_id, workflow_instance_id, transition_run_id, attempt_id, runtime_profile_id,
                   session_id, turn_id, state, recorded_at, evidence_json
            FROM prompt_dispatch_events ORDER BY event_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CanonicalPromptDispatchEventRecord(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                reader.GetString(8), reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                ParseEnum<PromptDispatchState>(reader.GetString(12)),
                ParseDate(reader.GetString(13)), ReadJson<IReadOnlyList<string>>(reader.GetString(14))));
        }

        return rows;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
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
