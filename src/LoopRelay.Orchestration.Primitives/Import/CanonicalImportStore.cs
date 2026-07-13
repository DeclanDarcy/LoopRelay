using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Import;

public sealed class CanonicalImportStore(string _databasePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task PersistDetectionAsync(ImportDetection detection, CancellationToken token)
    {
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO canonical_import_detections(
                detection_id, source_kind, source_family, source_version, source_fingerprint,
                document_json, detected_at
            ) VALUES($id,$kind,$family,$version,$fingerprint,$document,$at)
            ON CONFLICT(detection_id) DO NOTHING;
            """;
        Add(command, ("$id", detection.Identity.Value), ("$kind", detection.SourceKind.ToString()),
            ("$family", detection.SourceFamily), ("$version", detection.SourceVersion),
            ("$fingerprint", detection.SourceFingerprint), ("$document", Serialize(detection)),
            ("$at", Format(detection.DetectedAt)));
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task PersistPreviewAsync(ImportPreview preview, CancellationToken token)
    {
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO canonical_import_previews(
                    preview_id,detection_id,source_fingerprint,lifecycle,approval_json,document_json,previewed_at
                ) VALUES($preview,$detection,$fingerprint,'Previewed',NULL,$document,$at)
                ON CONFLICT(preview_id) DO NOTHING;
                """;
            Add(command, ("$preview", preview.Identity.Value), ("$detection", preview.Detection.Identity.Value),
                ("$fingerprint", preview.Detection.SourceFingerprint), ("$document", Serialize(preview)),
                ("$at", Format(preview.PreviewedAt)));
            await command.ExecuteNonQueryAsync(token);
        }
        foreach (ImportIdentityMapping mapping in preview.Mappings)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO canonical_import_mappings(
                    preview_id,domain,source_identity,target_identity,preserved,rule,conflict
                ) VALUES($preview,$domain,$source,$target,$preserved,$rule,$conflict);
                """;
            Add(command, ("$preview", preview.Identity.Value), ("$domain", mapping.Domain),
                ("$source", mapping.SourceIdentity), ("$target", mapping.TargetIdentity),
                ("$preserved", mapping.Preserved ? 1 : 0), ("$rule", mapping.Rule),
                ("$conflict", mapping.Conflict));
            await command.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    public async Task<ImportDetection?> ReadDetectionAsync(ImportDetectionIdentity identity, CancellationToken token)
        => await ReadDocumentAsync<ImportDetection>("canonical_import_detections", "detection_id", identity.Value, token);
    public async Task<ImportPreview?> ReadPreviewAsync(ImportPreviewIdentity identity, CancellationToken token)
        => await ReadDocumentAsync<ImportPreview>("canonical_import_previews", "preview_id", identity.Value, token);

    public async Task ApproveAndPlanAsync(ImportApproval approval, ImportOperationIdentity operation,
        string planHash, CancellationToken token)
    {
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        await using (SqliteCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE canonical_import_previews SET lifecycle='Approved', import_id=$operation, approval_json=$approval
                WHERE preview_id=$preview AND source_fingerprint=$fingerprint AND lifecycle='Previewed';
                """;
            Add(update, ("$approval", Serialize(approval)), ("$preview", approval.Preview.Value),
                ("$fingerprint", approval.SourceFingerprint), ("$operation", operation.Value));
            if (await update.ExecuteNonQueryAsync(token) != 1)
                throw new InvalidOperationException("Import approval is stale, duplicate, or does not match the preview fingerprint.");
        }
        await using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO compatibility_import_operations(
                    import_id,source_schema_identity,source_schema_family,source_schema_version,
                    source_digest,plan_hash,state,planned_at,diagnostic_json
                )
                SELECT $operation, detection.source_kind, detection.source_family,
                       CAST(detection.source_version AS INTEGER), detection.source_fingerprint,
                       $plan,'Planned',$at,$diagnostic
                FROM canonical_import_previews preview
                JOIN canonical_import_detections detection ON detection.detection_id=preview.detection_id
                WHERE preview.preview_id=$preview;
                """;
            Add(insert, ("$operation", operation.Value), ("$plan", planHash),
                ("$at", Format(approval.ApprovedAt)), ("$diagnostic", Serialize(approval)),
                ("$preview", approval.Preview.Value));
            await insert.ExecuteNonQueryAsync(token);
        }
        await InsertEventAsync(connection, transaction, operation, "Planned", [approval.Preview.Value], approval.ApprovedAt, token);
        await transaction.CommitAsync(token);
    }

    public async Task CompleteAsync(ImportOperationIdentity operation, ImportPreview preview,
        ImportVerification verification, ImportReceipt receipt, CancellationToken token)
    {
        if (!verification.Equivalent) throw new InvalidOperationException("Import receipt cannot precede semantic equivalence.");
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        await ExecuteAsync(connection, transaction, """
            INSERT INTO canonical_import_verifications(
                verification_id,import_id,equivalent,target_logical_fingerprint,domain_diffs_json,verified_at
            ) VALUES($verification,$operation,1,$fingerprint,$diffs,$verified);
            INSERT INTO canonical_import_receipts(
                receipt_id,import_id,preview_id,source_fingerprint,target_logical_fingerprint,evidence_json,recorded_at
            ) VALUES($receipt,$operation,$preview,$source,$fingerprint,$evidence,$recorded);
            UPDATE compatibility_import_operations SET state='Completed',started_at=COALESCE(started_at,$recorded),
                verified_at=$verified,completed_at=$recorded WHERE import_id=$operation;
            UPDATE canonical_import_previews SET lifecycle='Completed' WHERE preview_id=$preview;
            UPDATE canonical_source_authority SET canonical_only=1,import_receipt_id=$receipt,
                source_non_authoritative_at=$recorded,marker_sequence=marker_sequence+1 WHERE id=1;
            """, token,
            ("$verification", CausalId("importverify")), ("$operation", operation.Value),
            ("$fingerprint", verification.TargetLogicalFingerprint), ("$diffs", Serialize(verification.DomainDiffs)),
            ("$verified", Format(verification.VerifiedAt)), ("$receipt", receipt.Identity.Value),
            ("$preview", preview.Identity.Value), ("$source", preview.Detection.SourceFingerprint),
            ("$evidence", Serialize(receipt.Evidence)), ("$recorded", Format(receipt.RecordedAt)));
        await InsertEventAsync(connection, transaction, operation, "Verified", verification.DomainDiffs, verification.VerifiedAt, token);
        await InsertEventAsync(connection, transaction, operation, "Completed", receipt.Evidence, receipt.RecordedAt, token);
        await transaction.CommitAsync(token);
    }

    public async Task<bool> IsCanonicalOnlyAsync(CancellationToken token)
    {
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT canonical_only FROM canonical_source_authority WHERE id=1;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture) == 1;
    }

    public async Task RecordAdapterExhaustionAsync(
        ImportAdapterDescriptor adapter,
        string portfolioFingerprint,
        IReadOnlyDictionary<string, string> fixtureReceipts,
        IReadOnlyDictionary<string, string> canonicalOnlyRuns,
        IReadOnlyDictionary<string, string> disabledResults,
        CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portfolioFingerprint);
        string[] missing = adapter.FixtureIdentities
            .Where(fixture => !fixtureReceipts.ContainsKey(fixture) ||
                              !canonicalOnlyRuns.ContainsKey(fixture) ||
                              !disabledResults.ContainsKey(fixture))
            .ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Adapter exhaustion lacks accepted evidence for: {string.Join(", ", missing)}.");
        if (!await IsCanonicalOnlyAsync(token))
            throw new InvalidOperationException("Adapter exhaustion requires a completed canonical-only import.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        await ExecuteAsync(connection, transaction, """
            UPDATE canonical_import_adapter_exhaustion SET superseded_at=$at
            WHERE adapter_identity=$adapter AND adapter_version=$version
              AND portfolio_fingerprint<>$portfolio AND superseded_at IS NULL;
            INSERT INTO canonical_import_adapter_exhaustion(
                adapter_identity,adapter_version,portfolio_fingerprint,fixture_receipts_json,
                canonical_only_runs_json,disabled_results_json,exhausted_at,superseded_at
            ) VALUES($adapter,$version,$portfolio,$receipts,$runs,$disabled,$at,NULL)
            ON CONFLICT(adapter_identity,adapter_version,portfolio_fingerprint) DO NOTHING;
            """, token, ("$adapter", adapter.AdapterIdentity), ("$version", adapter.Version),
            ("$portfolio", portfolioFingerprint), ("$receipts", Serialize(fixtureReceipts)),
            ("$runs", Serialize(canonicalOnlyRuns)), ("$disabled", Serialize(disabledResults)),
            ("$at", Format(now)));
        await transaction.CommitAsync(token);
    }

    public async Task<ImportAdapterExhaustion?> ReadActiveAdapterExhaustionAsync(
        string adapterIdentity, string adapterVersion, CancellationToken token)
    {
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT adapter_identity,adapter_version,portfolio_fingerprint,fixture_receipts_json,
                   canonical_only_runs_json,disabled_results_json,exhausted_at,superseded_at
            FROM canonical_import_adapter_exhaustion
            WHERE adapter_identity=$adapter AND adapter_version=$version AND superseded_at IS NULL
            ORDER BY exhausted_at DESC LIMIT 1;
            """;
        Add(command, ("$adapter", adapterIdentity), ("$version", adapterVersion));
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        return new ImportAdapterExhaustion(reader.GetString(0), reader.GetString(1), reader.GetString(2),
            JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(reader.GetString(3), JsonOptions)
                ?? new Dictionary<string, string>(),
            JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(reader.GetString(4), JsonOptions)
                ?? new Dictionary<string, string>(),
            JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(reader.GetString(5), JsonOptions)
                ?? new Dictionary<string, string>(),
            DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind));
    }

    public async Task<ImportOperationIdentity?> ReadOperationAsync(
        ImportPreviewIdentity preview,
        CancellationToken token)
    {
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT import_id FROM canonical_import_previews WHERE preview_id=$preview AND lifecycle='Approved';";
        command.Parameters.AddWithValue("$preview", preview.Value);
        string? value = Convert.ToString(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
        return value is null ? null : new ImportOperationIdentity(value);
    }

    public async Task<ImportApproval?> ReadApprovalAsync(ImportPreviewIdentity preview, CancellationToken token)
    {
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT approval_json FROM canonical_import_previews WHERE preview_id=$preview AND lifecycle='Approved';";
        command.Parameters.AddWithValue("$preview", preview.Value);
        string? json = Convert.ToString(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
        return json is null ? null : JsonSerializer.Deserialize<ImportApproval>(json, JsonOptions);
    }

    public async Task<bool> ContainsPreviewAsync(string identity, CancellationToken token)
    {
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM canonical_import_previews WHERE preview_id=$id);";
        command.Parameters.AddWithValue("$id", identity);
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture) == 1;
    }

    public async Task<ImportReceipt?> ReadReceiptBySourceFingerprintAsync(string fingerprint, CancellationToken token)
    {
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT receipt_id,import_id,preview_id,source_fingerprint,target_logical_fingerprint,evidence_json,recorded_at
            FROM canonical_import_receipts WHERE source_fingerprint=$fingerprint ORDER BY recorded_at LIMIT 1;
            """;
        command.Parameters.AddWithValue("$fingerprint", fingerprint);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        return new ImportReceipt(new ImportReceiptIdentity(reader.GetString(0)),
            new ImportOperationIdentity(reader.GetString(1)), new ImportPreviewIdentity(reader.GetString(2)),
            reader.GetString(3), reader.GetString(4),
            JsonSerializer.Deserialize<string[]>(reader.GetString(5), JsonOptions) ?? [],
            DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    public async Task<ImportReceipt?> ReadReceiptAsync(string identity, CancellationToken token)
    {
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenReadOnly(_databasePath);
        await connection.OpenAsync(token);
        await using SqliteCommand table = connection.CreateCommand();
        table.CommandText = "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name='canonical_import_receipts');";
        if (Convert.ToInt64(await table.ExecuteScalarAsync(token), CultureInfo.InvariantCulture) != 1) return null;
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT receipt_id,import_id,preview_id,source_fingerprint,target_logical_fingerprint,evidence_json,recorded_at
            FROM canonical_import_receipts
            WHERE receipt_id=$identity OR import_id=$identity OR preview_id=$identity
            ORDER BY recorded_at DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$identity", identity);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        return new ImportReceipt(new ImportReceiptIdentity(reader.GetString(0)),
            new ImportOperationIdentity(reader.GetString(1)), new ImportPreviewIdentity(reader.GetString(2)),
            reader.GetString(3), reader.GetString(4),
            JsonSerializer.Deserialize<string[]>(reader.GetString(5), JsonOptions) ?? [],
            DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    public async Task AdoptApprovedAsync(ImportApproval approval, ImportOperationIdentity operation, CancellationToken token)
    {
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE canonical_import_previews SET lifecycle='Approved',import_id=$operation,approval_json=$approval
            WHERE preview_id=$preview AND source_fingerprint=$fingerprint;
            """;
        Add(command, ("$operation", operation.Value), ("$approval", Serialize(approval)),
            ("$preview", approval.Preview.Value), ("$fingerprint", approval.SourceFingerprint));
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task StageFilesystemProductFactsAsync(
        string repositoryPath,
        ImportPreview preview,
        CancellationToken token)
    {
        var products = new (string Path, ProductIdentity Product, WorkflowIdentity Producer)[]
        {
            (".agents/plan.md", ProductIdentity.ExecutablePlan, WorkflowIdentity.Plan),
            (".agents/details.md", ProductIdentity.ExecutionDetails, WorkflowIdentity.Plan),
            (".agents/milestones", ProductIdentity.ExecutionMilestoneSet, WorkflowIdentity.Plan),
            (".agents/operational-context.md", ProductIdentity.OperationalContext, WorkflowIdentity.Plan),
            (".agents/projections/plan.md", ProductIdentity.AdversarialProjection, WorkflowIdentity.Plan),
            (".agents/projections/adversarial-plan-review.md", ProductIdentity.AdversarialReview, WorkflowIdentity.Plan),
            (".agents/decision-session.json", ProductIdentity.DecisionSet, WorkflowIdentity.Execute),
            (".agents/history", ProductIdentity.ImplementationSlice, WorkflowIdentity.Execute),
            (".agents/handoff.md", ProductIdentity.ExecutionHandoff, WorkflowIdentity.Execute),
            (".agents/evidence.md", ProductIdentity.CompletionEvidence, WorkflowIdentity.Execute),
            (".agents/archive/epics", ProductIdentity.CertifiedCompletion, WorkflowIdentity.Execute),
            (".agents/epic.md", ProductIdentity.PreparedEpic, WorkflowIdentity.TraditionalRoadmap),
        };
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        foreach ((string relative, ProductIdentity product, WorkflowIdentity producer) in products)
        {
            string path = Path.Combine(repositoryPath, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) && !Directory.Exists(path)) continue;
            await ExecuteAsync(connection, transaction, """
                INSERT INTO canonical_product_records(
                    product_identity,producer_workflow,producer_transition,intended_consumers_json,
                    repository_ownership,authority,storage_representations_json,causal_identity,
                    freshness,validation_state,lifecycle,evidence_locations_json,updated_at,schema_version
                ) VALUES($product,$producer,'CompatibilityImport','[]','repository-owned',
                    'canonical-import-gateway',$storage,$causal,'Fresh','Valid','Active',$evidence,$at,1)
                ON CONFLICT(product_identity) DO NOTHING;
                """, token, ("$product", product.Value), ("$producer", producer.Value),
                ("$storage", Serialize(new[] { relative })),
                ("$causal", $"import:{preview.Identity.Value}:{product.Value}"),
                ("$evidence", Serialize(new[] { relative })), ("$at", Format(DateTimeOffset.UtcNow)));
        }
        await transaction.CommitAsync(token);
    }

    private async Task<T?> ReadDocumentAsync<T>(string table, string key, string value, CancellationToken token)
    {
        await using SqliteConnection connection = await OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT document_json FROM {table} WHERE {key}=$value;";
        command.Parameters.AddWithValue("$value", value);
        string? json = Convert.ToString(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
        return json is null ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken token)
    {
        SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenMigrationTarget(_databasePath);
        try
        {
            await connection.OpenAsync(token);
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, token);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
    private static async Task InsertEventAsync(SqliteConnection c, SqliteTransaction t,
        ImportOperationIdentity operation, string state, IReadOnlyList<string> evidence, DateTimeOffset at, CancellationToken token)
        => await ExecuteAsync(c, t, """
            INSERT INTO compatibility_import_events(event_id,import_id,state,recorded_at,evidence_json)
            VALUES($event,$operation,$state,$at,$evidence);
            """, token, ("$event", CausalId("impevent")), ("$operation", operation.Value),
            ("$state", state), ("$at", Format(at)), ("$evidence", Serialize(evidence)));
    private static async Task ExecuteAsync(SqliteConnection c, SqliteTransaction t, string sql,
        CancellationToken token, params (string Name, object? Value)[] values)
    {
        await using SqliteCommand command = c.CreateCommand(); command.Transaction=t; command.CommandText=sql; Add(command, values);
        await command.ExecuteNonQueryAsync(token);
    }
    private static void Add(SqliteCommand command, params (string Name, object? Value)[] values)
    { foreach ((string name, object? value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value); }
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static string Format(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static string CausalId(string prefix) => LoopRelay.Core.Models.Identity.CausalUlid.NewId(prefix);
}
