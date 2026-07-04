using System.Text;
using CommandCenter.Core.Artifacts;

namespace CommandCenter.Core.Tests;

public sealed class FileSystemArtifactStoreTests
{
    [Fact]
    public async Task ConcurrentWritersAndReadersNeverObserveATornFile()
    {
        // Fix A (refactor-lazy-sqlite.md / atomic file writes): WriteAsync must be atomic — write to a temp file
        // in the same directory then File.Move(overwrite) (atomic rename). The naive truncate-then-write it
        // replaces could let a concurrent reader observe a partial/torn file mid-write. This drives many writers
        // that overwrite the SAME path with distinct, fixed-length, internally-consistent contents while a reader
        // hammers the file; every successful read must equal one of the WHOLE candidate contents, never a torn mix.
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, ".agents", "workflow", "timeline.json");
        var store = new FileSystemArtifactStore();

        // Each candidate is large (so a truncate-then-write would have a wide torn window) and self-describing:
        // a single repeated marker char, so any partial/interleaved file is detectable as "not all one marker".
        string[] candidates = Enumerable.Range(0, 8)
            .Select(index => new string((char)('A' + index), 64 * 1024))
            .ToArray();
        var validContents = new HashSet<string?>(candidates, StringComparer.Ordinal);

        // Seed a complete file so the very first read can never see a missing/empty file.
        await store.WriteAsync(path, candidates[0]);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var observed = new System.Collections.Concurrent.ConcurrentBag<string>();
        Exception? readerFailure = null;

        Task writer = Task.Run(async () =>
        {
            for (int iteration = 0; iteration < 200 && !cancellation.IsCancellationRequested; iteration++)
            {
                await store.WriteAsync(path, candidates[iteration % candidates.Length]);
            }
        });

        Task[] readers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            try
            {
                while (!writer.IsCompleted)
                {
                    string? content = await store.ReadAsync(path);
                    if (content is not null)
                    {
                        observed.Add(content);
                        // A torn file would be a mix of two markers (or a truncated length) — neither is a candidate.
                        Assert.Contains(content, validContents);
                    }
                }
            }
            catch (Exception exception) when (exception is not Xunit.Sdk.XunitException)
            {
                readerFailure = exception;
            }
        })).ToArray();

        await writer;
        await Task.WhenAll(readers);

        Assert.Null(readerFailure);
        Assert.NotEmpty(observed);
        // The final committed file is one whole candidate.
        Assert.Contains(await store.ReadAsync(path), validContents);
        // No atomic-write temp files leaked, and ListAsync never surfaces them.
        IReadOnlyList<string> listed = await store.ListAsync(Path.GetDirectoryName(path)!, "*");
        Assert.DoesNotContain(listed, file => Path.GetFileName(file).EndsWith(".tmp", StringComparison.Ordinal));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, ".*.tmp"));
    }

    [Fact]
    public async Task WriteProducesUtf8WithoutBomByteIdenticalToContent()
    {
        // Fix A: the atomic write must preserve identical bytes — UTF-8 with NO BOM, matching the prior
        // File.WriteAllTextAsync default — so no golden/contract bytes drift.
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, ".agents", "decisions", "decisions.md");
        const string content = "decisions: café — naïve ✓"; // multi-byte UTF-8 to expose any BOM/encoding drift.
        var store = new FileSystemArtifactStore();

        await store.WriteAsync(path, content);

        byte[] actual = await File.ReadAllBytesAsync(path);
        byte[] expected = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        Assert.Equal(expected, actual);
        Assert.False(actual.Length >= 3 && actual[0] == 0xEF && actual[1] == 0xBB && actual[2] == 0xBF);
    }

    [Fact]
    public async Task WriteReadExistsAndDelete()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, ".agents", "handoffs", "handoff.md");
        var store = new FileSystemArtifactStore();

        await store.WriteAsync(path, "handoff");

        Assert.True(await store.ExistsAsync(path));
        Assert.Equal("handoff", await store.ReadAsync(path));

        await store.DeleteAsync(path);

        Assert.False(await store.ExistsAsync(path));
    }

    [Fact]
    public async Task WrittenContentPersistsAcrossStoreInstances()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, ".agents", "decisions", "decisions.md");

        await new FileSystemArtifactStore().WriteAsync(path, "decisions");

        Assert.Equal("decisions", await new FileSystemArtifactStore().ReadAsync(path));
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
