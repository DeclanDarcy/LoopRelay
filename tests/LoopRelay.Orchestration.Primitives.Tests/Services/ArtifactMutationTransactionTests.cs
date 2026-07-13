using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Orchestration.Services;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Orchestration.Tests.Services;

public sealed class ArtifactMutationTransactionTests
{
    [Fact]
    public async Task Created_glob_files_are_detected_and_removed_by_restore()
    {
        var store = new TestArtifactStore();
        await store.WriteAsync(".agents/milestones/m1.md", "# M1\n");
        var profile = new OperationPermissionProfile(
            "refine-details",
            ".",
            [],
            [],
            [],
            [new OperationPathGlob(".agents/milestones", "m*.md")]);
        ArtifactMutationTransaction transaction = await ArtifactMutationTransaction.CaptureAsync(store, profile);

        await store.WriteAsync(".agents/milestones/m2.md", "# M2\n");

        Assert.Equal([".agents/milestones/m2.md"], await transaction.CreatedGlobFilesAsync());
        await transaction.RestoreAsync();
        Assert.True(await store.ExistsAsync(".agents/milestones/m1.md"));
        Assert.False(await store.ExistsAsync(".agents/milestones/m2.md"));
    }

    private sealed class TestArtifactStore : IArtifactStore
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

        public Task<bool> ExistsAsync(string path) => Task.FromResult(_files.ContainsKey(path));
        public Task<string?> ReadAsync(string path) =>
            Task.FromResult(_files.TryGetValue(path, out string? content) ? content : null);
        public Task WriteAsync(string path, string content)
        {
            _files[path] = content;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string path)
        {
            _files.Remove(path);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern)
        {
            string prefix = path.TrimEnd('/') + "/";
            string suffix = searchPattern.StartsWith("*", StringComparison.Ordinal)
                ? searchPattern[1..]
                : searchPattern[(searchPattern.IndexOf('*') + 1)..];
            string filePrefix = searchPattern.Contains('*')
                ? searchPattern[..searchPattern.IndexOf('*')]
                : searchPattern;
            string[] matches = _files.Keys.Where(candidate =>
                    candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(candidate).StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(candidate).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return Task.FromResult<IReadOnlyList<string>>(matches);
        }
        public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
