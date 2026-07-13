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
        CanonicalCompletionSnapshot snapshot = await _store.ReadSnapshotAsync(cancellationToken);
        CompletionDecision? decision = snapshot.Decisions.LastOrDefault();
        CompletionCertificate? certificate = decision is null ? null : snapshot.Certificates
            .LastOrDefault(item => item.Decision == decision.Identity);
        CompletionClosurePlan? plan = certificate is null ? null : snapshot.ClosurePlans
            .LastOrDefault(item => item.Certificate == certificate.Identity);
        CompletionSettlement? settlement = plan is null ? null : snapshot.Settlements
            .LastOrDefault(item => item.Plan == plan.Identity);
        CertifiedTerminalFact? terminal = decision is null ? null : snapshot.TerminalFacts
            .LastOrDefault(item => item.RootRun == decision.RootRun);
        string watermark = string.Join(':',
            snapshot.Decisions.Count, snapshot.Certificates.Count, snapshot.ClosurePlans.Count,
            snapshot.Settlements.Count, snapshot.TerminalFacts.Count,
            settlement?.Identity.Value ?? plan?.Identity.Value ?? decision?.Identity.Value ?? "empty");
        return new(decision, certificate, plan, settlement, terminal,
            settlement?.PendingOperations ?? plan?.Operations.Select(item => item.Identity).ToArray() ?? [],
            watermark);
    }
}
