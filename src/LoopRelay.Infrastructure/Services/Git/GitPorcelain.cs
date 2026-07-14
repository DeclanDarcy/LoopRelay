namespace LoopRelay.Infrastructure.Services.Git;

/// <summary>Parser for <c>git status --porcelain</c> v1 changed paths.</summary>
public static class GitPorcelain
{
    public static IReadOnlyList<string> ChangedPaths(string statusOutput)
    {
        List<string> changed = [];
        foreach (string rawLine in statusOutput.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length < 4)
            {
                continue;
            }

            string path = line[3..];

            int arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrow >= 0)
            {
                // A rename changes both ends of the arrow: the source stops existing and the
                // target starts existing, so both belong to the changed surface.
                AddNormalized(changed, path[..arrow]);
                AddNormalized(changed, path[(arrow + " -> ".Length)..]);
                continue;
            }

            AddNormalized(changed, path);
        }

        return changed;
    }

    private static void AddNormalized(List<string> changed, string path)
    {
        string normalized = path.Replace('\\', '/').Trim('"');
        if (!changed.Contains(normalized, StringComparer.Ordinal))
        {
            changed.Add(normalized);
        }
    }
}
