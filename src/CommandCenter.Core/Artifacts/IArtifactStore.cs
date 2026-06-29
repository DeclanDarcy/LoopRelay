namespace CommandCenter.Core.Artifacts;

public interface IArtifactStore
{
    Task<bool> ExistsAsync(string path);

    Task<string?> ReadAsync(string path);

    /// <summary>
    /// Reads the artifact at <paramref name="path"/> and projects it through <paramref name="deserialize"/>,
    /// returning <c>default(T)</c> when the file is absent. Behaviourally equivalent to reading the string via
    /// <see cref="ReadAsync(string)"/> and invoking <paramref name="deserialize"/> on it (a throwing delegate
    /// propagates), but lets an implementation cache the already-materialized graph for an unchanged file so a
    /// repeated read skips re-deserialization. The deserialized graph MUST be immutable: callers must never mutate
    /// a returned value, because an implementation may alias the same instance to subsequent callers.
    /// The default implementation simply reads the string and deserializes it with no caching, so existing
    /// implementers (e.g. test doubles) keep working unchanged; caching implementations override it.
    /// </summary>
    async Task<T?> ReadAs<T>(string path, Func<string, T?> deserialize)
    {
        string? content = await ReadAsync(path);
        return content is null ? default : deserialize(content);
    }

    Task WriteAsync(string path, string content);

    Task DeleteAsync(string path);

    Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern);

    Task<IReadOnlyList<string>> ListDirectoriesAsync(string path);
}
