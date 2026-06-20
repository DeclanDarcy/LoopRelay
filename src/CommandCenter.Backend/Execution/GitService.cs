using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Execution;

public sealed class GitService(IProcessRunner processRunner) : IGitService
{
    public async Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository)
    {
        var status = await GetStatusAsync(repository);
        return new ExecutionRepositorySnapshot
        {
            Branch = status.Branch,
            DirtyState = status.DirtyState,
            CapturedAt = status.CapturedAt
        };
    }

    public async Task<RepositoryGitStatus> GetStatusAsync(Repository repository)
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
            ["status", "--porcelain=v1", "--branch", "-z"],
            repository.Path);
        if (statusResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"git status failed: {statusResult.StandardError}");
        }

        var parsedStatus = ParseStatus(statusResult.StandardOutput);

        return new RepositoryGitStatus
        {
            Branch = parsedStatus.Branch ?? branchResult.StandardOutput.Trim(),
            AheadCount = parsedStatus.AheadCount,
            BehindCount = parsedStatus.BehindCount,
            DirtyState = parsedStatus.DirtyState,
            CapturedAt = DateTimeOffset.UtcNow
        };
    }

    private static ParsedGitStatus ParseStatus(string porcelainOutput)
    {
        string? branch = null;
        var aheadCount = 0;
        var behindCount = 0;
        var staged = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var modified = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var added = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var deleted = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var renamed = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var untracked = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = porcelainOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (entry.StartsWith("## ", StringComparison.Ordinal))
            {
                (branch, aheadCount, behindCount) = ParseBranchHeader(entry);
                continue;
            }

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

            AddByStatus(indexStatus, path, staged, modified, added, deleted);
            AddByStatus(workTreeStatus, path, null, modified, added, deleted);
        }

        return new ParsedGitStatus
        {
            Branch = branch,
            AheadCount = aheadCount,
            BehindCount = behindCount,
            DirtyState = new RepositoryDirtyState
            {
                StagedPaths = staged.ToArray(),
                ModifiedPaths = modified.ToArray(),
                AddedPaths = added.ToArray(),
                DeletedPaths = deleted.ToArray(),
                RenamedPaths = renamed.ToArray(),
                UntrackedPaths = untracked.ToArray(),
                IsClean = staged.Count == 0 &&
                    modified.Count == 0 &&
                    added.Count == 0 &&
                    deleted.Count == 0 &&
                    renamed.Count == 0 &&
                    untracked.Count == 0
            }
        };
    }

    private static (string? Branch, int AheadCount, int BehindCount) ParseBranchHeader(string header)
    {
        var body = header[3..];
        var branchSegment = body.Split(' ', 2)[0];
        var branch = branchSegment.Split("...", 2, StringSplitOptions.None)[0];
        var aheadCount = ParseCount(body, "ahead ");
        var behindCount = ParseCount(body, "behind ");

        return (branch == "HEAD" ? string.Empty : branch, aheadCount, behindCount);
    }

    private static int ParseCount(string value, string marker)
    {
        var markerIndex = value.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return 0;
        }

        var start = markerIndex + marker.Length;
        var end = start;
        while (end < value.Length && char.IsDigit(value[end]))
        {
            end++;
        }

        return int.TryParse(value[start..end], out var count) ? count : 0;
    }

    private static void AddByStatus(
        char status,
        string path,
        SortedSet<string>? staged,
        SortedSet<string> modified,
        SortedSet<string> added,
        SortedSet<string> deleted)
    {
        switch (status)
        {
            case 'M':
                if (staged is null)
                {
                    modified.Add(path);
                }
                else
                {
                    staged.Add(path);
                }

                break;
            case 'A':
                added.Add(path);
                if (staged is not null)
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

    private sealed class ParsedGitStatus
    {
        public string? Branch { get; init; }

        public int AheadCount { get; init; }

        public int BehindCount { get; init; }

        public RepositoryDirtyState DirtyState { get; init; } = new();
    }
}
