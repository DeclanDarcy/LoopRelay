using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LoopRelay.Core.Repositories;
using LoopRelay.Persistence.Sqlite;
using LoopRelay.Persistence.Sqlite.Abstractions;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Persistence.Sqlite.Tests;

/// <summary>
/// Guards the OperationalContext source-fingerprint coverage. Rotated historical
/// <c>operational_context.NNNN.md</c> revisions live directly in <c>.agents/</c> (NOT under
/// <c>.agents/operational_context/</c>) and are consumed by the metrics evidence reader via
/// <c>ArtifactService.DiscoverAsync</c>. A change to one MUST move the OperationalContext fingerprint —
/// otherwise <c>CanSkipDerivedRebuildAsync</c> wrongly skips and serves a stale derived base. Before the
/// coverage fix these files were invisible to the provider and these tests fail.
/// </summary>
[Collection("ProcessEnvironment")]
public sealed class DefaultSourceFingerprintProviderTests : IDisposable
{
    private static readonly IReadOnlyList<SourceFamily> OperationalContext =
        new[] { SourceFamily.OperationalContext };

    private readonly string tempRoot;
    private readonly Repository repository;
    private readonly DefaultSourceFingerprintProvider provider;

    public DefaultSourceFingerprintProviderTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), $"cc-fp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRoot, ".agents"));
        repository = new Repository { Id = Guid.NewGuid(), Name = "temp", Path = tempRoot };

        var options = new SqliteDatabaseOptions(Path.Combine(tempRoot, "command-center.db"));
        provider = new DefaultSourceFingerprintProvider(new SqliteConnectionFactory(options), TimeProvider.System);
    }

    [Fact]
    public async Task Fingerprint_ChangesWhenARotatedHistoricalRevisionIsAdded()
    {
        WriteAgentsFile("operational_context.md", "current");
        WriteAgentsFile("operational_context.0001.md", "revision one");
        string before = await FingerprintAsync();

        // A new rotated revision at the .agents/ root: this shifts the family row_count, which the cheap
        // signature gate detects deterministically (no reliance on mtime granularity).
        WriteAgentsFile("operational_context.0002.md", "revision two");
        string after = await FingerprintAsync();

        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task Fingerprint_ChangesWhenAnExistingRotatedRevisionContentChanges()
    {
        WriteAgentsFile("operational_context.md", "current");
        WriteAgentsFile("operational_context.0001.md", "revision one");
        string before = await FingerprintAsync();

        string rotated = Path.Combine(tempRoot, ".agents", "operational_context.0001.md");
        File.WriteAllText(rotated, "revision one EDITED");
        // Bump mtime deterministically so the (row_count, max_updated_at) signature gate re-hashes,
        // isolating the coverage fix from filesystem mtime resolution.
        File.SetLastWriteTimeUtc(rotated, DateTime.UtcNow.AddSeconds(5));
        string after = await FingerprintAsync();

        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task Fingerprint_IsStableWhenNothingChanges()
    {
        WriteAgentsFile("operational_context.md", "current");
        WriteAgentsFile("operational_context.0001.md", "revision one");

        string first = await FingerprintAsync();
        string second = await FingerprintAsync();

        Assert.Equal(first, second);
    }

    private Task<string> FingerprintAsync() =>
        provider.ForFamiliesAsync(repository, OperationalContext, CancellationToken.None);

    private void WriteAgentsFile(string relative, string content)
    {
        string path = Path.Combine(tempRoot, ".agents", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
