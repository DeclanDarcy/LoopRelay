using LoopRelay.Core.Abstractions.Persistence;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;

namespace LoopRelay.Core.Tests.Services;

public sealed class FileBackedExecutionEvidenceStoreTests
{
    [Fact]
    public async Task WriteAsync_AppendsExpectedPathAndPreservesContent()
    {
        using TempRepo repo = TempRepo.Create();
        var evidence = new FileBackedExecutionEvidenceStore(repo.Store, repo.Repository);
        const string content = "# Evidence\r\n\r\nOpaque body.";

        ExecutionEvidenceRecord record = await evidence.WriteAsync("execution-result", content);

        Assert.Equal("execution-result", record.Stem);
        Assert.Equal(1, record.Sequence);
        Assert.Equal(".agents/evidence/execution/execution-result.0001.md", record.RelativePath);
        Assert.Equal(content, record.Content);
        Assert.Equal(content, await repo.ReadAsync(record.RelativePath));
    }

    [Fact]
    public async Task WriteAsync_AllocatesOneAfterHighestNumericSuffixForStem()
    {
        using TempRepo repo = TempRepo.Create();
        await repo.WriteAsync(".agents/evidence/execution/execution-result.0002.md", "old 2");
        await repo.WriteAsync(".agents/evidence/execution/execution-result.0010.md", "old 10");
        await repo.WriteAsync(".agents/evidence/execution/execution-result.invalid.md", "ignored");
        await repo.WriteAsync(".agents/evidence/execution/other.9999.md", "other stem");
        var evidence = new FileBackedExecutionEvidenceStore(repo.Store, repo.Repository);

        ExecutionEvidenceRecord record = await evidence.WriteAsync("execution-result", "new");

        Assert.Equal(11, record.Sequence);
        Assert.Equal(".agents/evidence/execution/execution-result.0011.md", record.RelativePath);
        Assert.Equal("new", await repo.ReadAsync(record.RelativePath));
    }

    [Fact]
    public async Task NextPathAsync_DoesNotWriteEvidence()
    {
        using TempRepo repo = TempRepo.Create();
        var evidence = new FileBackedExecutionEvidenceStore(repo.Store, repo.Repository);

        string path = await evidence.NextPathAsync("execution-result");

        Assert.Equal(".agents/evidence/execution/execution-result.0001.md", path);
        Assert.Null(await repo.ReadAsync(path));
    }

    [Fact]
    public async Task ListAsync_ReturnsMatchingEvidenceRecordsInNumericOrder()
    {
        using TempRepo repo = TempRepo.Create();
        await repo.WriteAsync(".agents/evidence/execution/execution-result.0002.md", "old 2");
        await repo.WriteAsync(".agents/evidence/execution/execution-result.0010.md", "old 10");
        await repo.WriteAsync(".agents/evidence/execution/other.0001.md", "other");
        var evidence = new FileBackedExecutionEvidenceStore(repo.Store, repo.Repository);

        IReadOnlyList<ExecutionEvidenceRecord> records = await evidence.ListAsync("execution-result.*.md");

        Assert.Equal([2, 10], records.Select(record => record.Sequence).ToArray());
        Assert.Equal(["old 2", "old 10"], records.Select(record => record.Content).ToArray());
    }

    private sealed class TempRepo : IDisposable
    {
        private TempRepo(string root)
        {
            Root = root;
            Repository = new Repository
            {
                Id = Guid.NewGuid(),
                Name = "repo",
                Path = root,
            };
        }

        public string Root { get; }

        public Repository Repository { get; }

        public FileSystemArtifactStore Store { get; } = new();

        public static TempRepo Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "LoopRelay.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempRepo(root);
        }

        public Task WriteAsync(string relativePath, string content) =>
            Store.WriteAsync(ArtifactPath.ResolveRepositoryPath(Repository, relativePath), content);

        public Task<string?> ReadAsync(string relativePath) =>
            Store.ReadAsync(ArtifactPath.ResolveRepositoryPath(Repository, relativePath));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
