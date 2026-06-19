using System.Collections.Concurrent;

namespace CommandCenter.Backend.Artifacts;

public sealed class MemoryArtifactStore : IArtifactStore
{
    private readonly ConcurrentDictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> ExistsAsync(string path)
    {
        return Task.FromResult(files.ContainsKey(Normalize(path)));
    }

    public Task<string?> ReadAsync(string path)
    {
        files.TryGetValue(Normalize(path), out var content);
        return Task.FromResult(content);
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
        var prefix = Normalize(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var matches = files.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Where(key => GlobMatches(Path.GetFileName(key), searchPattern))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(matches);
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
