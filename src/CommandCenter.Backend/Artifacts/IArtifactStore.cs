namespace CommandCenter.Backend.Artifacts;

public interface IArtifactStore
{
    Task<bool> ExistsAsync(string path);

    Task<string?> ReadAsync(string path);

    Task WriteAsync(string path, string content);

    Task DeleteAsync(string path);

    Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern);
}
