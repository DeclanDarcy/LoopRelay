using CommandCenter.Backend.Repositories;
using System.Security.Cryptography;
using System.Text;

namespace CommandCenter.Backend.Execution;

public sealed class GitService(IProcessRunner processRunner) : IGitService
{
    public async Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository)
    {
        RepositoryGitStatus status = await GetStatusAsync(repository);
        return new ExecutionRepositorySnapshot
        {
            Branch = status.Branch,
            DirtyState = status.DirtyState,
            CapturedAt = status.CapturedAt
        };
    }

    public async Task<RepositoryGitStatus> GetStatusAsync(Repository repository)
    {
        ProcessRunResult branchResult = await processRunner.RunAsync(
            "git",
            ["branch", "--show-current"],
            repository.Path);
        if (branchResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"git branch failed: {branchResult.StandardError}");
        }

        ProcessRunResult statusResult = await processRunner.RunAsync(
            "git",
            ["status", "--porcelain=v1", "--branch", "-z"],
            repository.Path);
        if (statusResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"git status failed: {statusResult.StandardError}");
        }

        ParsedGitStatus parsedStatus = ParseStatus(statusResult.StandardOutput);

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
        RepositoryGitStatus status = await GetStatusAsync(repository);
        ISet<string> preExistingPaths = GetAllDirtyPaths(session.RepositorySnapshot?.DirtyState);
        IReadOnlyList<CommitScopeItem> scopeItems = BuildScopeItems(status.DirtyState, preExistingPaths);
        CommitStatusSnapshot snapshot = CreateCommitStatusSnapshot(status);

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

    public async Task<CommitStatusSnapshot> GetCommitStatusSnapshotAsync(Repository repository)
    {
        return CreateCommitStatusSnapshot(await GetStatusAsync(repository));
    }

    public async Task<CommitResult> CommitAsync(
        Repository repository,
        string message,
        IReadOnlyList<string> selectedPaths,
        string preparationSnapshotId)
    {
        var addArguments = new List<string> { "add", "-A", "--" };
        addArguments.AddRange(selectedPaths);
        ProcessRunResult addResult = await processRunner.RunAsync("git", addArguments, repository.Path);
        if (addResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"git add failed: {addResult.StandardError}");
        }

        ProcessRunResult commitResult = await processRunner.RunAsync(
            "git",
            ["commit", "-m", message],
            repository.Path);
        if (commitResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"git commit failed: {commitResult.StandardError}");
        }

        ProcessRunResult shaResult = await processRunner.RunAsync(
            "git",
            ["rev-parse", "HEAD"],
            repository.Path);
        if (shaResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"git rev-parse failed: {shaResult.StandardError}");
        }

        return new CommitResult
        {
            CommitSha = shaResult.StandardOutput.Trim(),
            CommittedAt = DateTimeOffset.UtcNow,
            CommitMessage = message,
            PreparationSnapshotId = preparationSnapshotId,
            SelectedPaths = selectedPaths.Select(NormalizeGitPath).ToArray()
        };
    }

    public async Task<PushResult> PushAsync(Repository repository, string? commitSha)
    {
        DateTimeOffset attemptedAt = DateTimeOffset.UtcNow;
        ProcessRunResult pushResult = await processRunner.RunAsync(
            "git",
            ["push"],
            repository.Path);
        if (pushResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"git push failed: {pushResult.StandardError}");
        }

        RepositoryGitStatus status = await GetStatusAsync(repository);

        return new PushResult
        {
            PushAttemptedAt = attemptedAt,
            PushedAt = DateTimeOffset.UtcNow,
            PushedCommitSha = commitSha,
            BranchName = status.Branch,
            RemoteName = null
        };
    }

    private static ParsedGitStatus ParseStatus(string porcelainOutput)
    {
        string? branch = null;
        int aheadCount = 0;
        int behindCount = 0;
        var staged = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var modified = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var added = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var deleted = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var renamed = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var untracked = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] entries = porcelainOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        for (int index = 0; index < entries.Length; index++)
        {
            string entry = entries[index];
            if (entry.StartsWith("## ", StringComparison.Ordinal))
            {
                (branch, aheadCount, behindCount) = ParseBranchHeader(entry);
                continue;
            }

            if (entry.Length < 4)
            {
                continue;
            }

            char indexStatus = entry[0];
            char workTreeStatus = entry[1];
            string path = NormalizeGitPath(entry[3..]);

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
        string body = header[3..];
        string branchSegment = body.Split(' ', 2)[0];
        string branch = branchSegment.Split("...", 2, StringSplitOptions.None)[0];
        int aheadCount = ParseCount(body, "ahead ");
        int behindCount = ParseCount(body, "behind ");

        return (branch == "HEAD" ? string.Empty : branch, aheadCount, behindCount);
    }

    private static int ParseCount(string value, string marker)
    {
        int markerIndex = value.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return 0;
        }

        int start = markerIndex + marker.Length;
        int end = start;
        while (end < value.Length && char.IsDigit(value[end]))
        {
            end++;
        }

        return int.TryParse(value[start..end], out int count) ? count : 0;
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
        foreach (string path in paths.Select(NormalizeGitPath))
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

        foreach (string path in dirtyState.StagedPaths
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
        string milestoneName = Path.GetFileNameWithoutExtension(milestonePath);
        if (string.IsNullOrWhiteSpace(milestoneName))
        {
            milestoneName = "Execute selected milestone";
        }

        string fileLabel = changedFileCount == 1 ? "file" : "files";
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

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static CommitStatusSnapshot CreateCommitStatusSnapshot(RepositoryGitStatus status)
    {
        return new CommitStatusSnapshot
        {
            Id = BuildSnapshotId(status),
            Branch = status.Branch,
            AheadCount = status.AheadCount,
            BehindCount = status.BehindCount,
            DirtyState = status.DirtyState,
            CapturedAt = status.CapturedAt
        };
    }

    private static void AppendPaths(StringBuilder builder, string label, IEnumerable<string> paths)
    {
        foreach (string path in paths.Select(NormalizeGitPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
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
