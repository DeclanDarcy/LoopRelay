using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// All .agents/* disk effects for the loop: rotation (read+archive numbered+delete live), restart-safe
/// latest reads (live or highest numbered), decision persistence (numbered + canonical), and the
/// operational_context safety copy. Rotation is move-semantics (matches RepositoryOrchestrator's loop path).
/// </summary>
internal sealed class LoopArtifacts(IArtifactStore store, Repository repository)
{
    public Task<bool> ExistsAsync(string relativePath) =>
        store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) =>
        store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) =>
        store.WriteAsync(Resolve(relativePath), content);

    public Task<string?> ReadPlanAsync() => ReadAsync(OrchestrationArtifactPaths.Plan);

    public Task<string?> RotateLiveHandoffAsync() => RotateAsync(
        OrchestrationArtifactPaths.LiveHandoff,
        OrchestrationArtifactPaths.HandoffsDirectory,
        OrchestrationArtifactPaths.HistoricalHandoffSearchPattern,
        "handoff",
        OrchestrationArtifactPaths.HistoricalHandoff);

    public Task<string?> RotateLiveDecisionsAsync() => RotateAsync(
        OrchestrationArtifactPaths.Decisions,
        OrchestrationArtifactPaths.DecisionsDirectory,
        OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
        "decisions",
        OrchestrationArtifactPaths.HistoricalDecision);

    public Task<(string? Content, string? RelativePath)> ReadLatestHandoffAsync() => ReadLatestAsync(
        OrchestrationArtifactPaths.LiveHandoff,
        OrchestrationArtifactPaths.HandoffsDirectory,
        OrchestrationArtifactPaths.HistoricalHandoffSearchPattern,
        "handoff",
        OrchestrationArtifactPaths.HistoricalHandoff);

    public Task<(string? Content, string? RelativePath)> ReadLatestDecisionsAsync() => ReadLatestAsync(
        OrchestrationArtifactPaths.Decisions,
        OrchestrationArtifactPaths.DecisionsDirectory,
        OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
        "decisions",
        OrchestrationArtifactPaths.HistoricalDecision);

    public async Task PersistDecisionsAsync(string decisions)
    {
        int sequence = await NextSequenceAsync(
            OrchestrationArtifactPaths.DecisionsDirectory,
            OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
            "decisions");
        await store.WriteAsync(Resolve(OrchestrationArtifactPaths.HistoricalDecision(sequence)), decisions);
        await store.WriteAsync(Resolve(OrchestrationArtifactPaths.Decisions), decisions);
    }

    public async Task EnsureOperationalContextAsync()
    {
        if (await ExistsAsync(OrchestrationArtifactPaths.OperationalContext))
        {
            return;
        }

        string? plan = await ReadPlanAsync();
        if (plan is not null)
        {
            await WriteAsync(OrchestrationArtifactPaths.OperationalContext, plan);
        }
    }

    private string Resolve(string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    private async Task<string?> RotateAsync(
        string liveRelative, string directoryRelative, string searchPattern, string baseName, Func<int, string> historical)
    {
        string? content = await store.ReadAsync(Resolve(liveRelative));
        if (content is null)
        {
            return null;
        }

        int sequence = await NextSequenceAsync(directoryRelative, searchPattern, baseName);
        string target = Resolve(historical(sequence));
        if (await store.ExistsAsync(target))
        {
            throw new IOException($"Historical artifact already exists: {historical(sequence)}");
        }

        await store.WriteAsync(target, content);
        await store.DeleteAsync(Resolve(liveRelative));
        return content;
    }

    private async Task<(string? Content, string? RelativePath)> ReadLatestAsync(
        string liveRelative, string directoryRelative, string searchPattern, string baseName, Func<int, string> historical)
    {
        string? live = await store.ReadAsync(Resolve(liveRelative));
        if (live is not null)
        {
            return (live, liveRelative);
        }

        int highest = await HighestSequenceAsync(directoryRelative, searchPattern, baseName);
        if (highest == 0)
        {
            return (null, null);
        }

        string rel = historical(highest);
        return (await store.ReadAsync(Resolve(rel)), rel);
    }

    private async Task<int> NextSequenceAsync(string directoryRelative, string searchPattern, string baseName) =>
        await HighestSequenceAsync(directoryRelative, searchPattern, baseName) + 1;

    private async Task<int> HighestSequenceAsync(string directoryRelative, string searchPattern, string baseName)
    {
        IReadOnlyList<string> files = await store.ListAsync(Resolve(directoryRelative), searchPattern);
        int max = 0;
        foreach (string file in files)
        {
            string[] parts = Path.GetFileName(file).Split('.');
            if (parts.Length < 3 || !string.Equals(parts[0], baseName, StringComparison.Ordinal))
            {
                continue;
            }

            string segment = parts[^2];
            if (segment.Length == 4 && int.TryParse(segment, out int parsed) && parsed > 0)
            {
                max = Math.Max(max, parsed);
            }
        }

        return max;
    }
}
