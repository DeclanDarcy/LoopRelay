using System.Globalization;
using System.Text.Json;
using LoopRelay.Completion.Models.Authority;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Completion.Services.Authority;

public sealed record CanonicalCompletionSnapshot(
    IReadOnlyList<CompletionDecision> Decisions,
    IReadOnlyList<CompletionCertificate> Certificates,
    IReadOnlyList<CompletionClosurePlan> ClosurePlans,
    IReadOnlyList<CompletionSettlement> Settlements,
    IReadOnlyList<CertifiedTerminalFact> TerminalFacts);

/// <summary>
/// Result of <see cref="CanonicalCompletionAuthorityStore.ReadHeadsAndCountsAsync"/> (Task 3.4): the
/// latest decision, and - reached by walking forward from it exactly as
/// <see cref="CanonicalCompletionAuthorityStore.ReadSnapshotAsync(CancellationToken)"/>'s in-memory
/// chain used to - the latest certificate, closure plan, and settlement, plus the latest terminal fact
/// for that decision's run, plus one row count per table. Every count is unscoped (every root run),
/// matching the unscoped read this replaces for <see cref="CompletionAuthorityProjection"/>.
/// </summary>
public sealed record CompletionAuthorityHeadsAndCounts(
    CompletionDecision? LatestDecision,
    CompletionCertificate? Certificate,
    CompletionClosurePlan? ClosurePlan,
    CompletionSettlement? Settlement,
    CertifiedTerminalFact? TerminalFact,
    int DecisionCount,
    int CertificateCount,
    int ClosurePlanCount,
    int SettlementCount,
    int TerminalFactCount);

public sealed class CanonicalCompletionAuthorityStore(Repository _repository)
{
    /// <summary>
    /// Test-only observability: the read-only connection <see cref="ReadHeadsAndCountsAsync"/> opens
    /// is offered here immediately after it is opened, before any schema check or data read issues a
    /// statement on it. Mirrors <c>LedgerLoopHistoryStore.ConnectionObserverForTesting</c> and
    /// <c>CanonicalWorkflowPersistenceStore.ConnectionObserverForTesting</c>, for the same reason:
    /// Microsoft.Data.Sqlite exposes no statement hook, and this method opens its own connection with
    /// pooling disabled, so a test cannot otherwise reach the handle a read ran on.
    /// </summary>
    internal Action<SqliteConnection>? ConnectionObserverForTesting { get; set; }

    /// <summary>
    /// Test-only observability, mirroring <c>CanonicalWorkflowPersistenceStore.ReadWorkflowInstancesAsyncInvokedForTesting</c>
    /// (Task 3.3): fires whenever the unkeyed, full-hydration read <see cref="ReadSnapshotCoreAsync"/>
    /// performs actually runs - via either overload of <see cref="ReadSnapshotAsync(CancellationToken)"/>
    /// - so a test can assert directly that <see cref="CompletionAuthorityProjection.ProjectAsync"/>'s
    /// head-and-count read (Task 3.4, <see cref="ReadHeadsAndCountsAsync"/>) never fell back to it.
    /// Instance-scoped, like <see cref="ConnectionObserverForTesting"/>.
    /// </summary>
    internal Action? ReadSnapshotAsyncInvokedForTesting { get; set; }

    public async Task AppendDecisionAsync(CompletionDecision decision,
        CancellationToken cancellationToken = default)
    {
        decision.Validate();
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction();
        await InsertDecisionAsync(connection, transaction, decision, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task PersistCertifiedCandidateAsync(
        CompletionDecision decision,
        CompletionCertificate certificate,
        CompletionClosurePlan plan,
        CancellationToken cancellationToken = default)
    {
        decision.Validate();
        if (decision.Kind != CompletionDecisionKind.CertifiedCandidate ||
            certificate.Decision != decision.Identity || plan.Decision != decision.Identity ||
            plan.Certificate != certificate.Identity)
            throw new InvalidOperationException("Certified completion decision, certificate, and closure plan do not share one causal identity.");
        ValidatePlan(plan);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction();
        await InsertDecisionAsync(connection, transaction, decision, cancellationToken);
        await ExecuteAsync(connection, transaction, """
            INSERT INTO canonical_completion_certificates(
                certificate_id,decision_id,evidence_json,certified_at
            ) VALUES($id,$decision,$evidence,$at) ON CONFLICT(certificate_id) DO NOTHING;
            """, cancellationToken, ("$id", certificate.Identity.Value),
            ("$decision", certificate.Decision.Value), ("$evidence", Json(certificate.EvidenceIdentities)),
            ("$at", Format(certificate.CertifiedAt)));
        await ExecuteAsync(connection, transaction, """
            INSERT INTO canonical_completion_closure_plans(
                plan_id,decision_id,certificate_id,operations_json,content_hash,planned_at
            ) VALUES($id,$decision,$certificate,$operations,$hash,$at)
            ON CONFLICT(plan_id) DO NOTHING;
            """, cancellationToken, ("$id", plan.Identity.Value), ("$decision", plan.Decision.Value),
            ("$certificate", plan.Certificate.Value), ("$operations", Json(plan.Operations)),
            ("$hash", plan.ContentHash), ("$at", Format(plan.PlannedAt)));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AppendSettlementAsync(
        CompletionDecision decision,
        CompletionCertificate certificate,
        CompletionClosurePlan plan,
        CompletionSettlement settlement,
        IReadOnlyList<CompletionClosureReceipt> verifiedEffectReceipts,
        CancellationToken cancellationToken = default)
    {
        if (settlement.Plan != plan.Identity)
            throw new InvalidOperationException("Completion settlement references another closure plan.");
        if (settlement.Kind == CompletionSettlementKind.CertifiedTerminal)
        {
            HashSet<string> requiredOperations = plan.Operations
                .Where(operation => operation.Kind != CompletionClosureOperationKind.CertifiedTerminalFact)
                .Select(operation => operation.Identity)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> receiptedOperations = verifiedEffectReceipts
                .Select(receipt => receipt.OperationIdentity)
                .ToHashSet(StringComparer.Ordinal);
            if (settlement.PendingOperations.Count != 0 ||
                !requiredOperations.SetEquals(receiptedOperations) ||
                verifiedEffectReceipts.Any(receipt => string.IsNullOrWhiteSpace(receipt.EffectReceiptIdentity)))
                throw new InvalidOperationException("Certified terminal settlement requires every closure operation and verified effect receipt.");
        }
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction();
        await ExecuteAsync(connection, transaction, """
            INSERT INTO canonical_completion_settlements(
                settlement_id,plan_id,kind,pending_operations_json,evidence_json,reason,settled_at
            ) VALUES($id,$plan,$kind,$pending,$evidence,$reason,$at)
            ON CONFLICT(settlement_id) DO NOTHING;
            """, cancellationToken, ("$id", settlement.Identity.Value), ("$plan", settlement.Plan.Value),
            ("$kind", settlement.Kind.ToString()), ("$pending", Json(settlement.PendingOperations)),
            ("$evidence", Json(settlement.EvidenceIdentities)),
            ("$reason", settlement.CannotProceedReason?.ToString()), ("$at", Format(settlement.SettledAt)));
        if (settlement.Kind == CompletionSettlementKind.CertifiedTerminal)
        {
            CertifiedTerminalFact fact = new(CertifiedTerminalIdentity.New(), decision.RootRun,
                decision.Identity, certificate.Identity, plan.Identity, settlement.Identity,
                verifiedEffectReceipts
                    .Distinct()
                    .OrderBy(receipt => receipt.OperationIdentity, StringComparer.Ordinal)
                    .ThenBy(receipt => receipt.EffectReceiptIdentity, StringComparer.Ordinal)
                    .ToArray(),
                settlement.SettledAt);
            await ExecuteAsync(connection, transaction, """
                INSERT INTO canonical_certified_terminal_facts(
                    terminal_id,root_run_id,decision_id,certificate_id,plan_id,settlement_id,
                    effect_receipts_json,recorded_at
                ) VALUES($id,$run,$decision,$certificate,$plan,$settlement,$receipts,$at)
                ON CONFLICT(root_run_id) DO NOTHING;
                """, cancellationToken, ("$id", fact.Identity.Value), ("$run", fact.RootRun.Value),
                ("$decision", fact.Decision.Value), ("$certificate", fact.Certificate.Value),
                ("$plan", fact.Plan.Value), ("$settlement", fact.Settlement.Value),
                ("$receipts", Json(fact.EffectReceipts)), ("$at", Format(fact.RecordedAt)));
        }
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Whole-workspace read across every root run. Status and projection consumers depend on this;
    /// consumers that only care about one root run should use the run-scoped overload instead.
    /// </summary>
    public Task<CanonicalCompletionSnapshot> ReadSnapshotAsync(
        CancellationToken cancellationToken = default) =>
        ReadSnapshotCoreAsync(rootRun: null, cancellationToken);

    /// <summary>
    /// Reads only the completion authority owned by one root run. Both overloads share one
    /// implementation — including <see cref="OpenAsync"/>, so schema-compatibility failures are
    /// classified and thrown identically whether or not the read is run-scoped.
    /// </summary>
    public Task<CanonicalCompletionSnapshot> ReadSnapshotAsync(
        RunIdentity rootRun,
        CancellationToken cancellationToken = default) =>
        ReadSnapshotCoreAsync(rootRun, cancellationToken);

    private async Task<CanonicalCompletionSnapshot> ReadSnapshotCoreAsync(
        RunIdentity? rootRun,
        CancellationToken cancellationToken)
    {
        ReadSnapshotAsyncInvokedForTesting?.Invoke();
        // Only `canonical_completion_decisions` and `canonical_certified_terminal_facts` carry a run
        // column. Certificates, closure plans, and settlements are narrowed by reachability from this
        // run's decisions (`decision_id`, and `plan_id` for settlements) instead.
        //
        // That substitution is lossless only because every row in those three tables is written with a
        // committed parent: `PersistCertifiedCandidateAsync` commits the decision, certificate, and plan
        // in one transaction, and `AppendSettlementAsync` is only ever reached for a plan already read
        // back from this store. No FOREIGN KEY enforces it (the schema declares none) — the guarantee is
        // procedural. A future writer that bypasses that discipline could orphan a row, and this read
        // would silently omit it while the unfiltered overload still returned it.
        string runScope = rootRun is null ? string.Empty : " WHERE root_run_id = $run";
        const string DecisionsOfRun =
            "SELECT decision_id FROM canonical_completion_decisions WHERE root_run_id = $run";
        string decisionScope = rootRun is null ? string.Empty : $" WHERE decision_id IN ({DecisionsOfRun})";
        string planScope = rootRun is null ? string.Empty
            : $" WHERE plan_id IN (SELECT plan_id FROM canonical_completion_closure_plans WHERE decision_id IN ({DecisionsOfRun}))";

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        var decisions = new List<CompletionDecision>();
        await using (SqliteCommand command = Command(connection, $"""
            SELECT decision_id,root_run_id,attempt_id,kind,reason,evidence_json,gate_identities_json,
                   review_identities_json,decided_at FROM canonical_completion_decisions{runScope} ORDER BY decided_at,decision_id;
            """, rootRun))
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                decisions.Add(new(new(reader.GetString(0)), new(reader.GetString(1)), new(reader.GetString(2)),
                    Enum.Parse<CompletionDecisionKind>(reader.GetString(3)),
                    reader.IsDBNull(4) ? null : Enum.Parse<CompletionCannotProceedReason>(reader.GetString(4)),
                    ReadList(reader, 5), ReadList(reader, 6), ReadList(reader, 7), Parse(reader.GetString(8))));
        var certificates = new List<CompletionCertificate>();
        await using (SqliteCommand command = Command(connection,
            $"SELECT certificate_id,decision_id,evidence_json,certified_at FROM canonical_completion_certificates{decisionScope} ORDER BY certified_at,certificate_id;", rootRun))
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                certificates.Add(new(new(reader.GetString(0)), new(reader.GetString(1)), ReadList(reader, 2), Parse(reader.GetString(3))));
        var plans = new List<CompletionClosurePlan>();
        await using (SqliteCommand command = Command(connection,
            $"SELECT plan_id,decision_id,certificate_id,operations_json,content_hash,planned_at FROM canonical_completion_closure_plans{decisionScope} ORDER BY planned_at,plan_id;", rootRun))
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                plans.Add(new(new(reader.GetString(0)), new(reader.GetString(1)), new(reader.GetString(2)),
                    JsonSerializer.Deserialize<CompletionClosureOperation[]>(reader.GetString(3)) ?? [],
                    reader.GetString(4), Parse(reader.GetString(5))));
        var settlements = new List<CompletionSettlement>();
        await using (SqliteCommand command = Command(connection,
            $"SELECT settlement_id,plan_id,kind,pending_operations_json,evidence_json,reason,settled_at FROM canonical_completion_settlements{planScope} ORDER BY settled_at,settlement_id;", rootRun))
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                settlements.Add(new(new(reader.GetString(0)), new(reader.GetString(1)),
                    Enum.Parse<CompletionSettlementKind>(reader.GetString(2)), ReadList(reader, 3), ReadList(reader, 4),
                    reader.IsDBNull(5) ? null : Enum.Parse<CompletionCannotProceedReason>(reader.GetString(5)),
                    Parse(reader.GetString(6))));
        var terminal = new List<CertifiedTerminalFact>();
        await using (SqliteCommand command = Command(connection, $"""
            SELECT terminal_id,root_run_id,decision_id,certificate_id,plan_id,settlement_id,
                   effect_receipts_json,recorded_at FROM canonical_certified_terminal_facts{runScope} ORDER BY recorded_at,terminal_id;
            """, rootRun))
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                terminal.Add(new(new(reader.GetString(0)), new(reader.GetString(1)), new(reader.GetString(2)),
                    new(reader.GetString(3)), new(reader.GetString(4)), new(reader.GetString(5)),
                    JsonSerializer.Deserialize<CompletionClosureReceipt[]>(reader.GetString(6)) ?? [],
                    Parse(reader.GetString(7))));
        return new(decisions, certificates, plans, settlements, terminal);
    }

    /// <summary>
    /// Head-and-count projection read for <see cref="CompletionAuthorityProjection"/> (Task 3.4). That
    /// projection only ever needs the latest decision, the latest certificate/plan/settlement reached
    /// by walking forward from it, the latest terminal fact for its run, and one row count per table
    /// for its watermark - not every historical row. It used to get there by calling the unscoped
    /// <see cref="ReadSnapshotAsync(CancellationToken)"/> overload on a write connection (which also
    /// called <see cref="LoopRelayWorkspaceDatabase.EnsureSchemaAsync"/>) and then discarding almost
    /// all of what it hydrated in memory.
    /// <para>
    /// Runs on a read-only connection and never opens a write transaction. It deliberately does not
    /// call <see cref="LoopRelayWorkspaceDatabase.EnsureSchemaAsync"/> - that opens a write transaction
    /// on a schema-memo miss, and throws outright for an unrecognized family, both wrong for a
    /// connection this method must keep read-only. Instead it follows the read-only-safe admission path
    /// <c>LedgerLoopHistoryStore.ReadLatestAsync</c> established: answer from the cheap schema memo
    /// (<see cref="LoopRelayWorkspaceDatabase.InspectMemoizedAsync"/>, a 2-SELECT live-stamp re-check)
    /// once this process has admitted this exact database file, and fall back to full classification
    /// (<see cref="LoopRelayWorkspaceDatabase.InspectSchemaAsync"/>) - never
    /// <see cref="LoopRelayWorkspaceDatabase.InspectStampedAsync"/>, which trusts the on-disk stamp over
    /// physical shape - on a cold miss or a live mismatch. A not-yet-admitted, wrong-version, or
    /// tampered store is therefore still classified in full and still rejected with the same
    /// <see cref="WorkspaceCompatibilityImportRequiredException"/> as the write path always threw,
    /// never a silent pass.
    /// </para>
    /// <para>
    /// A workspace database that has never been created is tolerated rather than admitted: a read-only
    /// open cannot create or migrate one, so a missing file answers with every field empty (mirroring
    /// <c>LedgerLoopHistoryStore.ReadLatestAsync</c>'s <c>null</c> and
    /// <c>CanonicalWorkflowPersistenceStore.ReadActiveWorkflowInstancesAsync</c>'s empty list for the
    /// same case) instead of the write path's old side effect of silently creating and migrating one.
    /// </para>
    /// </summary>
    public async Task<CompletionAuthorityHeadsAndCounts> ReadHeadsAndCountsAsync(
        CancellationToken cancellationToken = default)
    {
        string database = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(database))
        {
            return new(null, null, null, null, null, 0, 0, 0, 0, 0);
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(database);
        await connection.OpenAsync(cancellationToken);
        ConnectionObserverForTesting?.Invoke(connection);
        WorkspaceSchemaInspection inspection =
            await LoopRelayWorkspaceDatabase.InspectMemoizedAsync(connection, cancellationToken)
            ?? await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection, cancellationToken);
        if (inspection.Family != WorkspaceSchemaFamily.CanonicalWorkspace ||
            inspection.Version != LoopRelayWorkspaceDatabase.CurrentSchemaVersion)
        {
            throw new WorkspaceCompatibilityImportRequiredException(inspection);
        }

        CompletionDecision? decision = await ReadLatestDecisionAsync(connection, cancellationToken);
        CompletionCertificate? certificate = decision is null
            ? null : await ReadLatestCertificateAsync(connection, decision.Identity, cancellationToken);
        CompletionClosurePlan? plan = certificate is null
            ? null : await ReadLatestPlanAsync(connection, certificate.Identity, cancellationToken);
        CompletionSettlement? settlement = plan is null
            ? null : await ReadLatestSettlementAsync(connection, plan.Identity, cancellationToken);
        CertifiedTerminalFact? terminal = decision is null
            ? null : await ReadLatestTerminalAsync(connection, decision.RootRun, cancellationToken);

        return new(decision, certificate, plan, settlement, terminal,
            await ScalarCountAsync(connection, "SELECT COUNT(*) FROM canonical_completion_decisions;", cancellationToken),
            await ScalarCountAsync(connection, "SELECT COUNT(*) FROM canonical_completion_certificates;", cancellationToken),
            await ScalarCountAsync(connection, "SELECT COUNT(*) FROM canonical_completion_closure_plans;", cancellationToken),
            await ScalarCountAsync(connection, "SELECT COUNT(*) FROM canonical_completion_settlements;", cancellationToken),
            await ScalarCountAsync(connection, "SELECT COUNT(*) FROM canonical_certified_terminal_facts;", cancellationToken));
    }

    private static async Task<CompletionDecision?> ReadLatestDecisionAsync(
        SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT decision_id,root_run_id,attempt_id,kind,reason,evidence_json,gate_identities_json,
                   review_identities_json,decided_at
            FROM canonical_completion_decisions
            ORDER BY decided_at DESC, decision_id DESC
            LIMIT 1;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(new(reader.GetString(0)), new(reader.GetString(1)), new(reader.GetString(2)),
            Enum.Parse<CompletionDecisionKind>(reader.GetString(3)),
            reader.IsDBNull(4) ? null : Enum.Parse<CompletionCannotProceedReason>(reader.GetString(4)),
            ReadList(reader, 5), ReadList(reader, 6), ReadList(reader, 7), Parse(reader.GetString(8)));
    }

    private static async Task<CompletionCertificate?> ReadLatestCertificateAsync(
        SqliteConnection connection, CompletionDecisionIdentity decision, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT certificate_id,decision_id,evidence_json,certified_at
            FROM canonical_completion_certificates
            WHERE decision_id = $decision
            ORDER BY certified_at DESC, certificate_id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$decision", decision.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(new(reader.GetString(0)), new(reader.GetString(1)), ReadList(reader, 2), Parse(reader.GetString(3)));
    }

    private static async Task<CompletionClosurePlan?> ReadLatestPlanAsync(
        SqliteConnection connection, CompletionCertificateIdentity certificate, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT plan_id,decision_id,certificate_id,operations_json,content_hash,planned_at
            FROM canonical_completion_closure_plans
            WHERE certificate_id = $certificate
            ORDER BY planned_at DESC, plan_id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$certificate", certificate.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(new(reader.GetString(0)), new(reader.GetString(1)), new(reader.GetString(2)),
            JsonSerializer.Deserialize<CompletionClosureOperation[]>(reader.GetString(3)) ?? [],
            reader.GetString(4), Parse(reader.GetString(5)));
    }

    private static async Task<CompletionSettlement?> ReadLatestSettlementAsync(
        SqliteConnection connection, CompletionClosurePlanIdentity plan, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT settlement_id,plan_id,kind,pending_operations_json,evidence_json,reason,settled_at
            FROM canonical_completion_settlements
            WHERE plan_id = $plan
            ORDER BY settled_at DESC, settlement_id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$plan", plan.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(new(reader.GetString(0)), new(reader.GetString(1)),
            Enum.Parse<CompletionSettlementKind>(reader.GetString(2)), ReadList(reader, 3), ReadList(reader, 4),
            reader.IsDBNull(5) ? null : Enum.Parse<CompletionCannotProceedReason>(reader.GetString(5)),
            Parse(reader.GetString(6)));
    }

    private static async Task<CertifiedTerminalFact?> ReadLatestTerminalAsync(
        SqliteConnection connection, RunIdentity rootRun, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT terminal_id,root_run_id,decision_id,certificate_id,plan_id,settlement_id,
                   effect_receipts_json,recorded_at
            FROM canonical_certified_terminal_facts
            WHERE root_run_id = $run
            ORDER BY recorded_at DESC, terminal_id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$run", rootRun.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(new(reader.GetString(0)), new(reader.GetString(1)), new(reader.GetString(2)),
            new(reader.GetString(3)), new(reader.GetString(4)), new(reader.GetString(5)),
            JsonSerializer.Deserialize<CompletionClosureReceipt[]>(reader.GetString(6)) ?? [],
            Parse(reader.GetString(7)));
    }

    private static async Task<int> ScalarCountAsync(
        SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static void ValidatePlan(CompletionClosurePlan plan)
    {
        if (plan.Operations.Count == 0 || plan.Operations[^1].Kind != CompletionClosureOperationKind.CertifiedTerminalFact)
            throw new ArgumentException("A completion closure plan must end with the certified-terminal fact.");
        for (int index = 0; index < plan.Operations.Count; index++)
        {
            CompletionClosureOperation operation = plan.Operations[index];
            if (operation.Order != index || (index > 0 &&
                !operation.Dependencies.SequenceEqual([plan.Operations[index - 1].Identity])))
                throw new ArgumentException("Completion closure operations must form one immutable ordered dependency chain.");
        }
    }

    private static Task InsertDecisionAsync(SqliteConnection connection, SqliteTransaction transaction,
        CompletionDecision decision, CancellationToken cancellationToken) => ExecuteAsync(connection, transaction, """
        INSERT INTO canonical_completion_decisions(
            decision_id,root_run_id,attempt_id,kind,reason,evidence_json,gate_identities_json,
            review_identities_json,decided_at
        ) VALUES($id,$run,$attempt,$kind,$reason,$evidence,$gates,$reviews,$at)
        ON CONFLICT(decision_id) DO NOTHING;
        """, cancellationToken, ("$id", decision.Identity.Value), ("$run", decision.RootRun.Value),
        ("$attempt", decision.Attempt.Value), ("$kind", decision.Kind.ToString()),
        ("$reason", decision.CannotProceedReason?.ToString()), ("$evidence", Json(decision.EvidenceIdentities)),
        ("$gates", Json(decision.GateIdentities)), ("$reviews", Json(decision.ReviewIdentities)),
        ("$at", Format(decision.DecidedAt)));

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        string database = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(database);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        return connection;
    }

    private static SqliteCommand Command(SqliteConnection connection, string sql, RunIdentity? rootRun = null)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        if (rootRun.HasValue)
            command.Parameters.AddWithValue("$run", rootRun.Value.Value);
        return command;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction,
        string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach ((string name, object? value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static IReadOnlyList<string> ReadList(SqliteDataReader reader, int ordinal) =>
        JsonSerializer.Deserialize<string[]>(reader.GetString(ordinal)) ?? [];
    private static string Format(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
