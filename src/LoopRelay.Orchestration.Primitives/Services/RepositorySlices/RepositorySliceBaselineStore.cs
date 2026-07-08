using System.Text.Json;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Orchestration.Models.RepositorySlices;

namespace LoopRelay.Orchestration.Services.RepositorySlices;

public sealed class RepositorySliceBaselineStore(
    RepositoryChangeSetDetector detector,
    IArtifactStore? artifacts = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<RepositorySliceBaseline> CapturePreSliceAsync(
        string? executionSliceId = null,
        DateTimeOffset? capturedAtUtc = null)
    {
        string sliceId = string.IsNullOrWhiteSpace(executionSliceId)
            ? NewExecutionSliceId()
            : executionSliceId.Trim();
        RepositorySliceSnapshot snapshot = await detector.CaptureSnapshotAsync(sliceId, capturedAtUtc);
        string? persistedPath = null;

        if (artifacts is not null)
        {
            persistedPath = OrchestrationArtifactPaths.NonImplementationSliceBaseline(sliceId);
            await artifacts.WriteAsync(persistedPath, Serialize(snapshot));
        }

        return new RepositorySliceBaseline(sliceId, snapshot, persistedPath);
    }

    public async Task<RepositorySliceSnapshot> CapturePostSliceAsync(
        RepositorySliceBaseline baseline,
        DateTimeOffset? capturedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        RepositorySliceSnapshot snapshot =
            await detector.CaptureSnapshotAsync(baseline.ExecutionSliceId, capturedAtUtc);
        if (artifacts is not null)
        {
            await artifacts.WriteAsync(
                OrchestrationArtifactPaths.NonImplementationSlicePostSnapshot(baseline.ExecutionSliceId),
                Serialize(snapshot));
        }

        return snapshot;
    }

    public async Task<RepositorySliceDelta> CapturePostSliceDeltaAsync(
        RepositorySliceBaseline baseline,
        DateTimeOffset? capturedAtUtc = null)
    {
        RepositorySliceSnapshot postSnapshot = await CapturePostSliceAsync(baseline, capturedAtUtc);
        return ComputeDelta(baseline, postSnapshot);
    }

    public RepositorySliceDelta ComputeDelta(
        RepositorySliceBaseline baseline,
        RepositorySliceSnapshot postSnapshot)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(postSnapshot);

        if (!string.Equals(baseline.ExecutionSliceId, postSnapshot.ExecutionSliceId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Post-slice snapshot must use the baseline execution slice ID.", nameof(postSnapshot));
        }

        var baselineByPath = baseline.Snapshot.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        var changed = new List<RepositoryChangedFileFacts>();

        foreach (RepositoryFileSnapshotEntry post in postSnapshot.Files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            baselineByPath.TryGetValue(post.Path, out RepositoryFileSnapshotEntry? pre);
            if (pre is not null && SameSnapshotFacts(pre, post))
            {
                continue;
            }

            changed.Add(new RepositoryChangedFileFacts(
                baseline.ExecutionSliceId,
                post.Path,
                post.PreviousPath ?? pre?.PreviousPath,
                post.Status,
                pre?.Status,
                post.Status,
                pre is not null,
                post.Exists,
                post.IsDeleted,
                post.Extension,
                post.Size,
                pre?.ContentSha256,
                post.ContentSha256,
                post.TrackedDiffMetadata));
        }

        return new RepositorySliceDelta(baseline.ExecutionSliceId, changed);
    }

    private static bool SameSnapshotFacts(RepositoryFileSnapshotEntry left, RepositoryFileSnapshotEntry right) =>
        string.Equals(left.Status, right.Status, StringComparison.Ordinal) &&
        string.Equals(left.PreviousPath, right.PreviousPath, StringComparison.Ordinal) &&
        left.Exists == right.Exists &&
        left.IsDeleted == right.IsDeleted &&
        left.Size == right.Size &&
        string.Equals(left.ContentSha256, right.ContentSha256, StringComparison.Ordinal) &&
        SameDiffMetadata(left.TrackedDiffMetadata, right.TrackedDiffMetadata);

    private static bool SameDiffMetadata(
        IReadOnlyList<RepositoryGitDiffNameStatus> left,
        IReadOnlyList<RepositoryGitDiffNameStatus> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!EqualityComparer<RepositoryGitDiffNameStatus>.Default.Equals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string NewExecutionSliceId() =>
        $"slice-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

    private static string Serialize(RepositorySliceSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions) + Environment.NewLine;
}
