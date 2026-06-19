using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Artifacts;

internal static class ArtifactPath
{
    public static string ResolveRepositoryPath(Repository repository, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Artifact path must be repository-relative.", nameof(relativePath));
        }

        var repositoryRoot = Path.GetFullPath(repository.Path);
        var resolvedPath = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath));
        var rootWithSeparator = repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!resolvedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(resolvedPath, repositoryRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Artifact path must stay within the repository root.", nameof(relativePath));
        }

        return resolvedPath;
    }

    public static string ToRepositoryRelativePath(Repository repository, string fullPath)
    {
        return Path.GetRelativePath(Path.GetFullPath(repository.Path), Path.GetFullPath(fullPath))
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    public static string CombineRelative(params string[] parts)
    {
        return string.Join('/', parts);
    }
}
