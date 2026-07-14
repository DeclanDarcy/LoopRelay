using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Persistence;

namespace LoopRelay.Cli.Services.Cli;

internal sealed record LedgerEvidenceMatch(
    string Source,
    string Identity,
    string Content);

/// <summary>
/// Resolves receipt-consumed content back out of the evidence ledger by its sha256: system-owned
/// bodies (for example the adversarial review under gitignored <c>.LoopRelay/evidence</c>) are
/// captured as raw prompt output in <c>canonical_transition_evidence</c>, and rotated
/// decision/handoff/delta bodies live in <c>loop_history</c>. A read receipt's file hash is
/// therefore retrievable exactly as consumed without the file surviving on disk.
/// </summary>
internal static class LedgerEvidenceRetrieval
{
    public static async Task<LedgerEvidenceMatch?> TryResolveContentByHashAsync(
        Repository repository,
        string sha256,
        CancellationToken cancellationToken)
    {
        CanonicalLedgerEvidenceMatch? match = await new CanonicalLedgerEvidenceProjection()
            .TryResolveContentByHashAsync(repository, sha256, cancellationToken);
        return match is null ? null : new LedgerEvidenceMatch(match.Source, match.Identity, match.Content);
    }
}
