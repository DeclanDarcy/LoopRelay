using System.Security.Cryptography;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Models.RepositorySlices;

namespace LoopRelay.Orchestration.Services.RepositorySlices;

public sealed class RepositoryChangeSetDetector(IProcessRunner _processRunner, Repository _repository)
{
    public async Task<RepositorySliceSnapshot> CaptureSnapshotAsync(
        string executionSliceId,
        DateTimeOffset? capturedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionSliceId);

        IReadOnlyList<GitStatusEntry> statusEntries = await GetStatusEntriesAsync();
        IReadOnlyList<RepositoryGitDiffNameStatus> diffEntries = await GetDiffMetadataAsync();
        var diffByPath = diffEntries
            .GroupBy(entry => entry.Path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RepositoryGitDiffNameStatus>)group.ToList(), StringComparer.Ordinal);

        var files = new List<RepositoryFileSnapshotEntry>(statusEntries.Count);
        foreach (GitStatusEntry status in statusEntries.OrderBy(entry => entry.Path, StringComparer.Ordinal))
        {
            string fullPath = ArtifactPath.ResolveRepositoryPath(_repository, status.Path);
            bool fileExists = File.Exists(fullPath);
            bool directoryExists = Directory.Exists(fullPath);
            long? size = fileExists ? new FileInfo(fullPath).Length : null;
            string? hash = fileExists ? await Sha256Async(fullPath) : null;
            string extension = Path.GetExtension(status.Path).ToLowerInvariant();
            IReadOnlyList<RepositoryGitDiffNameStatus> metadata =
                diffByPath.TryGetValue(status.Path, out IReadOnlyList<RepositoryGitDiffNameStatus>? entries)
                    ? entries
                    : Array.Empty<RepositoryGitDiffNameStatus>();

            files.Add(new RepositoryFileSnapshotEntry(
                status.Path,
                status.PreviousPath,
                status.Status,
                fileExists || directoryExists,
                IsDeleted(status.Status, fileExists || directoryExists),
                extension,
                size,
                hash,
                metadata));
        }

        return new RepositorySliceSnapshot(
            executionSliceId.Trim(),
            capturedAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
            files);
    }

    private async Task<IReadOnlyList<GitStatusEntry>> GetStatusEntriesAsync()
    {
        ProcessRunResult result = await _processRunner.RunAsync(
            "git",
            ["status", "--porcelain", "--untracked-files=all"],
            _repository.Path);
        if (result.ExitCode != 0)
        {
            throw new RepositoryChangeSetDetectionException($"git status failed: {result.StandardError}");
        }

        return ParsePorcelainStatus(result.StandardOutput);
    }

    private async Task<IReadOnlyList<RepositoryGitDiffNameStatus>> GetDiffMetadataAsync()
    {
        ProcessRunResult result = await _processRunner.RunAsync(
            "git",
            ["diff", "--name-status", "--find-renames", "HEAD", "--"],
            _repository.Path);
        if (result.ExitCode != 0)
        {
            throw new RepositoryChangeSetDetectionException($"git diff --name-status failed: {result.StandardError}");
        }

        return ParseNameStatus(result.StandardOutput);
    }

    internal static IReadOnlyList<GitStatusEntry> ParsePorcelainStatus(string statusOutput)
    {
        var entries = new List<GitStatusEntry>();
        foreach (string rawLine in statusOutput.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length < 4)
            {
                continue;
            }

            string status = line[..2];
            string path = line[3..];
            string? previousPath = null;
            int arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrow >= 0)
            {
                previousPath = NormalizeGitPath(path[..arrow]);
                path = path[(arrow + " -> ".Length)..];
            }

            entries.Add(new GitStatusEntry(status, NormalizeGitPath(path), previousPath));
        }

        return entries;
    }

    internal static IReadOnlyList<RepositoryGitDiffNameStatus> ParseNameStatus(string diffOutput)
    {
        var entries = new List<RepositoryGitDiffNameStatus>();
        foreach (string rawLine in diffOutput.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            string status = parts[0];
            if ((status.StartsWith('R') || status.StartsWith('C')) && parts.Length >= 3)
            {
                entries.Add(new RepositoryGitDiffNameStatus(
                    status,
                    NormalizeGitPath(parts[2]),
                    NormalizeGitPath(parts[1])));
                continue;
            }

            entries.Add(new RepositoryGitDiffNameStatus(status, NormalizeGitPath(parts[1])));
        }

        return entries;
    }

    private static bool IsDeleted(string status, bool exists) =>
        !exists && status.Any(character => character == 'D');

    private static string NormalizeGitPath(string path)
    {
        string trimmed = path.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed.Replace('\\', '/');
    }

    private static async Task<string> Sha256Async(string path)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal sealed record GitStatusEntry(string Status, string Path, string? PreviousPath);
}
