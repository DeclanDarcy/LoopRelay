using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;

namespace CommandCenter.Execution.Services;

public sealed class FileSystemExecutionSessionStore : IExecutionSessionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string storePath;

    // Source-pure derivation cache: the last successfully materialized result keyed by a content
    // signature (path, length, last-write-UTC ticks). The signature is a pure function of the source
    // file's identity on disk; it is NEVER keyed by wall-clock, so a cache hit can only occur when the
    // bytes on disk are unchanged. Guarded by a lock because the store is a singleton hit concurrently.
    private readonly object cacheGate = new();
    private CacheEntry? cache;

    public FileSystemExecutionSessionStore()
        : this(Environment.GetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH") ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CommandCenter",
                "execution-sessions.json"))
    {
    }

    public FileSystemExecutionSessionStore(string storePath)
    {
        this.storePath = storePath;
    }

    public async Task<IReadOnlyList<ExecutionSession>> LoadAsync()
    {
        FileSignature? signatureBeforeRead = ReadSignature();
        if (signatureBeforeRead is null)
        {
            // No file on disk: nothing to cache, nothing to serve.
            return [];
        }

        // Fast path: if the current on-disk signature matches the cached one, the file is byte-for-byte
        // unchanged since we materialized it, so the cached immutable result is still correct.
        if (TryGetCached(signatureBeforeRead.Value, out IReadOnlyList<ExecutionSession>? cached))
        {
            return cached;
        }

        IReadOnlyList<ExecutionSession> result;
        await using (FileStream stream = File.OpenRead(storePath))
        {
            ExecutionSession[]? deserialized =
                await JsonSerializer.DeserializeAsync<ExecutionSession[]>(stream, SerializerOptions);
            result = deserialized ?? [];
        }

        // Re-stat after the read. Only cache when the file is identical before and after the read, so a
        // signature is only ever associated with bytes it actually describes (no torn-write poisoning).
        // If the file changed mid-read we simply skip caching and return what we read this time; the next
        // Load re-stats and re-reads, so stale data can never be served.
        FileSignature? signatureAfterRead = ReadSignature();
        if (signatureAfterRead is { } stable && stable.Equals(signatureBeforeRead.Value))
        {
            StoreCache(stable, result);
        }

        return result;
    }

    public async Task SaveAsync(IReadOnlyList<ExecutionSession> sessions)
    {
        string? directory = Path.GetDirectoryName(storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Defensive copy so the cached value is an independent immutable snapshot the caller cannot
        // mutate after the fact (the caller still owns its IReadOnlyList; we must not alias it).
        ExecutionSession[] snapshot = [.. sessions];

        await using (FileStream stream = File.Create(storePath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions);
        }

        // Prime the cache with the just-written data under the file's NEW signature. We re-stat rather
        // than trust mtime alone: two same-tick writes of equal length would otherwise leave the cache
        // pointing at the wrong snapshot. If the signature can't be read back, drop the cache instead so
        // the next Load re-reads from disk rather than risk serving anything stale.
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

    private bool TryGetCached(FileSignature signature, out IReadOnlyList<ExecutionSession> result)
    {
        lock (cacheGate)
        {
            if (cache is { } entry && entry.Signature.Equals(signature))
            {
                result = entry.Sessions;
                return true;
            }
        }

        result = [];
        return false;
    }

    private void StoreCache(FileSignature signature, IReadOnlyList<ExecutionSession> sessions)
    {
        lock (cacheGate)
        {
            cache = new CacheEntry(signature, sessions);
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
        FileInfo info = new(storePath);
        if (!info.Exists)
        {
            return null;
        }

        return new FileSignature(storePath, info.Length, info.LastWriteTimeUtc.Ticks);
    }

    private readonly record struct FileSignature(string Path, long Length, long LastWriteUtcTicks);

    private sealed record CacheEntry(FileSignature Signature, IReadOnlyList<ExecutionSession> Sessions);
}
