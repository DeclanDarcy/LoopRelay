using LoopRelay.Completion.Models.Authority;

namespace LoopRelay.Completion.Services.Authority;

public sealed record CompletionAuthorityProjectionSnapshot(
    CompletionDecision? LatestDecision,
    CompletionCertificate? Certificate,
    CompletionClosurePlan? ClosurePlan,
    CompletionSettlement? LatestSettlement,
    CertifiedTerminalFact? TerminalFact,
    IReadOnlyList<string> PendingOperations,
    string Watermark);

public interface ICompletionAuthorityProjection
{
    Task<CompletionAuthorityProjectionSnapshot> ProjectAsync(
        CancellationToken cancellationToken = default);
}

public sealed class CompletionAuthorityProjection(CanonicalCompletionAuthorityStore _store)
    : ICompletionAuthorityProjection
{
    public async Task<CompletionAuthorityProjectionSnapshot> ProjectAsync(
        CancellationToken cancellationToken = default)
    {
        // Task 3.4: per-chain `ORDER BY ... DESC LIMIT 1` reads plus a `COUNT(*)` per table, on a
        // read-only connection, instead of hydrating every historical row across every table on a
        // write connection and discarding almost all of it here via `LastOrDefault`.
        CompletionAuthorityHeadsAndCounts heads = await _store.ReadHeadsAndCountsAsync(cancellationToken);
        string watermark = string.Join(':',
            heads.DecisionCount, heads.CertificateCount, heads.ClosurePlanCount,
            heads.SettlementCount, heads.TerminalFactCount,
            heads.Settlement?.Identity.Value ?? heads.ClosurePlan?.Identity.Value
                ?? heads.LatestDecision?.Identity.Value ?? "empty");
        return new(heads.LatestDecision, heads.Certificate, heads.ClosurePlan, heads.Settlement, heads.TerminalFact,
            heads.Settlement?.PendingOperations
                ?? heads.ClosurePlan?.Operations.Select(item => item.Identity).ToArray() ?? [],
            watermark);
    }
}
