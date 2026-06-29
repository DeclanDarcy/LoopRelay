using System.Collections.Concurrent;
using System.Text;

namespace CommandCenter.Core.Artifacts;

public sealed class FileSystemArtifactStore : IArtifactStore
{
    // UTF-8 without a BOM, matching File.WriteAllTextAsync's default encoding so the on-disk bytes are
    // byte-identical to the prior naive write (no contract/golden drift).
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    // Source-pure read cache: the last successfully read content of each path keyed by a content signature
    // (length, last-write-UTC ticks). The signature is a pure function of the file's identity on disk and is
    // NEVER keyed by wall-clock, so a cache hit can only occur when the bytes on disk are unchanged since we
    // materialized them. The cached value is an immutable string. This is the single read chokepoint for every
    // repository-scoped artifact: all FileSystem*Repository / ArtifactService reads funnel through ReadAsync,
    // and this store is a process singleton, so the cache spans requests and eliminates the per-request re-read
    // + re-deserialize of unchanged JSON/markdown when a repository is selected (~23 GETs hitting the same files).
    // Keyed per-path (ConcurrentDictionary) because, unlike the single-file execution-session store, this store
    // serves many files. The write/delete paths prime/evict so a mutation is never masked by a stale entry.
    private readonly ConcurrentDictionary<string, CacheEntry> readCache = new(StringComparer.Ordinal);

    public Task<bool> ExistsAsync(string path)
    {
        return Task.FromResult(File.Exists(path) || Directory.Exists(path));
    }

    public async Task<string?> ReadAsync(string path)
    {
        FileSignature? signatureBeforeRead = ReadSignature(path);
        if (signatureBeforeRead is null)
        {
            // No file on disk: nothing to cache, nothing to serve. Drop any prior entry so a delete that
            // happened out-of-band (not through DeleteAsync) can never leave a stale cached value behind.
            readCache.TryRemove(path, out _);
            return null;
        }

        // Fast path: if the current on-disk signature matches the cached one, the file is byte-for-byte
        // unchanged since we materialized it, so the cached immutable string is still correct.
        if (readCache.TryGetValue(path, out CacheEntry cached) &&
            cached.Signature.Equals(signatureBeforeRead.Value))
        {
            return cached.Content;
        }

        // Open with FileShare.ReadWrite | FileShare.Delete so a concurrent atomic WriteAsync (which renames/replaces
        // a temp file over this destination) can supersede the file while this read is in flight. The reader always
        // observes a single, complete file — its handle is bound to whichever version (old or new) existed when it
        // opened; it never sees a torn/partial mix. The atomic replace (Win32 ReplaceFile) briefly opens the target
        // exclusively, so a read that races that instant can transiently fail with a sharing violation; those are
        // transient, so we retry with a short backoff until the file can be opened (or is genuinely gone).
        const int maxAttempts = 100;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                string content;
                await using (var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: true))
                {
                    content = await reader.ReadToEndAsync();
                }

                // Re-stat after the read. Only cache when the file is identical before and after the read, so a
                // signature is only ever associated with bytes it actually describes (no torn-write poisoning). If
                // the file changed mid-read we skip caching and return what we read this time; the next read re-stats
                // and re-reads, so stale data can never be served.
                FileSignature? signatureAfterRead = ReadSignature(path);
                if (signatureAfterRead is { } stable && stable.Equals(signatureBeforeRead.Value))
                {
                    readCache[path] = new CacheEntry(stable, content);
                }

                return content;
            }
            catch (FileNotFoundException)
            {
                // Raced with a delete between the Exists check and the open: treat as absent (File.Exists==false).
                readCache.TryRemove(path, out _);
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                readCache.TryRemove(path, out _);
                return null;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException && attempt < maxAttempts)
            {
                // Transient sharing/access violation while a concurrent writer atomically replaces the file; retry.
                await Task.Delay(5);
            }
        }
    }

    public async Task WriteAsync(string path, string content)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Atomic write: a naive File.WriteAllTextAsync truncates the destination and streams bytes into it, so a
        // concurrent reader can observe a torn/partial file. Instead, write the full payload to a uniquely-named
        // temp file in the SAME directory (same volume, so File.Move is a rename, not a copy), then atomically
        // rename it over the destination. A reader therefore only ever sees the previous complete file or the new
        // complete file — never a partial one. The temp is cleaned up on any failure. Encoding is UTF-8 without a
        // BOM, byte-identical to the prior File.WriteAllTextAsync default.
        string targetDirectory = string.IsNullOrWhiteSpace(directory)
            ? Directory.GetCurrentDirectory()
            : directory;
        string temporaryPath = Path.Combine(
            targetDirectory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, Utf8NoBom);
            await AtomicReplaceAsync(temporaryPath, path);
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw;
        }

        // Prime the cache with the just-written content under the file's NEW signature. We re-stat rather than
        // trust mtime alone: two same-tick writes of equal length would otherwise leave the cache pointing at the
        // wrong content. If the signature can't be read back (e.g. a concurrent delete/replace already superseded
        // it), evict instead so the next read re-reads from disk rather than risk serving anything stale.
        FileSignature? signatureAfterWrite = ReadSignature(path);
        if (signatureAfterWrite is { } stable)
        {
            readCache[path] = new CacheEntry(stable, content);
        }
        else
        {
            readCache.TryRemove(path, out _);
        }
    }

    /// <summary>
    /// Renames <paramref name="temporaryPath"/> over <paramref name="destinationPath"/> as a single atomic
    /// operation (a reader sees only the old or new complete file, never a torn one). On Windows, an in-flight
    /// reader or a racing concurrent writer can make the overwrite-rename transiently fail with a sharing
    /// violation (<see cref="IOException"/>/<see cref="UnauthorizedAccessException"/>); these are transient, so we
    /// retry with a short backoff. The rename itself is never partial — it either fully succeeds or fully fails.
    /// </summary>
    private static async Task AtomicReplaceAsync(string temporaryPath, string destinationPath)
    {
        const int maxAttempts = 100;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                if (File.Exists(destinationPath))
                {
                    // File.Replace maps to the Win32 ReplaceFile API — an atomic in-place replacement designed to
                    // tolerate open readers (with FileShare.Delete), unlike File.Move(overwrite) which can hit
                    // ERROR_ACCESS_DENIED while the destination is being concurrently superseded.
                    File.Replace(temporaryPath, destinationPath, destinationBackupFileName: null,
                        ignoreMetadataErrors: true);
                }
                else
                {
                    // First write: a plain rename atomically publishes the file (no existing destination to replace).
                    File.Move(temporaryPath, destinationPath);
                }

                return;
            }
            catch (FileNotFoundException) when (attempt < maxAttempts)
            {
                // A racing writer replaced/removed the destination between our Exists check and Replace; retry,
                // which will re-evaluate Exists and take the Move-or-Replace branch appropriately.
                await Task.Delay(5);
            }
            catch (Exception exception) when (
                (exception is IOException or UnauthorizedAccessException) && attempt < maxAttempts)
            {
                // Transient sharing violation against a concurrent reader/writer; back off briefly and retry.
                await Task.Delay(5);
            }
        }
    }

    private static bool IsAtomicWriteTemporaryFile(string path)
    {
        string name = Path.GetFileName(path);
        return name.StartsWith('.') && name.EndsWith(".tmp", StringComparison.Ordinal);
    }

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup: a leftover temp file is harmless (it is uniquely named and never read).
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup: see above.
        }
    }

    public Task DeleteAsync(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        // Evict so a read-after-delete can never be served the deleted file's stale content.
        readCache.TryRemove(path, out _);
        return Task.CompletedTask;
    }

    private static FileSignature? ReadSignature(string path)
    {
        FileInfo info = new(path);
        if (!info.Exists)
        {
            return null;
        }

        return new FileSignature(info.Length, info.LastWriteTimeUtc.Ticks);
    }

    private readonly record struct FileSignature(long Length, long LastWriteUtcTicks);

    private readonly record struct CacheEntry(FileSignature Signature, string Content);

    public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern)
    {
        if (!Directory.Exists(path))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        string[] files = Directory.GetFiles(path, searchPattern)
            // Exclude the in-flight atomic-write temp files (`.{name}.{guid}.tmp`) so a listing that races a
            // concurrent WriteAsync never observes the transient temp before it is renamed over the destination.
            .Where(file => !IsAtomicWriteTemporaryFile(file))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        string[] directories = Directory.GetDirectories(path)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(directories);
    }
}
