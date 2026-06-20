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
            ? CreateArtifact(repository, ArtifactPath.ResolveRepositoryPath(repository, relativePath), ArtifactType.Handoff, ArtifactFamily.Handoff)
            : null;
    }

    public async Task<Artifact?> GetCurrentOperationalContextAsync(Repository repository)
    {
        const string relativePath = ".agents/operational_context.md";
        return await ExistsAsync(repository, relativePath)
            ? CreateArtifact(repository, ArtifactPath.ResolveRepositoryPath(repository, relativePath), ArtifactType.OperationalContext, ArtifactFamily.OperationalContext)
            : null;
    }

    public async Task<Artifact?> GetCurrentDecisionsAsync(Repository repository)
    {
        const string relativePath = ".agents/decisions/decisions.md";
        return await ExistsAsync(repository, relativePath)
            ? CreateArtifact(repository, ArtifactPath.ResolveRepositoryPath(repository, relativePath), ArtifactType.Decision, ArtifactFamily.Decision)
            : null;
    }

    public async Task<bool> ExistsAsync(Repository repository, string relativePath)
    {
        return await artifactStore.ExistsAsync(ArtifactPath.ResolveRepositoryPath(repository, relativePath));
    }

    public async Task<string> LoadAsync(Repository repository, string relativePath)
    {
        var content = await artifactStore.ReadAsync(ArtifactPath.ResolveRepositoryPath(repository, relativePath));
        return content ?? throw new FileNotFoundException("Artifact was not found.", relativePath);
    }

    public Task SaveAsync(Repository repository, string relativePath, string content)
    {
        return artifactStore.WriteAsync(ArtifactPath.ResolveRepositoryPath(repository, relativePath), content);
    }

    private async Task AddStaticArtifactAsync(
        List<Artifact> artifacts,
        Repository repository,
        string fileName,
        ArtifactType type,
        ArtifactFamily family)
    {
        var relativePath = ArtifactPath.CombineRelative(AgentsDirectory, fileName);
        var fullPath = ArtifactPath.ResolveRepositoryPath(repository, relativePath);
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
        var directoryPath = ArtifactPath.ResolveRepositoryPath(repository, ArtifactPath.CombineRelative(AgentsDirectory, directoryName));
        var files = await artifactStore.ListAsync(directoryPath, "*.md");
        artifacts.AddRange(files
            .Where(file => ShouldIncludeDirectoryArtifact(family, Path.GetFileName(file)))
            .Select(file => CreateArtifact(repository, file, type, family)));
    }

    private static Artifact CreateArtifact(Repository repository, string fullPath, ArtifactType type, ArtifactFamily family)
    {
        return new Artifact
        {
            RelativePath = ArtifactPath.ToRepositoryRelativePath(repository, fullPath),
            Name = Path.GetFileName(fullPath),
            Type = type,
            Family = family,
            VersionKind = DetermineVersionKind(family, Path.GetFileName(fullPath))
        };
    }

    private static bool ShouldIncludeDirectoryArtifact(ArtifactFamily family, string fileName)
    {
        return family switch
        {
            ArtifactFamily.Handoff => IsCurrentOrHistorical(fileName, "handoff"),
            ArtifactFamily.Decision => IsCurrentOrHistorical(fileName, "decisions"),
            _ => true
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

    private static bool IsCurrentOrHistorical(string fileName, string baseName)
    {
        if (string.Equals(fileName, $"{baseName}.md", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = $"{baseName}.";
        const string suffix = ".md";

        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var sequenceText = fileName[prefix.Length..^suffix.Length];
        return sequenceText.Length == 4 &&
            sequenceText.All(char.IsDigit) &&
            int.TryParse(sequenceText, out var sequence) &&
            sequence > 0;
    }
}
