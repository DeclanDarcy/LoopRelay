using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.Manifests;
using LoopRelay.Projections.Models.Provenance;
using LoopRelay.Projections.Primitives;
using LoopRelay.Projections.Services.Definitions;
using LoopRelay.Projections.Services.Manifests;
using LoopRelay.Projections.Services.Provenance;

namespace LoopRelay.Projections.Services.Context;

public sealed class ProjectContextProjectionService(
    ProjectionArtifacts.ProjectionArtifacts artifacts,
    ProjectionDefinitionRegistry registry,
    ProjectionManifestStore manifestStore,
    ProjectionValidator validator,
    IProjectionPromptRunner promptRunner) : IProjectContextProjectionService
{
    private readonly ProjectionArtifacts.ProjectionArtifacts _artifacts = artifacts;
    private readonly ProjectionDefinitionRegistry _registry = registry;
    private readonly ProjectionManifestStore _manifestStore = manifestStore;
    private readonly ProjectionValidator _validator = validator;
    private readonly IProjectionPromptRunner _promptRunner = promptRunner;
    private readonly ProjectContextLoader _projectContextLoader = new(artifacts);
    private readonly ProjectionProvenanceFactory provenanceFactory = new();

    public Task<ProjectContextProjectionResult> EnsureFreshAsync(
        string runtimePromptName,
        CancellationToken cancellationToken = default) =>
        EnsureAsync(runtimePromptName, ProjectionRefreshPolicy.RegenerateWhenStale, cancellationToken);

    public async Task<ProjectContextProjectionResult> EnsureAsync(
        string runtimePromptName,
        ProjectionRefreshPolicy refreshPolicy,
        CancellationToken cancellationToken = default)
    {
        ProjectContext projectContext = await _projectContextLoader.LoadAsync(cancellationToken);
        ProjectionDefinition definition = _registry.Get(runtimePromptName);
        ProjectionProvenance currentProvenance = provenanceFactory.Create(definition, projectContext);
        ProjectionManifest manifest = await _manifestStore.LoadAsync();
        ProjectionManifestEntry? previous = manifest.Find(runtimePromptName);
        string? content = await _artifacts.ReadAsync(definition.ProjectionPath);
        bool generated = false;
        ProjectionFreshness freshness;
        ProjectionValidationResult validation;

        if (string.IsNullOrWhiteSpace(content))
        {
            content = await GenerateAsync(definition, projectContext, cancellationToken);
            generated = true;
            freshness = ProjectionFreshness.Fresh;
            validation = _validator.Validate(runtimePromptName, content);
        }
        else
        {
            validation = _validator.Validate(runtimePromptName, content);
            freshness = ProjectionFreshnessEvaluator.Evaluate(currentProvenance, previous);
            if ((!validation.IsValid || !freshness.IsFresh) && refreshPolicy == ProjectionRefreshPolicy.RegenerateWhenStale)
            {
                content = await GenerateAsync(definition, projectContext, cancellationToken);
                generated = true;
                freshness = ProjectionFreshness.Fresh;
                validation = _validator.Validate(runtimePromptName, content);
            }
        }

        string projectionHash = ProjectionHash.Sha256(content);
        DateTimeOffset observedAt = DateTimeOffset.UtcNow;
        ProjectionValidationStatus validationStatus = validation.IsValid
            ? ProjectionValidationStatus.Valid
            : ProjectionValidationStatus.Invalid;
        ProjectionManifestEntry entry = ProjectionManifestEntry.FromTrustedProvenance(
            currentProvenance,
            projectionHash,
            generated || previous is null ? observedAt : previous.GeneratedAt,
            validationStatus,
            freshness,
            validation.Error);

        if (!validation.IsValid)
        {
            await _manifestStore.UpsertAsync(entry);
            throw new ProjectionException($"Projection validation failed for {runtimePromptName}: {validation.Error}");
        }

        if (!freshness.IsFresh && refreshPolicy == ProjectionRefreshPolicy.BlockWhenStale)
        {
            await _manifestStore.UpsertAsync(entry);
            throw new ProjectionException($"Projection is stale for {runtimePromptName}: {FormatReasons(freshness.Reasons)}.");
        }

        if (generated)
        {
            await _artifacts.WriteAsync(definition.ProjectionPath, content);
        }

        await _manifestStore.UpsertAsync(entry);
        return new ProjectContextProjectionResult(definition, content, generated, freshness.Status, freshness.Reasons);
    }

    public async Task<ProjectionFreshness> EvaluateFreshnessAsync(
        string runtimePromptName,
        CancellationToken cancellationToken = default)
    {
        ProjectContext projectContext = await _projectContextLoader.LoadAsync(cancellationToken);
        ProjectionDefinition definition = _registry.Get(runtimePromptName);
        string? content = await _artifacts.ReadAsync(definition.ProjectionPath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return ProjectionFreshness.Unknown(ProjectionStaleReason.MissingProjectionArtifact);
        }

        ProjectionManifest manifest = await _manifestStore.LoadAsync();
        ProjectionProvenance currentProvenance = provenanceFactory.Create(definition, projectContext);
        return ProjectionFreshnessEvaluator.Evaluate(currentProvenance, manifest.Find(runtimePromptName));
    }

    private async Task<string> GenerateAsync(
        ProjectionDefinition definition,
        ProjectContext projectContext,
        CancellationToken cancellationToken)
    {
        string prompt = definition.RenderPrompt(projectContext.Content);
        string content = await _promptRunner.RunProjectionPromptAsync(definition, prompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ProjectionException($"{definition.ProjectionPromptName} returned empty projection content.");
        }

        return content;
    }

    private static string FormatReasons(IReadOnlyList<ProjectionStaleReason> reasons) =>
        reasons.Count == 0 ? "UnknownProvenance" : string.Join(", ", reasons);
}
