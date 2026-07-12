using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Infrastructure.Services.Artifacts;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Plan.Cli.Services.PlanArtifactOperations;

/// <summary>
/// Repository-relative .agents/* disk access for the planning pipeline, plus dedicated absolute-path
/// helpers for operation gates that already hold resolved paths.
/// </summary>
internal sealed class PlanArtifacts(IArtifactStore _store, Repository _repository)
{
    private readonly IArtifactStore repositoryArtifacts = _store is RepositoryArtifactStore
        ? _store
        : new RepositoryArtifactStore(_store, _repository);

    // Derived from SpecsEpic rather than restated, per the plan's "never restate .agents/... literals" rule.
    private static readonly string SpecsDirectory =
        Path.GetDirectoryName(OrchestrationArtifactPaths.SpecsEpic)!.Replace('\\', '/');

    public IArtifactStore Store => repositoryArtifacts;

    public Task<bool> ExistsAsync(string relativePath) =>
        repositoryArtifacts.ExistsAsync(relativePath);

    public Task<string?> ReadAsync(string relativePath) =>
        repositoryArtifacts.ReadAsync(relativePath);

    public Task WriteAsync(string relativePath, string content) =>
        repositoryArtifacts.WriteAsync(relativePath, content);

    public async Task<IReadOnlyList<string>> ListSpecsRelativeAsync()
    {
        return await repositoryArtifacts.ListAsync(SpecsDirectory, "*.md");
    }

    public async Task<IReadOnlyList<string>> ListMilestonesRelativeAsync()
    {
        return await repositoryArtifacts.ListAsync(
            OrchestrationArtifactPaths.MilestonesDirectory,
            OrchestrationArtifactPaths.MilestoneSearchPattern);
    }

    // Sandbox workspaces live outside the repository root; these never go through the relative Resolve helper.
    public Task<bool> ExistsAbsoluteAsync(string absolutePath) => _store.ExistsAsync(absolutePath);

    public Task<string?> ReadAbsoluteAsync(string absolutePath) => _store.ReadAsync(absolutePath);

    public Task WriteAbsoluteAsync(string absolutePath, string content) => _store.WriteAsync(absolutePath, content);

    public Task<IReadOnlyList<string>> ListAbsoluteAsync(string absoluteDirectory, string searchPattern) =>
        _store.ListAsync(absoluteDirectory, searchPattern);

}
