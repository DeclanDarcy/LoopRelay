namespace CommandCenter.Roadmap.Cli;

internal sealed class ProjectionCache(
    RoadmapArtifacts artifacts,
    ProjectionRegistry registry,
    ProjectionManifestStore manifestStore,
    ProjectionValidator validator,
    RoadmapPromptRunner promptRunner)
{
    public async Task<ProjectionCacheResult> EnsureAsync(
        string runtimePromptName,
        NorthStarContext northStarContext,
        PromptContract contract,
        CancellationToken cancellationToken)
    {
        ProjectionDefinition projection = registry.Get(runtimePromptName);
        string? content = await artifacts.ReadAsync(projection.ProjectionPath);
        bool generated = false;

        if (string.IsNullOrWhiteSpace(content))
        {
            content = await promptRunner.RunProjectionPromptAsync(projection, northStarContext.Content, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new RoadmapStepException($"{projection.ProjectionPromptName} returned empty projection content.");
            }

            generated = true;
        }

        ProjectionValidationResult validation = validator.Validate(runtimePromptName, content);
        string projectionHash = RoadmapHash.Sha256(content);
        ProjectionManifest manifest = await manifestStore.LoadAsync();
        ProjectionManifestEntry? previous = manifest.Find(runtimePromptName);
        ProjectionStaleStatus staleStatus = !generated && previous is not null &&
            !string.Equals(previous.NorthStarHash, northStarContext.Hash, StringComparison.Ordinal)
                ? ProjectionStaleStatus.Stale
                : ProjectionStaleStatus.Fresh;

        var entry = new ProjectionManifestEntry(
            runtimePromptName,
            projection.ProjectionPromptName,
            projection.ProjectionPath,
            RoadmapHash.Sha256(projection.ProjectionPromptName),
            northStarContext.SourceFiles,
            generated || previous is null ? northStarContext.Hash : previous.NorthStarHash,
            projectionHash,
            generated || previous is null ? DateTimeOffset.UtcNow : previous.GeneratedAt,
            validation.IsValid ? ProjectionValidationStatus.Valid : ProjectionValidationStatus.Invalid,
            staleStatus,
            validation.Error);

        await manifestStore.UpsertAsync(entry);

        if (!validation.IsValid)
        {
            string blockedPath = await WriteBlockedAsync(
                runtimePromptName,
                "Projection validation failed",
                "Delete or repair the projection file and rerun the roadmap CLI.",
                validation.Error ?? "Projection validation failed.");
            throw new RoadmapStepException($"Projection validation failed for {runtimePromptName}: {validation.Error}. Blocked artifact: {blockedPath}");
        }

        if (generated)
        {
            await artifacts.WriteAsync(projection.ProjectionPath, content);
        }

        if (staleStatus == ProjectionStaleStatus.Stale && contract.StaleProjectionPolicy == StaleProjectionPolicy.Block)
        {
            string blockedPath = await WriteBlockedAsync(
                runtimePromptName,
                "Projection refresh recommended",
                $"Delete {projection.ProjectionPath} to regenerate it from the current Core North-Star.",
                $"Manifest northStarHash differs from the current north-star hash for {runtimePromptName}.");
            throw new RoadmapStepException($"Projection is stale for {runtimePromptName}. Blocked artifact: {blockedPath}");
        }

        return new ProjectionCacheResult(projection, content, generated, staleStatus);
    }

    private async Task<string> WriteBlockedAsync(string runtimePromptName, string reason, string nextStep, string details)
    {
        string content = RoadmapBlockedArtifact.Render(
            RoadmapState.EvidenceBlocked,
            runtimePromptName,
            reason,
            nextStep,
            "None",
            details,
            DateTimeOffset.UtcNow);
        return await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "projection-blocked",
            content);
    }
}

internal sealed record ProjectionCacheResult(
    ProjectionDefinition Definition,
    string Content,
    bool Generated,
    ProjectionStaleStatus StaleStatus);
