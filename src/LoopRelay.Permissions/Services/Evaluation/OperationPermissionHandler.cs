using LoopRelay.Permissions.Models.Evaluation;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Primitives.Requests;

namespace LoopRelay.Permissions.Services.Evaluation;

public sealed class OperationPermissionHandler
{
    public PermissionResult Evaluate(PermissionRequest request, OperationPermissionProfile profile)
    {
        PermissionRequestDetails details = request.Details
            ?? new PermissionRequestDetails(PermissionRequestKind.Unknown, "unknown");

        if (details.Kind is PermissionRequestKind.UserInput)
        {
            return Deny("Operation-scoped sessions cannot request user input.");
        }

        if (details.Kind is PermissionRequestKind.McpElicitation)
        {
            return Deny("Operation-scoped sessions cannot request MCP elicitation.");
        }

        if (details.RequestsNetwork)
        {
            return Deny("Operation-scoped sessions cannot request network access.");
        }

        if (details.Kind is PermissionRequestKind.CommandExecution)
        {
            return Deny("Operation-scoped sessions cannot execute commands.");
        }

        if (details.Kind is PermissionRequestKind.Permissions)
        {
            return Deny("Operation-scoped sessions cannot request broader permissions.");
        }

        return details.Kind switch
        {
            PermissionRequestKind.FileChange => EvaluateFileChange(details, profile),
            PermissionRequestKind.ToolCall => EvaluateToolCall(details, profile),
            _ => Deny("Operation-scoped sessions deny unknown request shapes."),
        };
    }

    private static PermissionResult EvaluateFileChange(
        PermissionRequestDetails details,
        OperationPermissionProfile profile)
    {
        if (details.PathAccess is PermissionPathAccess.Delete)
        {
            return Deny("Operation-scoped sessions cannot delete files.");
        }

        IReadOnlyList<string> targets = details.PathArguments ?? [];
        if (targets.Count > 0)
        {
            return targets.All(target => IsAllowedPath(profile, target, PermissionPathAccess.Write))
                ? Allow("Operation writes allowed.")
                : Deny("One or more file-change targets are outside the operation write profile.");
        }

        string? target = details.FilePath ?? ExactGrantRoot(details.GrantRoot, profile);
        if (string.IsNullOrWhiteSpace(target))
        {
            return Deny("File-change request did not expose an exact target path.");
        }

        return IsAllowedPath(profile, target, PermissionPathAccess.Write)
            ? Allow("Operation write allowed.")
            : Deny("File-change target is outside the operation write profile.");
    }

    private static PermissionResult EvaluateToolCall(
        PermissionRequestDetails details,
        OperationPermissionProfile profile)
    {
        if (details.PathAccess is PermissionPathAccess.Delete)
        {
            return Deny("Operation-scoped sessions cannot delete files.");
        }

        if (details.PathAccess is PermissionPathAccess.Unknown)
        {
            return Deny("Operation-scoped sessions deny unknown tool calls.");
        }

        IReadOnlyList<string> paths = details.PathArguments ?? [];
        if (paths.Count == 0)
        {
            return Deny("Operation-scoped tool call did not expose path arguments.");
        }

        foreach (string path in paths)
        {
            if (!IsAllowedPath(profile, path, details.PathAccess))
            {
                return Deny("Tool-call path is outside the operation profile.");
            }
        }

        return Allow("Operation tool call allowed.");
    }

    private static string? ExactGrantRoot(string? grantRoot, OperationPermissionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(grantRoot))
        {
            return null;
        }

        if (IsAllowedPath(profile, grantRoot, PermissionPathAccess.Write))
        {
            return grantRoot;
        }

        return null;
    }

    private static bool IsAllowedPath(
        OperationPermissionProfile profile,
        string requestedPath,
        PermissionPathAccess access)
    {
        if (!TryNormalize(profile.RepositoryRoot, requestedPath, out string relativePath))
        {
            return false;
        }

        IReadOnlyList<string> exact = access == PermissionPathAccess.Read
            ? profile.AllowedReads
            : profile.AllowedWrites;
        IReadOnlyList<OperationPathGlob> globs = access == PermissionPathAccess.Read
            ? profile.AllowedReadGlobs
            : profile.AllowedWriteGlobs;

        if (exact.Any(path => string.Equals(NormalizeRelative(path), relativePath, StringComparison.Ordinal)))
        {
            return true;
        }

        foreach (OperationPathGlob glob in globs)
        {
            string globDirectory = NormalizeRelative(glob.Directory);
            if (string.Equals(relativePath, globDirectory, StringComparison.Ordinal))
            {
                return access == PermissionPathAccess.Read;
            }

            string directory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? string.Empty;
            string fileName = Path.GetFileName(relativePath);
            if (string.Equals(directory, globDirectory, StringComparison.Ordinal)
                && GlobMatches(fileName, glob.Pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryNormalize(string repositoryRoot, string requestedPath, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(repositoryRoot) || string.IsNullOrWhiteSpace(requestedPath))
        {
            return false;
        }

        if (ContainsParentTraversal(requestedPath))
        {
            return false;
        }

        string root = Path.GetFullPath(repositoryRoot);
        string resolved = Path.IsPathRooted(requestedPath)
            ? Path.GetFullPath(requestedPath)
            : Path.GetFullPath(Path.Combine(root, requestedPath));

        string rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(resolved, root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ContainsReparsePoint(root, resolved))
        {
            return false;
        }

        string rel = Path.GetRelativePath(root, resolved)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        if (rel == ".")
        {
            return false;
        }

        relativePath = rel;
        return true;
    }

    private static bool ContainsParentTraversal(string path) =>
        path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/'], StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));

    private static bool ContainsReparsePoint(string repositoryRoot, string resolvedPath)
    {
        string root = Path.GetFullPath(repositoryRoot);
        string current = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string relative = Path.GetRelativePath(root, resolvedPath);
        if (relative == ".")
        {
            return false;
        }

        foreach (string segment in relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                continue;
            }

            FileAttributes attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeRelative(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private static bool GlobMatches(string fileName, string pattern) =>
        GlobMatches(fileName.AsSpan(), pattern.AsSpan());

    private static bool GlobMatches(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern)
    {
        int textIndex = 0;
        int patternIndex = 0;
        int starIndex = -1;
        int matchIndex = 0;

        while (textIndex < text.Length)
        {
            if (patternIndex < pattern.Length
                && (pattern[patternIndex] == '?' || pattern[patternIndex] == text[textIndex]))
            {
                textIndex++;
                patternIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex++;
                matchIndex = textIndex;
            }
            else if (starIndex != -1)
            {
                patternIndex = starIndex + 1;
                textIndex = ++matchIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }

    private static PermissionResult Allow(string reason) => new(RuleDecision.Allow, reason);

    private static PermissionResult Deny(string reason) => new(RuleDecision.Deny, reason);
}
