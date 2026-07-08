using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Cli;
using Xunit;

namespace LoopRelay.Cli.Tests;


/// <summary>
/// IArtifactStore decorator that forwards to an inner store and counts ReadAsync / ListAsync calls,
/// so tests can prove the gate's short-circuit skipped all I/O on an unchanged-and-incomplete epic.
/// </summary>
internal sealed class CountingStore(IArtifactStore inner) : IArtifactStore
{
    public int Reads { get; private set; }

    public int Lists { get; private set; }

    public Task<bool> ExistsAsync(string path) => inner.ExistsAsync(path);

    public Task<string?> ReadAsync(string path)
    {
        Reads++;
        return inner.ReadAsync(path);
    }

    public Task WriteAsync(string path, string content) => inner.WriteAsync(path, content);

    public Task DeleteAsync(string path) => inner.DeleteAsync(path);

    public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern)
    {
        Lists++;
        return inner.ListAsync(path, searchPattern);
    }

    public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) => inner.ListDirectoriesAsync(path);
}
