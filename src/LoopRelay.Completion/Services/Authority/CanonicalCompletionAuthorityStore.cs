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

public sealed class CanonicalCompletionAuthorityStore(Repository _repository)
{
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

    public async Task<CanonicalCompletionSnapshot> ReadSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        var decisions = new List<CompletionDecision>();
        await using (SqliteCommand command = Command(connection, """
            SELECT decision_id,root_run_id,attempt_id,kind,reason,evidence_json,gate_identities_json,
                   review_identities_json,decided_at FROM canonical_completion_decisions ORDER BY decided_at,decision_id;
            """))
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                decisions.Add(new(new(reader.GetString(0)), new(reader.GetString(1)), new(reader.GetString(2)),
                    Enum.Parse<CompletionDecisionKind>(reader.GetString(3)),
                    reader.IsDBNull(4) ? null : Enum.Parse<CompletionCannotProceedReason>(reader.GetString(4)),
                    ReadList(reader, 5), ReadList(reader, 6), ReadList(reader, 7), Parse(reader.GetString(8))));
        var certificates = new List<CompletionCertificate>();
        await using (SqliteCommand command = Command(connection,
            "SELECT certificate_id,decision_id,evidence_json,certified_at FROM canonical_completion_certificates ORDER BY certified_at,certificate_id;"))
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                certificates.Add(new(new(reader.GetString(0)), new(reader.GetString(1)), ReadList(reader, 2), Parse(reader.GetString(3))));
        var plans = new List<CompletionClosurePlan>();
        await using (SqliteCommand command = Command(connection,
            "SELECT plan_id,decision_id,certificate_id,operations_json,content_hash,planned_at FROM canonical_completion_closure_plans ORDER BY planned_at,plan_id;"))
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                plans.Add(new(new(reader.GetString(0)), new(reader.GetString(1)), new(reader.GetString(2)),
                    JsonSerializer.Deserialize<CompletionClosureOperation[]>(reader.GetString(3)) ?? [],
                    reader.GetString(4), Parse(reader.GetString(5))));
        var settlements = new List<CompletionSettlement>();
        await using (SqliteCommand command = Command(connection,
            "SELECT settlement_id,plan_id,kind,pending_operations_json,evidence_json,reason,settled_at FROM canonical_completion_settlements ORDER BY settled_at,settlement_id;"))
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                settlements.Add(new(new(reader.GetString(0)), new(reader.GetString(1)),
                    Enum.Parse<CompletionSettlementKind>(reader.GetString(2)), ReadList(reader, 3), ReadList(reader, 4),
                    reader.IsDBNull(5) ? null : Enum.Parse<CompletionCannotProceedReason>(reader.GetString(5)),
                    Parse(reader.GetString(6))));
        var terminal = new List<CertifiedTerminalFact>();
        await using (SqliteCommand command = Command(connection, """
            SELECT terminal_id,root_run_id,decision_id,certificate_id,plan_id,settlement_id,
                   effect_receipts_json,recorded_at FROM canonical_certified_terminal_facts ORDER BY recorded_at,terminal_id;
            """))
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                terminal.Add(new(new(reader.GetString(0)), new(reader.GetString(1)), new(reader.GetString(2)),
                    new(reader.GetString(3)), new(reader.GetString(4)), new(reader.GetString(5)),
                    JsonSerializer.Deserialize<CompletionClosureReceipt[]>(reader.GetString(6)) ?? [],
                    Parse(reader.GetString(7))));
        return new(decisions, certificates, plans, settlements, terminal);
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

    private static SqliteCommand Command(SqliteConnection connection, string sql)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
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
