namespace CommandCenter.Backend.Artifacts;

public sealed class FileSystemArtifactStore : IArtifactStore
{
    public Task<bool> ExistsAsync(string path)
    {
        return Task.FromResult(File.Exists(path) || Directory.Exists(path));
    }

    public async Task<string?> ReadAsync(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path);
    }

    public async Task WriteAsync(string path, string content)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content);
    }

    public Task DeleteAsync(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern)
    {
        if (!Directory.Exists(path))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        string[] files = Directory.GetFiles(path, searchPattern)
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
