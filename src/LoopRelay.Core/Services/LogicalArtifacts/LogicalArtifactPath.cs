namespace LoopRelay.Core.Services.Artifacts;

internal static class LogicalArtifactPath
{
    public static bool TryNormalize(string path, out string normalized)
    {
        normalized = path.Replace('\\', '/').Trim();
        normalized = normalized.TrimStart('/');

        if (normalized.Length == 0 || Path.IsPathRooted(path))
        {
            normalized = string.Empty;
            return false;
        }

        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = string.Join('/', segments);
        return true;
    }

    public static bool MatchesPattern(string relativePath, string directory, string searchPattern)
    {
        string normalizedPath = NormalizeKnownRelative(relativePath);
        string normalizedDirectory = NormalizeKnownRelative(directory).TrimEnd('/');
        string prefix = normalizedDirectory.Length == 0 ? string.Empty : normalizedDirectory + "/";
        if (!normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string remainder = normalizedPath[prefix.Length..];
        return !remainder.Contains('/', StringComparison.Ordinal) &&
            FileNameMatches(remainder, searchPattern);
    }

    public static string NormalizeKnownRelative(string path) =>
        path.Replace('\\', '/').Trim().TrimStart('/');

    private static bool FileNameMatches(string fileName, string searchPattern)
    {
        if (searchPattern == "*")
        {
            return true;
        }

        int star = searchPattern.IndexOf('*', StringComparison.Ordinal);
        if (star < 0)
        {
            return string.Equals(fileName, searchPattern, StringComparison.OrdinalIgnoreCase);
        }

        string prefix = searchPattern[..star];
        string suffix = searchPattern[(star + 1)..];
        return fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
            fileName.Length >= prefix.Length + suffix.Length;
    }
}
