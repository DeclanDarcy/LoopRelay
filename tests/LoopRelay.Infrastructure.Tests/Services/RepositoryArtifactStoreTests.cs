using System.Diagnostics;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Infrastructure.Services.Artifacts;

namespace LoopRelay.Infrastructure.Tests.Services;

public sealed class RepositoryArtifactStoreTests
{
    [Fact]
    public async Task RelativeArtifactsAreWrittenUnderTheSelectedRepository()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-artifact-scope").FullName;
        try
        {
            var artifacts = new RepositoryArtifactStore(
                new FileSystemArtifactStore(),
                new Repository { Id = Guid.NewGuid(), Name = "repo", Path = root });

            await artifacts.WriteAsync(".agents/review/non-implementation-review.md", "# Review\n");

            string expected = Path.Combine(root, ".agents", "review", "non-implementation-review.md");
            Assert.Equal("# Review\n", await File.ReadAllTextAsync(expected));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WriteReadAndListUseRepositoryRelativePaths()
    {
        var store = new MemoryArtifactStore();
        var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = Root() };
        var artifacts = new RepositoryArtifactStore(store, repository);

        await artifacts.WriteAsync(".agents/evidence/e0001.md", "EVIDENCE");

        Assert.Equal("EVIDENCE", await artifacts.ReadAsync(".agents/evidence/e0001.md"));
        Assert.Equal([".agents/evidence/e0001.md"], await artifacts.ListAsync(".agents/evidence", "*.md"));
    }

    [Fact]
    public async Task RepositoryEscapeIsRejectedBeforeMutation()
    {
        var store = new MemoryArtifactStore();
        var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = Root() };
        var artifacts = new RepositoryArtifactStore(store, repository);

        await Assert.ThrowsAsync<ArgumentException>(() => artifacts.WriteAsync("../outside.md", "no"));
        Assert.Null(await store.ReadAsync(Path.GetFullPath(Path.Combine(repository.Path, "..", "outside.md"))));
    }

    [Fact]
    public async Task AbsolutePathsAreRejectedAtTheRepositoryBoundary()
    {
        var artifacts = new RepositoryArtifactStore(
            new MemoryArtifactStore(),
            new Repository { Id = Guid.NewGuid(), Name = "repo", Path = Root() });

        await Assert.ThrowsAsync<ArgumentException>(() => artifacts.ReadAsync(Path.GetFullPath("outside.md")));
    }

    /// <summary>
    /// Renamed/extended from the former <c>FilesystemLinksCannotRedirectWritesOutsideTheRepository</c>.
    /// Symlink creation on Windows requires admin/Developer-Mode privilege that CI and many dev
    /// hosts do not have; this host is one of them (verified: <see cref="Directory.CreateSymbolicLink"/>
    /// fails here with UnauthorizedAccessException). Without a fallback, the escape check would
    /// never actually run on such hosts and the test would pass vacuously. A directory junction
    /// exercises the exact same <c>EnsureExistingPathSegmentsStayWithinRepository</c> reparse-point
    /// resolution and does not require elevation, so it is used whenever a real symlink cannot be
    /// created.
    /// </summary>
    [Fact]
    public async Task Resolve_RejectsSegmentEscapingViaLink()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-artifact-root").FullName;
        string outside = Directory.CreateTempSubdirectory("looprelay-artifact-outside").FullName;
        string link = Path.Combine(root, "linked");
        try
        {
            if (!TryCreateEscapeLink(link, outside, out string? failureReason))
            {
                Assert.Fail(
                    "Could not create a symlink or a directory junction to exercise the escape " +
                    $"check in this environment: {failureReason}");
            }

            var artifacts = new RepositoryArtifactStore(
                new FileSystemArtifactStore(),
                new Repository { Id = Guid.NewGuid(), Name = "repo", Path = root });

            await Assert.ThrowsAsync<ArgumentException>(() => artifacts.WriteAsync("linked/escaped.md", "no"));
            Assert.False(File.Exists(Path.Combine(outside, "escaped.md")));
        }
        finally
        {
            // Delete the reparse point itself (non-recursive) before recursively deleting its
            // parent: a recursive delete that walks into a junction/symlink can throw
            // UnauthorizedAccessException or, worse, delete through into the link target.
            if (Directory.Exists(link))
            {
                Directory.Delete(link, recursive: false);
            }

            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>
    /// Tries a real symlink first (works unprivileged on Linux/macOS, and on Windows hosts with
    /// Developer Mode/admin). Falls back to a directory junction via <c>mklink /J</c>, which needs
    /// no elevation on Windows. Returns false only if neither mechanism is available.
    /// </summary>
    private static bool TryCreateEscapeLink(string linkPath, string targetPath, out string? failureReason)
    {
        failureReason = null;
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            failureReason = exception.Message;
        }

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                failureReason = "Unable to start mklink process.";
                return false;
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || !Directory.Exists(linkPath))
            {
                failureReason = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            failureReason = exception.Message;
            return false;
        }
    }

    private static string Root() =>
        Path.Combine(Path.GetTempPath(), "LoopRelay-infrastructure-tests", Guid.NewGuid().ToString("N"));
}
