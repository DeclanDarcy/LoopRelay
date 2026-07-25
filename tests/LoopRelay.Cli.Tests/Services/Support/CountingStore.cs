using LoopRelay.Core.Abstractions.Artifacts;

namespace LoopRelay.Cli.Tests.Services.Support;


/// <summary>
/// IArtifactStore decorator that forwards to an inner store and counts ReadAsync / ListAsync / WriteAsync
/// calls (the latter per-path), so tests can prove the gate's short-circuit skipped all I/O on an
/// unchanged-and-incomplete epic, or that a redundant write to a given path was eliminated.
/// </summary>
internal sealed class CountingStore(IArtifactStore inner) : IArtifactStore
{
    private readonly Dictionary<string, int> writesByPath = new();

    public int Reads { get; private set; }

    public int Lists { get; private set; }

    public int Writes { get; private set; }

    public int WritesTo(string path) => writesByPath.TryGetValue(path, out int count) ? count : 0;

    public Task<bool> ExistsAsync(string path) => inner.ExistsAsync(path);

    public Task<string?> ReadAsync(string path)
    {
        Reads++;
        return inner.ReadAsync(path);
    }

    public Task WriteAsync(string path, string content)
    {
        Writes++;
        writesByPath[path] = writesByPath.GetValueOrDefault(path) + 1;
        return inner.WriteAsync(path, content);
    }

    public Task DeleteAsync(string path) => inner.DeleteAsync(path);

    public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern)
    {
        Lists++;
        return inner.ListAsync(path, searchPattern);
    }

    public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) => inner.ListDirectoriesAsync(path);
}
