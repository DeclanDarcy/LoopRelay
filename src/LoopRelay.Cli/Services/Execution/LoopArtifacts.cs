using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Execution;

/// <summary>
/// All .agents/* disk effects for the loop: rotation (read+archive numbered+delete live), restart-safe
/// latest reads (live or highest numbered), decision persistence (numbered + canonical), and the
/// operational_context safety copy. Rotation is move-semantics (matches RepositoryOrchestrator's loop path).
/// </summary>
internal sealed class LoopArtifacts(IArtifactStore _store, Repository _repository)
{
    public Repository Repository => _repository;

    public IArtifactStore Store => _store;

    public Task<bool> ExistsAsync(string relativePath) =>
        _store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) =>
        _store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) =>
        _store.WriteAsync(Resolve(relativePath), content);

    // Absolute-path access for callers that already resolved and validated their target path.
    public Task<bool> ExistsAbsoluteAsync(string absolutePath) => _store.ExistsAsync(absolutePath);

    public Task<string?> ReadAbsoluteAsync(string absolutePath) => _store.ReadAsync(absolutePath);

    public Task WriteAbsoluteAsync(string absolutePath, string content) => _store.WriteAsync(absolutePath, content);

    public Task<string?> ReadPlanAsync() => ReadAsync(OrchestrationArtifactPaths.Plan);

    /// <summary>The optional plan companion (<c>.agents/details.md</c>): null when absent, mirroring <see cref="ReadPlanAsync"/>.</summary>
    public Task<string?> ReadDetailsAsync() => ReadAsync(OrchestrationArtifactPaths.Details);

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

    public Task<string?> RotateOperationalDeltaAsync() => RotateAsync(
        OrchestrationArtifactPaths.OperationalDelta,
        OrchestrationArtifactPaths.DeltasDirectory,
        OrchestrationArtifactPaths.HistoricalDeltaSearchPattern,
        "operational_delta",
        OrchestrationArtifactPaths.HistoricalDelta);

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

    /// <summary>
    /// Retires the live decisions.md once an execution slice has consumed it: deletes ONLY the live file so the
    /// next slice runs a fresh decision (and a mid-slice restart re-executes the pending decisions rather than
    /// skipping the decision session forever). No re-archival is needed — <see cref="PersistDecisionsAsync"/>
    /// already wrote the numbered snapshot that is the retained history, so rotating here would only duplicate it.
    /// Returns true if a live decisions.md was present; idempotent (a no-op when already absent).
    /// </summary>
    public async Task<bool> RetireLiveDecisionsAsync()
    {
        string live = Resolve(OrchestrationArtifactPaths.Decisions);
        if (!await _store.ExistsAsync(live))
        {
            return false;
        }

        await _store.DeleteAsync(live);
        return true;
    }

    public async Task PersistDecisionsAsync(string decisions)
    {
        int sequence = await NextSequenceAsync(
            OrchestrationArtifactPaths.DecisionsDirectory,
            OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
            "decisions");
        string target = Resolve(OrchestrationArtifactPaths.HistoricalDecision(sequence));
        if (await _store.ExistsAsync(target))
        {
            throw new IOException($"Historical artifact already exists: {OrchestrationArtifactPaths.HistoricalDecision(sequence)}");
        }
        await _store.WriteAsync(target, decisions);
        await _store.WriteAsync(Resolve(OrchestrationArtifactPaths.Decisions), decisions);
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
        ArtifactPath.ResolveRepositoryPath(_repository, relativePath);

    private async Task<string?> RotateAsync(
        string liveRelative, string directoryRelative, string searchPattern, string baseName, Func<int, string> historical)
    {
        string? content = await _store.ReadAsync(Resolve(liveRelative));
        if (content is null)
        {
            return null;
        }

        int sequence = await NextSequenceAsync(directoryRelative, searchPattern, baseName);
        string target = Resolve(historical(sequence));
        if (await _store.ExistsAsync(target))
        {
            throw new IOException($"Historical artifact already exists: {historical(sequence)}");
        }

        await _store.WriteAsync(target, content);
        await _store.DeleteAsync(Resolve(liveRelative));
        return content;
    }

    private async Task<(string? Content, string? RelativePath)> ReadLatestAsync(
        string liveRelative, string directoryRelative, string searchPattern, string baseName, Func<int, string> historical)
    {
        string? live = await _store.ReadAsync(Resolve(liveRelative));
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
        return (await _store.ReadAsync(Resolve(rel)), rel);
    }

    private async Task<int> NextSequenceAsync(string directoryRelative, string searchPattern, string baseName) =>
        await HighestSequenceAsync(directoryRelative, searchPattern, baseName) + 1;

    private async Task<int> HighestSequenceAsync(string directoryRelative, string searchPattern, string baseName)
    {
        IReadOnlyList<string> files = await _store.ListAsync(Resolve(directoryRelative), searchPattern);
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
