using System.Security.Cryptography;
using System.Text;
using LoopRelay.Core.Repositories;
using Dapper;
using LoopRelay.Persistence.Sqlite.Abstractions;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Persistence.Sqlite;

/// <summary>
/// Real per-FAMILY content-hash <see cref="ISourceFingerprintProvider"/> (Phase 2). It replaces the
/// fragile mtime probe with a deterministic content fingerprint: for each requested
/// <see cref="SourceFamily"/> it computes a cheap <c>(row_count, max_updated_at)</c> signature over the
/// family's source files and reuses the memoized content hash in the <c>source_fingerprint</c> table when
/// that signature is unchanged, re-hashing the actual file CONTENT only when the signature shifts. The
/// returned composite is a hash over the participating families' content hashes, so it is order-independent
/// in the families list and changes if-and-only-if some participating source content changes.
///
/// The fingerprint is a function of source CONTENT, never of mtime — touching a file without changing its
/// bytes (the documented self-invalidation hazard) yields the same fingerprint, closing the hazard that the
/// old mtime probe left open.
/// </summary>
public sealed class DefaultSourceFingerprintProvider : ISourceFingerprintProvider
{
    private readonly ISqliteConnectionFactory connectionFactory;
    private readonly TimeProvider timeProvider;

    public DefaultSourceFingerprintProvider(
        ISqliteConnectionFactory connectionFactory,
        TimeProvider timeProvider)
    {
        this.connectionFactory = connectionFactory;
        this.timeProvider = timeProvider;
    }

    public async Task<string> ForFamiliesAsync(
        Repository repo, IReadOnlyList<SourceFamily> families, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(families);

        await using SqliteConnection connection =
            await connectionFactory.OpenRepositoryConnectionAsync(repo, ct).ConfigureAwait(false);

        // Compute per-family content hashes in a stable family order so the composite is deterministic
        // regardless of the caller-supplied family ordering.
        var perFamily = new SortedDictionary<SourceFamily, string>();
        foreach (SourceFamily family in families)
        {
            perFamily[family] = await FingerprintFamilyAsync(connection, repo, family, ct).ConfigureAwait(false);
        }

        var builder = new StringBuilder();
        foreach ((SourceFamily family, string fingerprint) in perFamily)
        {
            builder.Append(family);
            builder.Append('=');
            builder.Append(fingerprint);
            builder.Append(';');
        }

        byte[] composite = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(composite);
    }

    private async Task<string> FingerprintFamilyAsync(
        SqliteConnection connection, Repository repo, SourceFamily family, CancellationToken ct)
    {
        FamilySignature signature = ComputeSignature(repo, family);

        // Cheap-compare gate: reuse the memoized content hash when the (row_count, max_updated_at) signature
        // is unchanged, so the expensive content re-hash only runs when the family's source actually shifts.
        SourceFingerprintRow? cached = await ReadCachedAsync(connection, repo.Id, family, ct).ConfigureAwait(false);
        if (cached is not null
            && cached.RowCount == signature.RowCount
            && string.Equals(cached.MaxUpdatedAt, signature.MaxUpdatedAt, StringComparison.Ordinal))
        {
            return cached.Fingerprint;
        }

        string fingerprint = ComputeContentHash(repo, family);
        await UpsertAsync(connection, repo.Id, family, fingerprint, signature, ct).ConfigureAwait(false);
        return fingerprint;
    }

    /// <summary>
    /// Cheap signature over the family's source files: file count plus the maximum last-write time. A change
    /// in either forces a content re-hash; an unchanged signature lets the memoized hash stand. The signature
    /// is NOT the fingerprint — content equality is always confirmed by the stored content hash.
    /// </summary>
    private static FamilySignature ComputeSignature(Repository repo, SourceFamily family)
    {
        long count = 0;
        DateTimeOffset? max = null;
        foreach (string file in EnumerateFamilyFiles(repo, family))
        {
            count++;
            DateTimeOffset writeTime = File.GetLastWriteTimeUtc(file);
            if (max is null || writeTime > max)
            {
                max = writeTime;
            }
        }

        return new FamilySignature(count, max?.UtcDateTime.ToString("O"));
    }

    /// <summary>
    /// Deterministic SHA-256 over the family's source CONTENT: each file contributes its repository-relative
    /// path and its raw bytes, ordered by relative path so the hash is independent of enumeration order.
    /// </summary>
    private static string ComputeContentHash(Repository repo, SourceFamily family)
    {
        using var sha = SHA256.Create();
        bool any = false;
        foreach (string file in EnumerateFamilyFiles(repo, family)
                     .OrderBy(path => ToRelative(repo, path), StringComparer.Ordinal))
        {
            any = true;
            byte[] header = Encoding.UTF8.GetBytes(ToRelative(repo, file) + "\n");
            sha.TransformBlock(header, 0, header.Length, null, 0);
            byte[] content = File.ReadAllBytes(file);
            sha.TransformBlock(content, 0, content.Length, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        // An absent/empty family still produces a stable, distinct fingerprint, so an empty source tree is a
        // legitimate (and equal-across-reads) state rather than an "unprobeable" one.
        string marker = any ? "content" : "empty";
        byte[] markerBytes = Encoding.UTF8.GetBytes(marker);
        byte[] combined = [.. sha.Hash!, .. markerBytes];
        return Convert.ToHexStringLower(SHA256.HashData(combined));
    }

    /// <summary>
    /// Maps a <see cref="SourceFamily"/> onto the repository-relative source locations whose content
    /// participates in its fingerprint, and enumerates every file beneath them. This mirrors the directories
    /// the old mtime probe scanned for the decision-session families, generalised per family.
    /// </summary>
    private static IEnumerable<string> EnumerateFamilyFiles(Repository repo, SourceFamily family)
    {
        string repoRoot = Path.GetFullPath(repo.Path);
        foreach (string relative in FamilyRoots(family))
        {
            string resolved = Path.GetFullPath(Path.Combine(repoRoot, relative));
            if (Directory.Exists(resolved))
            {
                foreach (string file in Directory.EnumerateFiles(resolved, "*", SearchOption.AllDirectories))
                {
                    yield return file;
                }
            }
            else if (File.Exists(resolved))
            {
                yield return resolved;
            }
        }

        // Operational-context rotates historical revisions as `operational_context.NNNN.md` files written
        // directly into the `.agents/` ROOT (NOT under `.agents/operational_context/`). The metrics evidence
        // reader consumes every one of these via ArtifactService.DiscoverAsync (the OperationalContext family),
        // so they MUST participate in the fingerprint — otherwise a rotated-revision content change leaves the
        // (row_count, max_updated_at) signature and content hash unchanged and the derived rebuild is wrongly
        // skipped (the exact blind spot the old mtime probe had). The glob also matches the current
        // `operational_context.md`, so it is the single source of truth for root-level operational-context files.
        if (family is SourceFamily.OperationalContext)
        {
            string agentsRoot = Path.GetFullPath(Path.Combine(repoRoot, ".agents"));
            if (Directory.Exists(agentsRoot))
            {
                foreach (string file in Directory.EnumerateFiles(
                    agentsRoot, "operational_context*.md", SearchOption.TopDirectoryOnly))
                {
                    yield return file;
                }
            }
        }
    }

    private static string[] FamilyRoots(SourceFamily family) => family switch
    {
        SourceFamily.Decisions => [".agents/decisions"],
        SourceFamily.Reasoning => [".agents/reasoning"],
        // Root-level `operational_context*.md` (current + rotated historicals) is handled by a dedicated glob
        // in EnumerateFamilyFiles; here we cover only the proposals subdirectory.
        SourceFamily.OperationalContext => [".agents/operational_context"],
        SourceFamily.Execution => [".agents/execution"],
        SourceFamily.Handoff => [".agents/handoffs"],
        SourceFamily.Git => [".agents/git"],
        SourceFamily.DecisionSession => [".agents/decision-sessions/registry.json"],
        _ => []
    };

    private static string ToRelative(Repository repo, string fullPath) =>
        Path.GetRelativePath(Path.GetFullPath(repo.Path), fullPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

    private static async Task<SourceFingerprintRow?> ReadCachedAsync(
        SqliteConnection connection, Guid repositoryId, SourceFamily family, CancellationToken ct)
    {
        var command = new CommandDefinition(
            """
            SELECT fingerprint AS Fingerprint, row_count AS RowCount, max_updated_at AS MaxUpdatedAt
            FROM source_fingerprint
            WHERE repository_id = @RepositoryId AND family = @Family;
            """,
            new { RepositoryId = repositoryId.ToString(), Family = family.ToString() },
            cancellationToken: ct);

        return await connection.QuerySingleOrDefaultAsync<SourceFingerprintRow>(command).ConfigureAwait(false);
    }

    private async Task UpsertAsync(
        SqliteConnection connection,
        Guid repositoryId,
        SourceFamily family,
        string fingerprint,
        FamilySignature signature,
        CancellationToken ct)
    {
        var command = new CommandDefinition(
            """
            INSERT INTO source_fingerprint
                (repository_id, family, fingerprint, row_count, max_updated_at, computed_at)
            VALUES
                (@RepositoryId, @Family, @Fingerprint, @RowCount, @MaxUpdatedAt, @ComputedAt)
            ON CONFLICT (repository_id, family) DO UPDATE SET
                fingerprint    = excluded.fingerprint,
                row_count      = excluded.row_count,
                max_updated_at = excluded.max_updated_at,
                computed_at    = excluded.computed_at;
            """,
            new
            {
                RepositoryId = repositoryId.ToString(),
                Family = family.ToString(),
                Fingerprint = fingerprint,
                RowCount = signature.RowCount,
                MaxUpdatedAt = signature.MaxUpdatedAt,
                ComputedAt = timeProvider.GetUtcNow().ToString("O")
            },
            cancellationToken: ct);

        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    private sealed record FamilySignature(long RowCount, string? MaxUpdatedAt);

    private sealed class SourceFingerprintRow
    {
        public string Fingerprint { get; init; } = string.Empty;

        public long RowCount { get; init; }

        public string? MaxUpdatedAt { get; init; }
    }
}
