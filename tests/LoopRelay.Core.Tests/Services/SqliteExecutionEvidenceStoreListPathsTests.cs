using LoopRelay.Core.Abstractions.Persistence;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Tests.Services;

/// <summary>
/// Covers PERF-14 (store side): <see cref="SqliteExecutionEvidenceStore.ListPathsAsync"/> must
/// return exactly the paths <see cref="SqliteExecutionEvidenceStore.ListAsync"/> would, while
/// reading no bodies and computing no hashes - and <c>ListAsync</c> must keep validating every row
/// it returns.
/// </summary>
public sealed class SqliteExecutionEvidenceStoreListPathsTests
{
    [Fact]
    public async Task ListPaths_ReturnsSamePathsAsListAsync()
    {
        Repository repository = await CreateWorkspaceAsync();
        var store = new SqliteExecutionEvidenceStore(repository);

        await store.WriteAsync("execution-result", "first");
        await store.WriteAsync("execution-result", "second");
        await store.WriteAsync("other-stem", "third");

        foreach (string pattern in new[] { "*.md", "*", "execution-result.*.md", "other-stem.0001.md" })
        {
            IReadOnlyList<ExecutionEvidenceRecord> viaList = await store.ListAsync(pattern);
            IReadOnlyList<ExecutionEvidencePath> viaPaths = await store.ListPathsAsync(pattern);

            Assert.Equal(
                viaList.Select(record => record.RelativePath).ToArray(),
                viaPaths.Select(path => path.RelativePath).ToArray());
            Assert.Equal(
                viaList.Select(record => (record.Stem, record.Sequence)).ToArray(),
                viaPaths.Select(path => (path.Stem, path.Sequence)).ToArray());
        }
    }

    [Fact]
    public async Task ListPaths_NonMatchingRowsAreExcluded()
    {
        Repository repository = await CreateWorkspaceAsync();
        var store = new SqliteExecutionEvidenceStore(repository);

        await store.WriteAsync("execution-result", "kept");
        await store.WriteAsync("other-stem", "dropped");

        IReadOnlyList<ExecutionEvidencePath> matched = await store.ListPathsAsync("execution-result.*.md");

        Assert.Equal(
            [".agents/evidence/execution/execution-result.0001.md"],
            matched.Select(path => path.RelativePath).ToArray());
    }

    [Fact]
    public async Task ListPaths_DoesNotReadBodies()
    {
        Repository repository = await CreateWorkspaceAsync();
        var store = new SqliteExecutionEvidenceStore(repository);

        await store.WriteAsync("execution-result", "honest body");

        // Corrupt the stored body so it no longer agrees with its recorded content hash. Anything
        // that actually reads the body must notice; anything that only reports the path cannot.
        await using (SqliteConnection tamper = LoopRelayWorkspaceDatabase.OpenReadWrite(
            LoopRelayWorkspaceDatabase.Resolve(repository)))
        {
            await tamper.OpenAsync();
            await using SqliteCommand command = tamper.CreateCommand();
            command.CommandText =
                "UPDATE execution_evidence SET body = 'tampered body' WHERE stem = 'execution-result';";
            await command.ExecuteNonQueryAsync();
        }

        IReadOnlyList<ExecutionEvidencePath> paths = await store.ListPathsAsync();
        Assert.Equal(
            [".agents/evidence/execution/execution-result.0001.md"],
            paths.Select(path => path.RelativePath).ToArray());

        // The other half of the claim: the content path still catches exactly this corruption, so
        // the test above proves bodies went unread rather than that validation was dropped.
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ListAsync());
    }

    [Fact]
    public async Task ListPaths_UnderscoreInStem_DoesNotAdmitOtherStems()
    {
        Repository repository = await CreateWorkspaceAsync();
        var store = new SqliteExecutionEvidenceStore(repository);

        // `_` is a single-character wildcard in SQL LIKE but a literal in this store's glob, so an
        // unescaped pre-filter would let `abc` through to the in-memory pass. This asserts the end
        // result, which GlobMatches also defends - it is coverage of the seam, not proof that the
        // escaping alone carries it.
        await store.WriteAsync("a_c", "underscore stem");
        await store.WriteAsync("abc", "wildcard collision");

        IReadOnlyList<ExecutionEvidencePath> matched = await store.ListPathsAsync("a_c.*.md");

        Assert.Equal(
            [".agents/evidence/execution/a_c.0001.md"],
            matched.Select(path => path.RelativePath).ToArray());
    }

    [Fact]
    public async Task ListPaths_OnEmptyStore_ReturnsEmpty()
    {
        Repository repository = await CreateWorkspaceAsync();
        var store = new SqliteExecutionEvidenceStore(repository);

        Assert.Empty(await store.ListPathsAsync());
    }

    private static async Task<Repository> CreateWorkspaceAsync()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-evidence-paths-").FullName;
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };

        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        return repository;
    }
}
