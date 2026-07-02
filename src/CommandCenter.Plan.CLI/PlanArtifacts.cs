using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Plan.Cli;

/// <summary>
/// Repository-relative .agents/* disk access for the planning pipeline, plus dedicated absolute-path
/// helpers for seeded sandbox workspaces (which live outside the repository root, so the repo-relative
/// Resolve — which validates the boundary via ArtifactPath.ResolveRepositoryPath — cannot be used for them).
/// </summary>
internal sealed class PlanArtifacts(IArtifactStore store, Repository repository)
{
    // Derived from SpecsRoadmap rather than restated, per the plan's "never restate .agents/... literals" rule.
    private static readonly string SpecsDirectory =
        Path.GetDirectoryName(OrchestrationArtifactPaths.SpecsRoadmap)!.Replace('\\', '/');

    public Task<bool> ExistsAsync(string relativePath) =>
        store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) =>
        store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) =>
        store.WriteAsync(Resolve(relativePath), content);

    public async Task<IReadOnlyList<string>> ListSpecsRelativeAsync()
    {
        IReadOnlyList<string> files = await store.ListAsync(Resolve(SpecsDirectory), "*.md");
        return ToRepositoryRelative(files);
    }

    public async Task<IReadOnlyList<string>> ListMilestonesRelativeAsync()
    {
        IReadOnlyList<string> files = await store.ListAsync(
            Resolve(OrchestrationArtifactPaths.MilestonesDirectory),
            OrchestrationArtifactPaths.MilestoneSearchPattern);
        return ToRepositoryRelative(files);
    }

    // Sandbox workspaces live outside the repository root; these never go through the relative Resolve helper.
    public Task<bool> ExistsAbsoluteAsync(string absolutePath) => store.ExistsAsync(absolutePath);

    public Task<string?> ReadAbsoluteAsync(string absolutePath) => store.ReadAsync(absolutePath);

    public Task WriteAbsoluteAsync(string absolutePath, string content) => store.WriteAsync(absolutePath, content);

    public Task<IReadOnlyList<string>> ListAbsoluteAsync(string absoluteDirectory, string searchPattern) =>
        store.ListAsync(absoluteDirectory, searchPattern);

    private string Resolve(string relativePath) => ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    private IReadOnlyList<string> ToRepositoryRelative(IReadOnlyList<string> fullPaths) =>
        fullPaths.Select(path => ArtifactPath.ToRepositoryRelativePath(repository, path)).ToList();
}
