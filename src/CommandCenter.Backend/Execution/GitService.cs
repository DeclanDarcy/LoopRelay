using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Execution;

public sealed class GitService(IProcessRunner processRunner) : IGitService
{
    public async Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository)
    {
        var branchResult = await processRunner.RunAsync(
            "git",
            ["branch", "--show-current"],
            repository.Path);
        if (branchResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"git branch failed: {branchResult.StandardError}");
        }

        var statusResult = await processRunner.RunAsync(
            "git",
            ["status", "--porcelain=v1", "-z"],
            repository.Path);
        if (statusResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"git status failed: {statusResult.StandardError}");
        }

        var dirtyState = ParseStatus(statusResult.StandardOutput);

        return new ExecutionRepositorySnapshot
        {
            Branch = branchResult.StandardOutput.Trim(),
            DirtyState = dirtyState,
            CapturedAt = DateTimeOffset.UtcNow
        };
    }

    private static RepositoryDirtyState ParseStatus(string porcelainOutput)
    {
        var staged = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var modified = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var deleted = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var renamed = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var untracked = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = porcelainOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (entry.Length < 4)
            {
                continue;
            }

            var indexStatus = entry[0];
            var workTreeStatus = entry[1];
            var path = NormalizeGitPath(entry[3..]);

            if (indexStatus == '?' && workTreeStatus == '?')
            {
                untracked.Add(path);
                continue;
            }

            if (indexStatus == 'R')
            {
                renamed.Add(path);
                if (index + 1 < entries.Length)
                {
                    index++;
                }
            }

            AddByStatus(indexStatus, path, staged, modified, deleted);
            AddByStatus(workTreeStatus, path, null, modified, deleted);
        }

        return new RepositoryDirtyState
        {
            StagedPaths = staged.ToArray(),
            ModifiedPaths = modified.ToArray(),
            DeletedPaths = deleted.ToArray(),
            RenamedPaths = renamed.ToArray(),
            UntrackedPaths = untracked.ToArray(),
            IsClean = staged.Count == 0 &&
                modified.Count == 0 &&
                deleted.Count == 0 &&
                renamed.Count == 0 &&
                untracked.Count == 0
        };
    }

    private static void AddByStatus(
        char status,
        string path,
        SortedSet<string>? staged,
        SortedSet<string> modified,
        SortedSet<string> deleted)
    {
        switch (status)
        {
            case 'M':
            case 'A':
                if (staged is null)
                {
                    modified.Add(path);
                }
                else
                {
                    staged.Add(path);
                }

                break;
            case 'D':
                deleted.Add(path);
                if (staged is not null)
                {
                    staged.Add(path);
                }

                break;
        }
    }

    private static string NormalizeGitPath(string path)
    {
        return path.Replace('\\', '/');
    }
}
