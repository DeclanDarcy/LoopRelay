using CommandCenter.Backend.Repositories;
using System.Security.Cryptography;
using System.Text;

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

    public async Task<CommitPreparation> PrepareCommitAsync(Repository repository, ExecutionSession session)
    {
        var status = await GetStatusAsync(repository);
        var preExistingPaths = GetAllDirtyPaths(session.RepositorySnapshot?.DirtyState);
        var scopeItems = BuildScopeItems(status.DirtyState, preExistingPaths);
        var snapshot = new CommitStatusSnapshot
        {
            Id = BuildSnapshotId(status),
            Branch = status.Branch,
            AheadCount = status.AheadCount,
            BehindCount = status.BehindCount,
            DirtyState = status.DirtyState,
            CapturedAt = status.CapturedAt
        };

        return new CommitPreparation
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            RepositoryId = session.RepositoryId,
            RepositoryPath = session.RepositoryPath,
            ProposedMessage = BuildProposedCommitMessage(session.MilestonePath, scopeItems.Count),
            ScopeItems = scopeItems,
            StatusSnapshot = snapshot,
            GeneratedAt = DateTimeOffset.UtcNow,
            HasPreExistingChanges = scopeItems.Any(item => item.Origin == CommitChangeOrigin.PreExisting)
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

    private static IReadOnlyList<CommitScopeItem> BuildScopeItems(
        RepositoryDirtyState dirtyState,
        ISet<string> preExistingPaths)
    {
        var items = new Dictionary<string, CommitScopeItem>(StringComparer.OrdinalIgnoreCase);
        AddScopeItems(items, dirtyState.StagedPaths, CommitChangeType.Staged, preExistingPaths);
        AddScopeItems(items, dirtyState.ModifiedPaths, CommitChangeType.Modified, preExistingPaths);
        AddScopeItems(items, dirtyState.AddedPaths, CommitChangeType.Added, preExistingPaths);
        AddScopeItems(items, dirtyState.DeletedPaths, CommitChangeType.Deleted, preExistingPaths);
        AddScopeItems(items, dirtyState.RenamedPaths, CommitChangeType.Renamed, preExistingPaths);
        AddScopeItems(items, dirtyState.UntrackedPaths, CommitChangeType.Untracked, preExistingPaths);

        return items.Values.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddScopeItems(
        IDictionary<string, CommitScopeItem> items,
        IEnumerable<string> paths,
        CommitChangeType changeType,
        ISet<string> preExistingPaths)
    {
        foreach (var path in paths.Select(NormalizeGitPath))
        {
            if (items.ContainsKey(path))
            {
                continue;
            }

            items[path] = new CommitScopeItem
            {
                Path = path,
                ChangeType = changeType,
                Origin = preExistingPaths.Contains(path)
                    ? CommitChangeOrigin.PreExisting
                    : CommitChangeOrigin.ExecutionGenerated,
                IsSelected = true
            };
        }
    }

    private static ISet<string> GetAllDirtyPaths(RepositoryDirtyState? dirtyState)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (dirtyState is null)
        {
            return paths;
        }

        foreach (var path in dirtyState.StagedPaths
            .Concat(dirtyState.ModifiedPaths)
            .Concat(dirtyState.AddedPaths)
            .Concat(dirtyState.DeletedPaths)
            .Concat(dirtyState.RenamedPaths)
            .Concat(dirtyState.UntrackedPaths))
        {
            paths.Add(NormalizeGitPath(path));
        }

        return paths;
    }

    private static string BuildProposedCommitMessage(string milestonePath, int changedFileCount)
    {
        var milestoneName = Path.GetFileNameWithoutExtension(milestonePath);
        if (string.IsNullOrWhiteSpace(milestoneName))
        {
            milestoneName = "Execute selected milestone";
        }

        var fileLabel = changedFileCount == 1 ? "file" : "files";
        return $"{milestoneName}\n\n- {changedFileCount} {fileLabel} changed";
    }

    private static string BuildSnapshotId(RepositoryGitStatus status)
    {
        var builder = new StringBuilder();
        builder.AppendLine(status.Branch);
        builder.AppendLine(status.AheadCount.ToString());
        builder.AppendLine(status.BehindCount.ToString());
        AppendPaths(builder, "staged", status.DirtyState.StagedPaths);
        AppendPaths(builder, "modified", status.DirtyState.ModifiedPaths);
        AppendPaths(builder, "added", status.DirtyState.AddedPaths);
        AppendPaths(builder, "deleted", status.DirtyState.DeletedPaths);
        AppendPaths(builder, "renamed", status.DirtyState.RenamedPaths);
        AppendPaths(builder, "untracked", status.DirtyState.UntrackedPaths);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void AppendPaths(StringBuilder builder, string label, IEnumerable<string> paths)
    {
        foreach (var path in paths.Select(NormalizeGitPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(label);
            builder.Append(':');
            builder.AppendLine(path);
        }
    }

    private sealed class ParsedGitStatus
    {
        public string? Branch { get; init; }

        public int AheadCount { get; init; }

        public int BehindCount { get; init; }

        public RepositoryDirtyState DirtyState { get; init; } = new();
    }
}
