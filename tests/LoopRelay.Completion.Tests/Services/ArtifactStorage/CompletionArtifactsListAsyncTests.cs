using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Core.Abstractions.Persistence;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Completion.Tests.Services.ArtifactStorage;

/// <summary>
/// Covers PERF-14 (consumer side): <see cref="CompletionArtifacts.ListAsync"/> must enumerate
/// execution evidence via the path-only <see cref="SqliteExecutionEvidenceStore.ListPathsAsync"/>
/// projection rather than the hash-validating <see cref="SqliteExecutionEvidenceStore.ListAsync"/>,
/// while producing exactly the paths (and order) the content-validating listing would have produced.
/// </summary>
public sealed class CompletionArtifactsListAsyncTests : IDisposable
{
    private readonly string _root;
    private readonly Repository _repository;
    private readonly SqliteExecutionEvidenceStore _evidenceStore;
    private readonly CompletionArtifacts _artifacts;

    public CompletionArtifactsListAsyncTests()
    {
        _root = Directory.CreateTempSubdirectory("looprelay-completion-artifacts-").FullName;
        _repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(_root),
            Path = _root,
        };
        CreateExecutionEvidenceTable();
        _evidenceStore = new SqliteExecutionEvidenceStore(_repository);
        _artifacts = new CompletionArtifacts(new MemoryArtifactStore(), _repository, _evidenceStore);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ListAsync_MatchesContentProjection_IncludingGlobFilteredExclusions()
    {
        await _evidenceStore.WriteAsync("execution-result", "one");
        await _evidenceStore.WriteAsync("execution-result", "two");
        // Must not survive the "execution-result.*.md" filter below - exercises both the SQL LIKE
        // superset pre-filter and the in-memory GlobMatches authority, not just the trivial "*.md"
        // case.
        await _evidenceStore.WriteAsync("other-stem", "excluded");

        const string pattern = "execution-result.*.md";
        IReadOnlyList<ExecutionEvidenceRecord> viaContentListing = await _evidenceStore.ListAsync(pattern);
        IReadOnlyList<string> viaConsumer = await _artifacts.ListAsync(
            CompletionArtifactPaths.ExecutionEvidenceDirectory, pattern);

        Assert.Equal(viaContentListing.Select(record => record.RelativePath).ToArray(), viaConsumer);
        Assert.Equal(
            [
                ".agents/evidence/execution/execution-result.0001.md",
                ".agents/evidence/execution/execution-result.0002.md",
            ],
            viaConsumer);
    }

    [Fact]
    public async Task ListAsync_OrdersByStemThenSequence_NotInsertionOrder()
    {
        // Interleave writes across two stems whose alphabetical order is the reverse of insertion
        // order. A regression to insertion order (or any other accidental resort introduced by the
        // switch to ListPathsAsync) would be caught here.
        await _evidenceStore.WriteAsync("zeta", "zeta-1");
        await _evidenceStore.WriteAsync("alpha", "alpha-1");
        await _evidenceStore.WriteAsync("zeta", "zeta-2");
        await _evidenceStore.WriteAsync("alpha", "alpha-2");

        IReadOnlyList<string> paths = await _artifacts.ListAsync(
            CompletionArtifactPaths.ExecutionEvidenceDirectory, "*.md");

        Assert.Equal(
            [
                ".agents/evidence/execution/alpha.0001.md",
                ".agents/evidence/execution/alpha.0002.md",
                ".agents/evidence/execution/zeta.0001.md",
                ".agents/evidence/execution/zeta.0002.md",
            ],
            paths);
    }

    [Fact]
    public async Task ListAsync_DoesNotFailOnCorruptedBody_WhileContentReadStillThrows()
    {
        await _evidenceStore.WriteAsync("execution-result", "honest body");
        await CorruptBodyAsync("execution-result");

        IReadOnlyList<string> paths = await _artifacts.ListAsync(
            CompletionArtifactPaths.ExecutionEvidenceDirectory, "*.md");

        Assert.Equal(
            [".agents/evidence/execution/execution-result.0001.md"],
            paths);

        // The other half of the claim: content-hash validation was skipped for listing
        // specifically, not dropped from the store altogether. The very same row still fails the
        // content-validating path.
        await Assert.ThrowsAsync<InvalidOperationException>(() => _evidenceStore.ListAsync());
    }

    private async Task CorruptBodyAsync(string stem)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(
            LoopRelayWorkspaceDatabase.Resolve(_repository));
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE execution_evidence SET body = 'tampered' WHERE stem = $stem;";
        command.Parameters.AddWithValue("$stem", stem);
        await command.ExecuteNonQueryAsync();
    }

    private void CreateExecutionEvidenceTable()
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS execution_evidence(
                logical_path text primary key,
                stem text not null,
                sequence integer not null,
                body text not null,
                content_hash text not null,
                created_at text not null,
                writer text,
                metadata_json text not null,
                unique(stem, sequence)
            );
            """;
        command.ExecuteNonQuery();
    }
}
