using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Plan.Cli.Services.PlanArtifactOperations;

/// <summary>
/// Repository-relative .agents/* disk access for the planning pipeline, plus dedicated absolute-path
/// helpers for operation gates that already hold resolved paths.
/// </summary>
internal sealed class PlanArtifacts(IArtifactStore store, Repository repository)
{
    // Derived from SpecsEpic rather than restated, per the plan's "never restate .agents/... literals" rule.
    private static readonly string SpecsDirectory =
        Path.GetDirectoryName(OrchestrationArtifactPaths.SpecsEpic)!.Replace('\\', '/');
    private readonly IArtifactStore _store = store;
    private readonly Repository _repository = repository;

    public Task<bool> ExistsAsync(string relativePath) =>
        _store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) =>
        _store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) =>
        _store.WriteAsync(Resolve(relativePath), content);

    public async Task<IReadOnlyList<string>> ListSpecsRelativeAsync()
    {
        IReadOnlyList<string> files = await _store.ListAsync(Resolve(SpecsDirectory), "*.md");
        return ToRepositoryRelative(files);
    }

    public async Task<IReadOnlyList<string>> ListMilestonesRelativeAsync()
    {
        IReadOnlyList<string> files = await _store.ListAsync(
            Resolve(OrchestrationArtifactPaths.MilestonesDirectory),
            OrchestrationArtifactPaths.MilestoneSearchPattern);
        return ToRepositoryRelative(files);
    }

    // Sandbox workspaces live outside the repository root; these never go through the relative Resolve helper.
    public Task<bool> ExistsAbsoluteAsync(string absolutePath) => _store.ExistsAsync(absolutePath);

    public Task<string?> ReadAbsoluteAsync(string absolutePath) => _store.ReadAsync(absolutePath);

    public Task WriteAbsoluteAsync(string absolutePath, string content) => _store.WriteAsync(absolutePath, content);

    public Task<IReadOnlyList<string>> ListAbsoluteAsync(string absoluteDirectory, string searchPattern) =>
        _store.ListAsync(absoluteDirectory, searchPattern);

    private string Resolve(string relativePath) => ArtifactPath.ResolveRepositoryPath(_repository, relativePath);

    private IReadOnlyList<string> ToRepositoryRelative(IReadOnlyList<string> fullPaths) =>
        fullPaths.Select(path => ArtifactPath.ToRepositoryRelativePath(_repository, path)).ToList();
}
