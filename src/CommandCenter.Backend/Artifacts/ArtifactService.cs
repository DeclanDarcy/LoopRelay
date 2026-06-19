using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Artifacts;

public sealed class ArtifactService(IArtifactStore artifactStore) : IArtifactService
{
    private const string AgentsDirectory = ".agents";

    public async Task<IReadOnlyList<Artifact>> DiscoverAsync(Repository repository)
    {
        var artifacts = new List<Artifact>();

        await AddStaticArtifactAsync(artifacts, repository, "plan.md", ArtifactType.Plan, ArtifactFamily.Plan);
        await AddStaticArtifactAsync(artifacts, repository, "operational_context.md", ArtifactType.OperationalContext, ArtifactFamily.OperationalContext);
        await AddDirectoryArtifactsAsync(artifacts, repository, "milestones", ArtifactType.Milestone, ArtifactFamily.Milestone);
        await AddDirectoryArtifactsAsync(artifacts, repository, "handoffs", ArtifactType.Handoff, ArtifactFamily.Handoff);
        await AddDirectoryArtifactsAsync(artifacts, repository, "decisions", ArtifactType.Decision, ArtifactFamily.Decision);

        return artifacts;
    }

    public async Task<Artifact?> GetCurrentHandoffAsync(Repository repository)
    {
        const string relativePath = ".agents/handoffs/handoff.md";
        return await ExistsAsync(repository, relativePath)
            ? CreateArtifact(repository, ResolveRepositoryPath(repository, relativePath), ArtifactType.Handoff, ArtifactFamily.Handoff)
            : null;
    }

    public async Task<Artifact?> GetCurrentDecisionsAsync(Repository repository)
    {
        const string relativePath = ".agents/decisions/decisions.md";
        return await ExistsAsync(repository, relativePath)
            ? CreateArtifact(repository, ResolveRepositoryPath(repository, relativePath), ArtifactType.Decision, ArtifactFamily.Decision)
            : null;
    }

    public async Task<bool> ExistsAsync(Repository repository, string relativePath)
    {
        return await artifactStore.ExistsAsync(ResolveRepositoryPath(repository, relativePath));
    }

    public async Task<string> LoadAsync(Repository repository, string relativePath)
    {
        var content = await artifactStore.ReadAsync(ResolveRepositoryPath(repository, relativePath));
        return content ?? throw new FileNotFoundException("Artifact was not found.", relativePath);
    }

    public Task SaveAsync(Repository repository, string relativePath, string content)
    {
        return artifactStore.WriteAsync(ResolveRepositoryPath(repository, relativePath), content);
    }

    private async Task AddStaticArtifactAsync(
        List<Artifact> artifacts,
        Repository repository,
        string fileName,
        ArtifactType type,
        ArtifactFamily family)
    {
        var relativePath = CombineRelative(AgentsDirectory, fileName);
        var fullPath = ResolveRepositoryPath(repository, relativePath);
        if (await artifactStore.ExistsAsync(fullPath))
        {
            artifacts.Add(CreateArtifact(repository, fullPath, type, family));
        }
    }

    private async Task AddDirectoryArtifactsAsync(
        List<Artifact> artifacts,
        Repository repository,
        string directoryName,
        ArtifactType type,
        ArtifactFamily family)
    {
        var directoryPath = ResolveRepositoryPath(repository, CombineRelative(AgentsDirectory, directoryName));
        var files = await artifactStore.ListAsync(directoryPath, "*.md");
        artifacts.AddRange(files.Select(file => CreateArtifact(repository, file, type, family)));
    }

    private static Artifact CreateArtifact(Repository repository, string fullPath, ArtifactType type, ArtifactFamily family)
    {
        return new Artifact
        {
            RelativePath = ToRepositoryRelativePath(repository, fullPath),
            Name = Path.GetFileName(fullPath),
            Type = type,
            Family = family,
            VersionKind = DetermineVersionKind(family, Path.GetFileName(fullPath))
        };
    }

    private static ArtifactVersionKind DetermineVersionKind(ArtifactFamily family, string fileName)
    {
        if (family == ArtifactFamily.Handoff)
        {
            return string.Equals(fileName, "handoff.md", StringComparison.OrdinalIgnoreCase)
                ? ArtifactVersionKind.Current
                : ArtifactVersionKind.Historical;
        }

        if (family == ArtifactFamily.Decision)
        {
            return string.Equals(fileName, "decisions.md", StringComparison.OrdinalIgnoreCase)
                ? ArtifactVersionKind.Current
                : ArtifactVersionKind.Historical;
        }

        return ArtifactVersionKind.Current;
    }

    private static string ResolveRepositoryPath(Repository repository, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Artifact path must be repository-relative.", nameof(relativePath));
        }

        var repositoryRoot = Path.GetFullPath(repository.Path);
        var resolvedPath = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath));
        var rootWithSeparator = repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!resolvedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(resolvedPath, repositoryRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Artifact path must stay within the repository root.", nameof(relativePath));
        }

        return resolvedPath;
    }

    private static string ToRepositoryRelativePath(Repository repository, string fullPath)
    {
        return Path.GetRelativePath(Path.GetFullPath(repository.Path), Path.GetFullPath(fullPath))
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string CombineRelative(params string[] parts)
    {
        return string.Join('/', parts);
    }
}
