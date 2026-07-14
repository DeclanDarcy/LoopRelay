using LoopRelay.Core.Abstractions.Artifacts;

namespace LoopRelay.Cli.Tests.Services.Support;


/// <summary>Forwards to an inner store but throws when a write targets the .agents/deltas archive — models a
/// failed delta archive so the strict transfer-fail path can be exercised.</summary>
internal sealed class ThrowOnDeltaArchiveStore(IArtifactStore inner) : IArtifactStore
{
    public Task<bool> ExistsAsync(string path) => inner.ExistsAsync(path);
    public Task<string?> ReadAsync(string path) => inner.ReadAsync(path);
    public Task WriteAsync(string path, string content) =>
        path.Replace('\\', '/').Contains("/deltas/", StringComparison.OrdinalIgnoreCase)
            ? throw new IOException("Configured archive write failure.")
            : inner.WriteAsync(path, content);
    public Task DeleteAsync(string path) => inner.DeleteAsync(path);
    public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern) => inner.ListAsync(path, searchPattern);
    public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) => inner.ListDirectoriesAsync(path);
}
