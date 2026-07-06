using System.Text.Json;

namespace LoopRelay.Core.Configuration;

public sealed class ApplicationConfigurationStore : IApplicationConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string configurationPath;

    // Source-pure read cache: the last successfully deserialized configuration keyed by a content
    // signature (length, last-write-UTC ticks). The signature is a pure function of the source file's
    // identity on disk; it is NEVER keyed by wall-clock, so a cache hit can only occur when the bytes on
    // disk are unchanged since we materialized them. The cached ApplicationConfiguration is immutable
    // (sealed, init-only properties, with an immutable Repository list), so it is safe to alias to every
    // LoadAsync caller. This store is a process singleton and LoadAsync is the single read chokepoint for
    // repository lookups (a single /workflow resolves the repository a dozen-plus times), so the cache
    // collapses those repeated re-read + re-deserialize passes over unchanged JSON to one parse. Guarded by
    // a lock because the singleton is hit concurrently. SaveAsync primes/evicts so a mutation is never
    // masked by a stale entry.
    private readonly object cacheGate = new();
    private CacheEntry? cache;

    public ApplicationConfigurationStore()
        : this(Environment.GetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH") ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LoopRelay",
                "configuration.json"))
    {
    }

    public ApplicationConfigurationStore(string configurationPath)
    {
        this.configurationPath = configurationPath;
    }

    public async Task<ApplicationConfiguration> LoadAsync()
    {
        FileSignature? signatureBeforeRead = ReadSignature();
        if (signatureBeforeRead is null)
        {
            // No file on disk: nothing to cache, nothing to serve. Drop any prior entry so a delete that
            // happened out-of-band (not through SaveAsync) can never leave a stale cached value behind.
            ClearCache();
            return new ApplicationConfiguration();
        }

        // Fast path: if the current on-disk signature matches the cached one, the file is byte-for-byte
        // unchanged since we materialized it, so the cached immutable configuration is still correct.
        if (TryGetCached(signatureBeforeRead.Value, out ApplicationConfiguration? cached))
        {
            return cached;
        }

        ApplicationConfiguration result;
        await using (FileStream stream = File.OpenRead(configurationPath))
        {
            result = await JsonSerializer.DeserializeAsync<ApplicationConfiguration>(stream, SerializerOptions)
                ?? new ApplicationConfiguration();
        }

        // Re-stat after the read. Only cache when the file is identical before and after the read, so a
        // signature is only ever associated with bytes it actually describes (no torn-write poisoning). If
        // the file changed mid-read we skip caching and return what we read this time; the next Load re-stats
        // and re-reads, so stale data can never be served.
        FileSignature? signatureAfterRead = ReadSignature();
        if (signatureAfterRead is { } stable && stable.Equals(signatureBeforeRead.Value))
        {
            StoreCache(stable, result);
        }

        return result;
    }

    public async Task SaveAsync(ApplicationConfiguration configuration)
    {
        string? directory = Path.GetDirectoryName(configurationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Defensive copy so the cached value is an independent immutable snapshot that cannot be altered by
        // the caller after the fact (the caller still owns the Repositories list it passed in; we must not
        // alias it). Repository itself is immutable, so a shallow copy of the list is sufficient.
        var snapshot = new ApplicationConfiguration
        {
            Repositories = [.. configuration.Repositories]
        };

        await using (FileStream stream = File.Create(configurationPath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions);
        }

        // Prime the cache with the just-written data under the file's NEW signature. We re-stat rather than
        // trust mtime alone: two same-tick writes of equal length would otherwise leave the cache pointing at
        // the wrong snapshot. If the signature can't be read back (e.g. a concurrent delete/replace already
        // superseded it), evict instead so the next Load re-reads from disk rather than risk serving stale.
        FileSignature? signatureAfterWrite = ReadSignature();
        if (signatureAfterWrite is { } stable)
        {
            StoreCache(stable, snapshot);
        }
        else
        {
            ClearCache();
        }
    }

    private bool TryGetCached(FileSignature signature, out ApplicationConfiguration configuration)
    {
        lock (cacheGate)
        {
            if (cache is { } entry && entry.Signature.Equals(signature))
            {
                configuration = entry.Configuration;
                return true;
            }
        }

        configuration = new ApplicationConfiguration();
        return false;
    }

    private void StoreCache(FileSignature signature, ApplicationConfiguration configuration)
    {
        lock (cacheGate)
        {
            cache = new CacheEntry(signature, configuration);
        }
    }

    private void ClearCache()
    {
        lock (cacheGate)
        {
            cache = null;
        }
    }

    private FileSignature? ReadSignature()
    {
        FileInfo info = new(configurationPath);
        if (!info.Exists)
        {
            return null;
        }

        return new FileSignature(configurationPath, info.Length, info.LastWriteTimeUtc.Ticks);
    }

    private readonly record struct FileSignature(string Path, long Length, long LastWriteUtcTicks);

    private sealed record CacheEntry(FileSignature Signature, ApplicationConfiguration Configuration);
}
