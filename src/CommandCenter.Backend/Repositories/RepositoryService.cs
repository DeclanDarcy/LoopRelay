using CommandCenter.Backend.Configuration;

namespace CommandCenter.Backend.Repositories;

public sealed class RepositoryService(IApplicationConfigurationStore configurationStore) : IRepositoryService
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    public async Task<IReadOnlyList<Repository>> GetAllAsync()
    {
        return (await configurationStore.LoadAsync()).Repositories;
    }

    public async Task<Repository> RegisterAsync(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            throw new ArgumentException("Repository path is required.", nameof(repositoryPath));
        }

        var normalizedPath = NormalizePath(repositoryPath);

        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException($"Repository path does not exist: {normalizedPath}");
        }

        if (!ContainsGitDirectory(normalizedPath))
        {
            throw new InvalidOperationException($"Directory is not a Git repository: {normalizedPath}");
        }

        var configuration = await configurationStore.LoadAsync();
        if (configuration.Repositories.Any(repository => PathComparer.Equals(NormalizePath(repository.Path), normalizedPath)))
        {
            throw new InvalidOperationException($"Repository is already registered: {normalizedPath}");
        }

        Directory.CreateDirectory(Path.Combine(normalizedPath, ".agents"));

        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = new DirectoryInfo(normalizedPath).Name,
            Path = normalizedPath
        };

        await configurationStore.SaveAsync(new ApplicationConfiguration
        {
            Repositories = [.. configuration.Repositories, repository]
        });

        return repository;
    }

    public async Task RemoveAsync(Guid repositoryId)
    {
        var configuration = await configurationStore.LoadAsync();
        var repositories = configuration.Repositories
            .Where(repository => repository.Id != repositoryId)
            .ToArray();

        await configurationStore.SaveAsync(new ApplicationConfiguration
        {
            Repositories = repositories
        });
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool ContainsGitDirectory(string repositoryPath)
    {
        var gitPath = Path.Combine(repositoryPath, ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }
}
