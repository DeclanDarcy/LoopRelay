using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Services.Artifacts;

namespace LoopRelay.Core.Tests.Services;

public sealed class LogicalArtifactResolverTests
{
    [Fact]
    public async Task Execution_evidence_resolves_through_file_backed_evidence_store()
    {
        var repo = new TestRepo();
        var evidence = new FileBackedExecutionEvidenceStore(
            new RepositoryArtifactStore(repo.Store, repo.Repository));
        var resolver = new LogicalArtifactResolver(
        [
            new FileBackedExecutionEvidenceLogicalArtifactProvider(evidence),
        ]);
        var written = await evidence.WriteAsync("execution-trust-posture", "evidence body");

        LogicalArtifactResolutionResult result = await resolver.ResolveAsync(written.RelativePath);

        Assert.True(result.IsResolved);
        Assert.Equal(LogicalArtifactDomain.ExecutionEvidence, result.Descriptor.Domain);
        Assert.Equal(LogicalArtifactStorageKind.FileBackedMigratedDomain, result.Descriptor.StorageKind);
        Assert.Equal("execution-trust-posture:0001", result.Descriptor.Identity);
        Assert.Equal("evidence body", result.Content!.Text);
    }

    [Fact]
    public async Task Missing_migrated_path_reports_MissingMigratedRecord()
    {
        var repo = new TestRepo();
        var evidence = new FileBackedExecutionEvidenceStore(
            new RepositoryArtifactStore(repo.Store, repo.Repository));
        var resolver = new LogicalArtifactResolver(
        [
            new FileBackedExecutionEvidenceLogicalArtifactProvider(evidence),
        ]);

        LogicalArtifactResolutionResult migrated = await resolver.ResolveAsync(
            ".agents/evidence/execution/missing.0001.md");

        Assert.Equal(LogicalArtifactResolutionStatus.MissingMigratedRecord, migrated.Status);
    }

    private sealed class TestRepo
    {
        public TestRepo()
        {
            Repository = new Repository
            {
                Id = Guid.NewGuid(),
                Name = "repo",
                Path = Path.Combine(Path.GetTempPath(), "LoopRelay.LogicalArtifacts", Guid.NewGuid().ToString("N")),
            };
        }

        public MemoryArtifactStore Store { get; } = new();

        public Repository Repository { get; }

        public Task WriteAsync(string relativePath, string content) =>
            Store.WriteAsync(ArtifactPath.ResolveRepositoryPath(Repository, relativePath), content);
    }
}
