using System.Security.Cryptography;
using System.Text;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Services.Artifacts;

namespace LoopRelay.Core.Tests.Services;

public sealed class LogicalArtifactResolverTests
{
    [Theory]
    [InlineData(".agents/specs/epic.md")]
    [InlineData(".agents/epic.md")]
    [InlineData(".agents/plan.md")]
    [InlineData(".agents/operational_context.md")]
    [InlineData(".agents/decisions/decisions.md")]
    [InlineData(".agents/handoffs/handoff.md")]
    public async Task Retained_files_resolve_from_filesystem(string path)
    {
        var repo = new TestRepo();
        await repo.WriteAsync(path, $"content for {path}");
        var resolver = new LogicalArtifactResolver(
        [
            RetainedProvider(repo),
        ]);

        LogicalArtifactResolutionResult result = await resolver.ResolveAsync(path);

        Assert.True(result.IsResolved);
        Assert.Equal(LogicalArtifactDomain.RetainedFile, result.Descriptor.Domain);
        Assert.Equal(LogicalArtifactStorageKind.RetainedFilesystem, result.Descriptor.StorageKind);
        Assert.Equal(path, result.Descriptor.RelativePath);
        Assert.Equal($"content for {path}", result.Content!.Text);
    }

    [Fact]
    public async Task Historical_loop_paths_resolve_from_file_backed_migrated_provider()
    {
        var repo = new TestRepo();
        await repo.WriteAsync(".agents/decisions/decisions.0001.md", "decision history");
        await repo.WriteAsync(".agents/handoffs/handoff.0002.md", "handoff history");
        await repo.WriteAsync(".agents/deltas/operational_delta.0003.md", "delta history");
        var resolver = new LogicalArtifactResolver(
        [
            new FileBackedMigratedDomainLogicalArtifactProvider(
                new RepositoryArtifactStore(repo.Store, repo.Repository),
                new Dictionary<string, LogicalArtifactDomain>(),
                [
                    new(".agents/decisions", "decisions.*.md", LogicalArtifactDomain.LoopHistory, "decisions"),
                    new(".agents/handoffs", "handoff.*.md", LogicalArtifactDomain.LoopHistory, "handoff"),
                    new(".agents/deltas", "operational_delta.*.md", LogicalArtifactDomain.LoopHistory, "operational_delta"),
                ]),
        ]);

        LogicalArtifactResolutionResult decision = await resolver.ResolveAsync(".agents/decisions/decisions.0001.md");
        LogicalArtifactResolutionResult handoff = await resolver.ResolveAsync(".agents/handoffs/handoff.0002.md");
        LogicalArtifactResolutionResult delta = await resolver.ResolveAsync(".agents/deltas/operational_delta.0003.md");

        Assert.Equal("decision history", decision.Content!.Text);
        Assert.Equal("handoff history", handoff.Content!.Text);
        Assert.Equal("delta history", delta.Content!.Text);
        Assert.All([decision, handoff, delta], result =>
        {
            Assert.True(result.IsResolved);
            Assert.Equal(LogicalArtifactDomain.LoopHistory, result.Descriptor.Domain);
            Assert.Equal(LogicalArtifactStorageKind.FileBackedMigratedDomain, result.Descriptor.StorageKind);
        });
    }

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
    public async Task Missing_paths_keep_retained_and_migrated_statuses_distinct()
    {
        var repo = new TestRepo();
        var evidence = new FileBackedExecutionEvidenceStore(
            new RepositoryArtifactStore(repo.Store, repo.Repository));
        var resolver = new LogicalArtifactResolver(
        [
            RetainedProvider(repo),
            new FileBackedExecutionEvidenceLogicalArtifactProvider(evidence),
        ]);

        LogicalArtifactResolutionResult retained = await resolver.ResolveAsync(".agents/plan.md");
        LogicalArtifactResolutionResult migrated = await resolver.ResolveAsync(
            ".agents/evidence/execution/missing.0001.md");

        Assert.Equal(LogicalArtifactResolutionStatus.MissingRetainedFile, retained.Status);
        Assert.Equal(LogicalArtifactResolutionStatus.MissingMigratedRecord, migrated.Status);
    }

    [Fact]
    public async Task Canonical_hasher_uses_resolved_logical_content()
    {
        var repo = new TestRepo();
        await repo.WriteAsync(".agents/plan.md", "hash me");
        var resolver = new LogicalArtifactResolver(
        [
            RetainedProvider(repo),
        ]);
        var hasher = new CanonicalArtifactHasher(resolver);

        CanonicalArtifactHash hash = await hasher.RequireHashAsync(".agents/plan.md");

        Assert.Equal("sha256", hash.Algorithm);
        Assert.Equal(ExpectedSha256("hash me"), hash.Value);
        Assert.Equal(".agents/plan.md", hash.Descriptor.RelativePath);
    }

    private static RetainedFilesystemLogicalArtifactProvider RetainedProvider(TestRepo repo) =>
        new(
            new RepositoryArtifactStore(repo.Store, repo.Repository),
            new Dictionary<string, LogicalArtifactDomain>
            {
                [".agents/specs/epic.md"] = LogicalArtifactDomain.RetainedFile,
                [".agents/epic.md"] = LogicalArtifactDomain.RetainedFile,
                [".agents/plan.md"] = LogicalArtifactDomain.RetainedFile,
                [".agents/operational_context.md"] = LogicalArtifactDomain.RetainedFile,
                [".agents/decisions/decisions.md"] = LogicalArtifactDomain.RetainedFile,
                [".agents/handoffs/handoff.md"] = LogicalArtifactDomain.RetainedFile,
            });

    private static string ExpectedSha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

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
