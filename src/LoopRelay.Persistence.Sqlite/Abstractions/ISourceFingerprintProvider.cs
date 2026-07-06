using LoopRelay.Core.Repositories;

namespace LoopRelay.Persistence.Sqlite.Abstractions;

/// <summary>
/// Computes a deterministic content fingerprint over a set of source-of-truth evidence families
/// for a repository. Replaces the fragile mtime probe and the "run the whole projection just to
/// get its fingerprint" anti-pattern: the validity check becomes a cheap compare BEFORE any
/// expensive derivation. The fingerprint is a function of source CONTENT (and per-family
/// row-count / max-updated-at), never of file mtime.
/// </summary>
public interface ISourceFingerprintProvider
{
    /// <summary>
    /// Returns a composite fingerprint over the supplied <paramref name="families"/> for
    /// <paramref name="repo"/>. The same source content yields the same fingerprint; any content
    /// change in a participating family changes it.
    /// </summary>
    Task<string> ForFamiliesAsync(
        Repository repo, IReadOnlyList<SourceFamily> families, CancellationToken ct);
}
