using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models;
using LoopRelay.Projections.Primitives;

namespace LoopRelay.Projections.Services;

public sealed class ProjectContextProjectionService(
    ProjectionArtifacts artifacts,
    ProjectionDefinitionRegistry registry,
    ProjectionManifestStore manifestStore,
    ProjectionValidator validator,
    IProjectionPromptRunner promptRunner) : IProjectContextProjectionService
{
    private readonly ProjectContextLoader projectContextLoader = new(artifacts);
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
        ProjectContext projectContext = await projectContextLoader.LoadAsync(cancellationToken);
        ProjectionDefinition definition = registry.Get(runtimePromptName);
        ProjectionProvenance currentProvenance = provenanceFactory.Create(definition, projectContext);
        ProjectionManifest manifest = await manifestStore.LoadAsync();
        ProjectionManifestEntry? previous = manifest.Find(runtimePromptName);
        string? content = await artifacts.ReadAsync(definition.ProjectionPath);
        bool generated = false;
        ProjectionFreshness freshness;
        ProjectionValidationResult validation;

        if (string.IsNullOrWhiteSpace(content))
        {
            content = await GenerateAsync(definition, projectContext, cancellationToken);
            generated = true;
            freshness = ProjectionFreshness.Fresh;
            validation = validator.Validate(runtimePromptName, content);
        }
        else
        {
            validation = validator.Validate(runtimePromptName, content);
            freshness = ProjectionFreshnessEvaluator.Evaluate(currentProvenance, previous);
            if ((!validation.IsValid || !freshness.IsFresh) && refreshPolicy == ProjectionRefreshPolicy.RegenerateWhenStale)
            {
                content = await GenerateAsync(definition, projectContext, cancellationToken);
                generated = true;
                freshness = ProjectionFreshness.Fresh;
                validation = validator.Validate(runtimePromptName, content);
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
            await manifestStore.UpsertAsync(entry);
            throw new ProjectionException($"Projection validation failed for {runtimePromptName}: {validation.Error}");
        }

        if (!freshness.IsFresh && refreshPolicy == ProjectionRefreshPolicy.BlockWhenStale)
        {
            await manifestStore.UpsertAsync(entry);
            throw new ProjectionException($"Projection is stale for {runtimePromptName}: {FormatReasons(freshness.Reasons)}.");
        }

        if (generated)
        {
            await artifacts.WriteAsync(definition.ProjectionPath, content);
        }

        await manifestStore.UpsertAsync(entry);
        return new ProjectContextProjectionResult(definition, content, generated, freshness.Status, freshness.Reasons);
    }

    public async Task<ProjectionFreshness> EvaluateFreshnessAsync(
        string runtimePromptName,
        CancellationToken cancellationToken = default)
    {
        ProjectContext projectContext = await projectContextLoader.LoadAsync(cancellationToken);
        ProjectionDefinition definition = registry.Get(runtimePromptName);
        string? content = await artifacts.ReadAsync(definition.ProjectionPath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return ProjectionFreshness.Unknown(ProjectionStaleReason.MissingProjectionArtifact);
        }

        ProjectionManifest manifest = await manifestStore.LoadAsync();
        ProjectionProvenance currentProvenance = provenanceFactory.Create(definition, projectContext);
        return ProjectionFreshnessEvaluator.Evaluate(currentProvenance, manifest.Find(runtimePromptName));
    }

    private async Task<string> GenerateAsync(
        ProjectionDefinition definition,
        ProjectContext projectContext,
        CancellationToken cancellationToken)
    {
        string prompt = definition.RenderPrompt(projectContext.Content);
        string content = await promptRunner.RunProjectionPromptAsync(definition, prompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ProjectionException($"{definition.ProjectionPromptName} returned empty projection content.");
        }

        return content;
    }

    private static string FormatReasons(IReadOnlyList<ProjectionStaleReason> reasons) =>
        reasons.Count == 0 ? "UnknownProvenance" : string.Join(", ", reasons);
}
