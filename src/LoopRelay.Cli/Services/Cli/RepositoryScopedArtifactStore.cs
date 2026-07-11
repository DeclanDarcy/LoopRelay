using LoopRelay.Core.Abstractions.Artifacts;

namespace LoopRelay.Cli.Services.Cli;

internal sealed class RepositoryScopedArtifactStore(
    IArtifactStore inner,
    string repositoryRoot) : IArtifactStore
{
    private readonly string root = Path.GetFullPath(repositoryRoot);

    public Task<bool> ExistsAsync(string path) => inner.ExistsAsync(Resolve(path));

    public Task<string?> ReadAsync(string path) => inner.ReadAsync(Resolve(path));

    public Task<T?> ReadAs<T>(string path, Func<string, T?> deserialize) =>
        inner.ReadAs(Resolve(path), deserialize);

    public Task WriteAsync(string path, string content) => inner.WriteAsync(Resolve(path), content);

    public Task DeleteAsync(string path) => inner.DeleteAsync(Resolve(path));

    public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern) =>
        inner.ListAsync(Resolve(path), searchPattern);

    public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) =>
        inner.ListDirectoriesAsync(Resolve(path));

    private string Resolve(string path)
    {
        string candidate = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
        string relative = Path.GetRelativePath(root, candidate);
        if (Path.IsPathRooted(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Artifact path `{path}` escaped repository root `{root}`.");
        }

        return candidate;
    }
}
