using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Primitives.Projections;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.State;

namespace LoopRelay.Roadmap.Cli.Services.Projections;

internal sealed class ProjectionCache(
    RoadmapArtifacts _artifacts,
    ProjectionRegistry _registry,
    IProjectionManifestStore _manifestStore,
    ProjectionValidator _validator,
    RoadmapPromptRunner _promptRunner)
{
    public async Task<ProjectionCacheResult> EnsureAsync(
        string runtimePromptName,
        ProjectContext projectContext,
        PromptContract contract,
        CancellationToken cancellationToken)
    {
        ProjectionDefinition projection = _registry.Get(runtimePromptName);
        var provenanceFactory = new ProjectionProvenanceFactory(_registry);
        ProjectionProvenance currentProvenance = provenanceFactory.Create(projection, projectContext);
        string? content = await _artifacts.ReadAsync(projection.ProjectionPath);
        bool generated = false;

        if (string.IsNullOrWhiteSpace(content))
        {
            content = await _promptRunner.RunProjectionPromptAsync(projection, projectContext.Content, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new RoadmapStepException($"{projection.ProjectionPromptName} returned empty projection content.");
            }

            generated = true;
        }

        ProjectionValidationResult validation = _validator.Validate(runtimePromptName, content);
        string projectionHash = RoadmapHash.Sha256(content);
        ProjectionManifest manifest = await _manifestStore.LoadAsync();
        ProjectionManifestEntry? previous = manifest.Find(runtimePromptName);
        ProjectionValidationStatus validationStatus = validation.IsValid
            ? ProjectionValidationStatus.Valid
            : ProjectionValidationStatus.Invalid;
        ProjectionFreshness freshness = generated
            ? ProjectionFreshness.Fresh
            : ProjectionFreshnessEvaluator.Evaluate(currentProvenance, previous);
        DateTimeOffset observedAt = DateTimeOffset.UtcNow;
        ProjectionManifestEntry entry = generated || freshness.IsFresh
            ? ProjectionManifestEntry.FromTrustedProvenance(
                currentProvenance,
                projectionHash,
                generated || previous is null ? observedAt : previous.GeneratedAt,
                validationStatus,
                freshness,
                validation.Error)
            : previous is null
                ? CreateUnknownEntry(projection, projectionHash, observedAt, validationStatus, freshness, validation.Error)
                : previous.WithFreshness(freshness, projectionHash, validationStatus, validation.Error);

        await _manifestStore.UpsertAsync(entry);

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
            await _artifacts.WriteAsync(projection.ProjectionPath, content);
        }

        if (!freshness.IsFresh && contract.StaleProjectionPolicy == StaleProjectionPolicy.Block)
        {
            string blockedPath = await WriteBlockedAsync(
                runtimePromptName,
                "Projection refresh recommended",
                $"Delete {projection.ProjectionPath} to regenerate it from the current Project Context.",
                $"Projection provenance is not fresh for {runtimePromptName}: {FormatReasons(freshness.Reasons)}.");
            throw new RoadmapStepException($"Projection is stale for {runtimePromptName}: {FormatReasons(freshness.Reasons)}. Blocked artifact: {blockedPath}");
        }

        return new ProjectionCacheResult(projection, content, generated, freshness.Status, freshness.Reasons);
    }

    private static ProjectionManifestEntry CreateUnknownEntry(
        ProjectionDefinition projection,
        string projectionHash,
        DateTimeOffset observedAt,
        ProjectionValidationStatus validationStatus,
        ProjectionFreshness freshness,
        string? validationError) =>
        new(
            projection.RuntimePromptName,
            projection.ProjectionPromptName,
            projection.ProjectionPath,
            string.Empty,
            [],
            string.Empty,
            projectionHash,
            observedAt,
            validationStatus,
            freshness.Status,
            validationError,
            ProjectionProvenanceStatus.Unknown,
            projection.RuntimePromptName,
            string.Empty,
            [],
            freshness.Reasons);

    private static string FormatReasons(IReadOnlyList<ProjectionStaleReason> reasons) =>
        reasons.Count == 0 ? "UnknownProvenance" : string.Join(", ", reasons);

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
        return await _artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "projection-blocked",
            content);
    }
}
