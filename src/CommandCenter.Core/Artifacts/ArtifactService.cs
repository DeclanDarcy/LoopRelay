using CommandCenter.Core.Repositories;

namespace CommandCenter.Core.Artifacts;

public sealed class ArtifactService(IArtifactStore artifactStore) : IArtifactService
{
    private const string AgentsDirectory = ".agents";

    public async Task<IReadOnlyList<Artifact>> DiscoverAsync(Repository repository)
    {
        var artifacts = new List<Artifact>();

        await AddStaticArtifactAsync(artifacts, repository, "plan.md", ArtifactType.Plan, ArtifactFamily.Plan);
        await AddStaticArtifactAsync(artifacts, repository, "operational_context.md", ArtifactType.OperationalContext, ArtifactFamily.OperationalContext);
        await AddDirectoryArtifactsAsync(artifacts, repository, "", ArtifactType.OperationalContext, ArtifactFamily.OperationalContext);
        await AddDirectoryArtifactsAsync(artifacts, repository, "milestones", ArtifactType.Milestone, ArtifactFamily.Milestone);
        await AddDirectoryArtifactsAsync(artifacts, repository, "handoffs", ArtifactType.Handoff, ArtifactFamily.Handoff);
        await AddDirectoryArtifactsAsync(artifacts, repository, "decisions", ArtifactType.Decision, ArtifactFamily.Decision);
        await AddReasoningArtifactsAsync(artifacts, repository);

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
        string? content = await artifactStore.ReadAsync(ArtifactPath.ResolveRepositoryPath(repository, relativePath));
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
        string relativePath = ArtifactPath.CombineRelative(AgentsDirectory, fileName);
        string fullPath = ArtifactPath.ResolveRepositoryPath(repository, relativePath);
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
        string directoryPath = ArtifactPath.ResolveRepositoryPath(repository, ArtifactPath.CombineRelative(AgentsDirectory, directoryName));
        IReadOnlyList<string> files = await artifactStore.ListAsync(directoryPath, "*.md");
        artifacts.AddRange(files
            .Where(file => ShouldIncludeDirectoryArtifact(family, Path.GetFileName(file)))
            .Select(file => CreateArtifact(repository, file, type, family)));
    }

    private async Task AddReasoningArtifactsAsync(List<Artifact> artifacts, Repository repository)
    {
        string reasoningRoot = ArtifactPath.ResolveRepositoryPath(repository, ArtifactPath.CombineRelative(AgentsDirectory, "reasoning"));
        await AddReasoningProjectionArtifactsAsync(artifacts, repository, ArtifactPath.CombineRelative(reasoningRoot, "events"), "event.md");
        await AddReasoningProjectionArtifactsAsync(artifacts, repository, ArtifactPath.CombineRelative(reasoningRoot, "threads"), "thread.md");
        await AddReasoningProjectionArtifactsAsync(artifacts, repository, ArtifactPath.CombineRelative(reasoningRoot, "relationships"), "relationship.md");

        string reportsRoot = ArtifactPath.CombineRelative(reasoningRoot, "reports");
        IReadOnlyList<string> reportFiles = await artifactStore.ListAsync(reportsRoot, "*.md");
        artifacts.AddRange(reportFiles
            .Where(file => IsReasoningReport(Path.GetFileName(file)))
            .Select(file => CreateArtifact(repository, file, ArtifactType.Reasoning, ArtifactFamily.Reasoning)));
    }

    private async Task AddReasoningProjectionArtifactsAsync(
        List<Artifact> artifacts,
        Repository repository,
        string directoryPath,
        string projectionFileName)
    {
        IReadOnlyList<string> directories = await artifactStore.ListDirectoriesAsync(directoryPath);
        foreach (string directory in directories)
        {
            string projectionPath = ArtifactPath.CombineRelative(directory, projectionFileName);
            if (await artifactStore.ExistsAsync(projectionPath))
            {
                artifacts.Add(CreateArtifact(repository, projectionPath, ArtifactType.Reasoning, ArtifactFamily.Reasoning));
            }
        }
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
            ArtifactFamily.OperationalContext => IsHistorical(fileName, "operational_context"),
            ArtifactFamily.Reasoning => IsReasoningReport(fileName),
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

        if (family == ArtifactFamily.OperationalContext)
        {
            return string.Equals(fileName, "operational_context.md", StringComparison.OrdinalIgnoreCase)
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

        string prefix = $"{baseName}.";
        const string suffix = ".md";

        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string sequenceText = fileName[prefix.Length..^suffix.Length];
        return sequenceText.Length == 4 &&
            sequenceText.All(char.IsDigit) &&
            int.TryParse(sequenceText, out int sequence) &&
            sequence > 0;
    }

    private static bool IsHistorical(string fileName, string baseName)
    {
        return !string.Equals(fileName, $"{baseName}.md", StringComparison.OrdinalIgnoreCase) &&
            IsCurrentOrHistorical(fileName, baseName);
    }

    private static bool IsReasoningReport(string fileName)
    {
        const string suffix = ".md";
        return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
            (fileName.StartsWith("reconstruction.", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("certification.", StringComparison.OrdinalIgnoreCase));
    }
}
