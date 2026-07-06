namespace LoopRelay.Infrastructure.Git;

/// <summary>Parser for <c>git status --porcelain</c> v1 changed paths.</summary>
public static class GitPorcelain
{
    public static IReadOnlyList<string> ChangedPaths(string statusOutput)
    {
        var changed = new List<string>();
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
                path = path[(arrow + " -> ".Length)..];
            }

            changed.Add(path.Replace('\\', '/').Trim('"'));
        }

        return changed;
    }
}
