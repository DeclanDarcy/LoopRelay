using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;

namespace LoopRelay.Infrastructure.Services.Artifacts;

/// <summary>
/// The production authority for repository artifact addressing. Every public path is a
/// repository-relative artifact identity; absolute filesystem paths exist only below this boundary.
/// </summary>
public sealed class RepositoryArtifactStore(IArtifactStore _store, Repository _repository) :
    IArtifactStore,
    IRepositoryArtifactMetadata
{
    public Task<bool> ExistsAsync(string relativePath) =>
        _store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) =>
        _store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) =>
        _store.WriteAsync(Resolve(relativePath), content);

    public Task DeleteAsync(string relativePath) =>
        _store.DeleteAsync(Resolve(relativePath));

    public async Task<IReadOnlyList<string>> ListAsync(string relativeDirectory, string searchPattern)
    {
        IReadOnlyList<string> absolutePaths = await _store.ListAsync(Resolve(relativeDirectory), searchPattern);
        return absolutePaths
            .Select(path => ArtifactPath.ToRepositoryRelativePath(_repository, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> ListDirectoriesAsync(string relativeDirectory)
    {
        IReadOnlyList<string> absolutePaths = await _store.ListDirectoriesAsync(Resolve(relativeDirectory));
        return absolutePaths
            .Select(path => ArtifactPath.ToRepositoryRelativePath(_repository, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public DateTime? GetLastWriteTimeUtc(string relativePath)
    {
        string path = Resolve(relativePath);
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
    }

    private string Resolve(string relativePath)
    {
        string resolved = ArtifactPath.ResolveRepositoryPath(_repository, relativePath);
        EnsureExistingPathSegmentsStayWithinRepository(resolved, relativePath);
        return resolved;
    }

    private void EnsureExistingPathSegmentsStayWithinRepository(string resolvedPath, string relativePath)
    {
        string repositoryRoot = Path.GetFullPath(_repository.Path);

        // Deliberately NOT cached (evaluated in PERF-27f, wave 2). ResolveFinalTarget(repositoryRoot)
        // looks process-invariant, but it is used below both as the walk's starting point and as the
        // boundary every resolved segment is checked against. If the repository root itself were
        // replaced with (or re-pointed as) a reparse point after a cached value was captured, a cached
        // physicalRoot would silently diverge from where the OS actually resolves `repositoryRoot` at
        // I/O time: the walk would verify segments under the stale target while the real read/write
        // lands under the live one, so an escape planted under the live target would never be
        // inspected. That is strictly worse than the false-rejection failure mode and is
        // indistinguishable, from the caller's side, from a link "created after process start" -
        // which the escape walk is explicitly required to keep catching. There is no cheap freshness
        // check for a reparse-point retarget that doesn't cost the same syscall as just re-resolving,
        // so this call stays per-operation. Do not memoize it without re-litigating this comment.
        string physicalRoot = ResolveFinalTarget(repositoryRoot);
        string current = physicalRoot;

        foreach (string segment in Path.GetRelativePath(repositoryRoot, resolvedPath)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo? entry = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : File.Exists(current)
                    ? new FileInfo(current)
                    : null;
            if (entry is null)
            {
                // A non-existent segment cannot currently redirect traversal. The filesystem store
                // will create descendants beneath the last physically verified parent.
                break;
            }

            current = ResolveFinalTarget(entry.FullName);
            if (!IsWithin(physicalRoot, current))
            {
                throw new ArgumentException(
                    $"Artifact path `{relativePath}` escapes repository root through a filesystem link.",
                    nameof(relativePath));
            }
        }
    }

    private static string ResolveFinalTarget(string path)
    {
        FileSystemInfo entry = Directory.Exists(path)
            ? new DirectoryInfo(path)
            : new FileInfo(path);
        try
        {
            return Path.GetFullPath(entry.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? entry.FullName);
        }
        catch (IOException)
        {
            // Broken links are not traversable. The underlying operation will report the filesystem error.
            return Path.GetFullPath(entry.FullName);
        }
    }

    private static bool IsWithin(string root, string candidate)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string rootWithSeparator = root.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootWithSeparator, comparison) ||
            string.Equals(candidate, root, comparison);
    }
}
