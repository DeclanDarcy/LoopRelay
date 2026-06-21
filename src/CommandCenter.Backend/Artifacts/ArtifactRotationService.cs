using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Artifacts;

public sealed class ArtifactRotationService(
    IArtifactStore artifactStore,
    IArtifactService artifactService) : IArtifactRotationService
{
    public Task<Artifact> RotateCurrentHandoffAsync(Repository repository)
    {
        return RotateAsync(repository, ArtifactFamily.Handoff);
    }

    public Task<Artifact> RotateCurrentDecisionsAsync(Repository repository)
    {
        return RotateAsync(repository, ArtifactFamily.Decision);
    }

    public Task<Artifact> RotateCurrentOperationalContextAsync(Repository repository)
    {
        return RotateAsync(repository, ArtifactFamily.OperationalContext);
    }

    public async Task<Artifact> RotateAsync(Repository repository, ArtifactFamily family)
    {
        RotationDefinition definition = GetDefinition(family);
        Artifact? currentArtifact = family switch
        {
            ArtifactFamily.Handoff => await artifactService.GetCurrentHandoffAsync(repository),
            ArtifactFamily.Decision => await artifactService.GetCurrentDecisionsAsync(repository),
            ArtifactFamily.OperationalContext => await artifactService.GetCurrentOperationalContextAsync(repository),
            _ => null
        };

        if (currentArtifact is null)
        {
            throw new FileNotFoundException("Current artifact was not found.", definition.CurrentRelativePath);
        }

        string currentPath = ArtifactPath.ResolveRepositoryPath(repository, definition.CurrentRelativePath);
        string? currentContent = await artifactStore.ReadAsync(currentPath);
        if (currentContent is null)
        {
            throw new FileNotFoundException("Current artifact was not found.", definition.CurrentRelativePath);
        }

        string historicalDirectory = ArtifactPath.ResolveRepositoryPath(repository, definition.DirectoryRelativePath);
        IReadOnlyList<string> files = await artifactStore.ListAsync(historicalDirectory, "*.md");
        int nextSequence = files
            .Select(file => TryParseHistoricalSequence(definition.BaseName, Path.GetFileName(file)))
            .Where(sequence => sequence.HasValue)
            .Select(sequence => sequence!.Value)
            .DefaultIfEmpty(0)
            .Max() + 1;

        string targetRelativePath = ArtifactPath.CombineRelative(
            definition.DirectoryRelativePath,
            $"{definition.BaseName}.{nextSequence:0000}.md");
        string targetPath = ArtifactPath.ResolveRepositoryPath(repository, targetRelativePath);

        if (await artifactStore.ExistsAsync(targetPath))
        {
            throw new IOException($"Historical artifact already exists: {targetRelativePath}");
        }

        await artifactStore.WriteAsync(targetPath, currentContent);

        return new Artifact
        {
            RelativePath = targetRelativePath,
            Name = Path.GetFileName(targetPath),
            Type = definition.Type,
            Family = family,
            VersionKind = ArtifactVersionKind.Historical
        };
    }

    private static RotationDefinition GetDefinition(ArtifactFamily family)
    {
        return family switch
        {
            ArtifactFamily.Handoff => new RotationDefinition(
                ".agents/handoffs",
                ".agents/handoffs/handoff.md",
                "handoff",
                ArtifactType.Handoff),
            ArtifactFamily.Decision => new RotationDefinition(
                ".agents/decisions",
                ".agents/decisions/decisions.md",
                "decisions",
                ArtifactType.Decision),
            ArtifactFamily.OperationalContext => new RotationDefinition(
                ".agents",
                ".agents/operational_context.md",
                "operational_context",
                ArtifactType.OperationalContext),
            _ => throw new NotSupportedException($"Artifact family does not support rotation: {family}")
        };
    }

    private static int? TryParseHistoricalSequence(string baseName, string fileName)
    {
        string prefix = $"{baseName}.";
        const string suffix = ".md";

        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
            fileName.Length != prefix.Length + 4 + suffix.Length)
        {
            return null;
        }

        string sequenceText = fileName[prefix.Length..^suffix.Length];
        if (sequenceText.Length != 4 ||
            !sequenceText.All(char.IsDigit) ||
            !int.TryParse(sequenceText, out int sequence) ||
            sequence <= 0)
        {
            return null;
        }

        return sequence;
    }

    private sealed record RotationDefinition(
        string DirectoryRelativePath,
        string CurrentRelativePath,
        string BaseName,
        ArtifactType Type);
}
