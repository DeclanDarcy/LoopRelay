using System.Collections.Concurrent;
using System.Text.Json;
using CommandCenter.Persistence.Sqlite.Abstractions;

namespace CommandCenter.Persistence.Sqlite;

/// <summary>
/// In-memory <see cref="IDerivedSnapshotCache"/> test double, exactly analogous to
/// <c>MemoryArtifactStore</c> / <c>InMemoryConfigurationStore</c>. Keyed by <c>(repo, kind)</c> with
/// a single stored entry per key, it reproduces the real cache's invalidation semantics: a
/// <see cref="TryGetAsync{T}"/> is a HIT only when the supplied fingerprint AND formula version match
/// the stored entry; otherwise it is a miss (returns <see langword="null"/>), so stale-source and
/// formula-bump busting behave identically to the SQLite implementation. The payload is round-tripped
/// through <see cref="JsonSerializerOptions"/> so type-coercion behaves like the on-disk path.
/// </summary>
public sealed class MemorySqliteSnapshotCache : IDerivedSnapshotCache
{
    private static readonly JsonSerializerOptions DefaultPayloadOptions =
        new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<(Guid Repo, string Kind), Entry> entries = new();
    private readonly JsonSerializerOptions payloadOptions;

    public MemorySqliteSnapshotCache(JsonSerializerOptions? payloadOptions = null)
    {
        this.payloadOptions = payloadOptions ?? DefaultPayloadOptions;
    }

    public Task<T?> TryGetAsync<T>(
        Guid repo, string kind, string fingerprint, string formulaVersion, CancellationToken ct)
    {
        if (entries.TryGetValue((repo, kind), out Entry? entry)
            && entry.Fingerprint == fingerprint
            && entry.FormulaVersion == formulaVersion)
        {
            T? value = JsonSerializer.Deserialize<T>(entry.PayloadJson, payloadOptions);
            return Task.FromResult(value);
        }

        return Task.FromResult<T?>(default);
    }

    public Task PutAsync<T>(
        Guid repo, string kind, string fingerprint, string formulaVersion, T baseValue, CancellationToken ct)
    {
        string payloadJson = JsonSerializer.Serialize(baseValue, payloadOptions);
        entries[(repo, kind)] = new Entry(fingerprint, formulaVersion, payloadJson);
        return Task.CompletedTask;
    }

    private sealed record Entry(string Fingerprint, string FormulaVersion, string PayloadJson);
}
