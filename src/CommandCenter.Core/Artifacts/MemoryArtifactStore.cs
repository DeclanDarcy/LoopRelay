using System.Collections.Concurrent;

namespace CommandCenter.Core.Artifacts;

public sealed class MemoryArtifactStore : IArtifactStore
{
    private readonly ConcurrentDictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> ExistsAsync(string path)
    {
        return Task.FromResult(files.ContainsKey(Normalize(path)));
    }

    public Task<string?> ReadAsync(string path)
    {
        files.TryGetValue(Normalize(path), out string? content);
        return Task.FromResult(content);
    }

    public Task<T?> ReadAs<T>(string path, Func<string, T?> deserialize)
    {
        // No caching layer here (this store holds raw strings only); deserialize on every read so the behaviour
        // is identical to ReadAsync followed by the caller's deserialize, including propagating a throwing delegate.
        return Task.FromResult(files.TryGetValue(Normalize(path), out string? content) ? deserialize(content) : default);
    }

    public Task WriteAsync(string path, string content)
    {
        files[Normalize(path)] = content;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path)
    {
        files.TryRemove(Normalize(path), out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern)
    {
        string prefix = Normalize(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string[] matches = files.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Where(key => GlobMatches(Path.GetFileName(key), searchPattern))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(matches);
    }

    public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path)
    {
        string prefix = Normalize(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string[] directories = files.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(key =>
            {
                string remainder = key[prefix.Length..];
                int separator = remainder.IndexOf(Path.DirectorySeparatorChar);
                return separator < 0 ? null : prefix + remainder[..separator];
            })
            .Where(directory => directory is not null)
            .Select(directory => directory!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(directories);
    }

    private static string Normalize(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static bool GlobMatches(string fileName, string searchPattern)
    {
        if (searchPattern == "*")
        {
            return true;
        }

        if (searchPattern.StartsWith("*.", StringComparison.Ordinal))
        {
            return fileName.EndsWith(searchPattern[1..], StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(fileName, searchPattern, StringComparison.OrdinalIgnoreCase);
    }
}
