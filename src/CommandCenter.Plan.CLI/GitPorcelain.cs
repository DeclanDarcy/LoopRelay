namespace CommandCenter.Plan.Cli;

/// <summary>
/// Parses <c>git status --porcelain</c> (v1) output into the list of changed paths, used by the submodule
/// publisher (<see cref="AgentsSubmodulePublisher"/>). Verbatim copy of the reference CLI's helper
/// (internals cannot be referenced across projects — accepted duplication).
/// </summary>
internal static class GitPorcelain
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

            // Drop the 2-char XY status + the separating space; keep the path.
            string path = line[3..];

            // A rename/copy entry is "old -> new"; the new path is the one that now exists.
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
