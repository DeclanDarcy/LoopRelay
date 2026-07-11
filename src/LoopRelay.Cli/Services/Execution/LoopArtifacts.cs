using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Execution;

/// <summary>
/// All .agents/* disk effects for the loop: rotation (read live + append to the configured history
/// store + delete live), restart-safe latest reads (live file, else the history store), decision
/// persistence, and the operational_context safety copy. Rotation is move-semantics. The canonical
/// composition injects the ledger-backed store (history authority); the file-backed default remains
/// only for legacy loop bodies until M17-M19.
/// </summary>
internal sealed class LoopArtifacts(IArtifactStore _store, Repository _repository, ILoopHistoryStore? historyStore = null)
{
    private readonly ILoopHistoryStore _historyStore = historyStore ?? new FileBackedLoopHistoryStore(_store, _repository);

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

    public Task<string?> RotateLiveHandoffAsync(LoopHistoryLineage? lineage = null) => RotateAsync(OrchestrationArtifactPaths.LiveHandoff, LoopHistoryKind.Handoff, lineage);

    public Task<string?> RotateLiveDecisionsAsync(LoopHistoryLineage? lineage = null) => RotateAsync(OrchestrationArtifactPaths.Decisions, LoopHistoryKind.Decisions, lineage);

    public Task<string?> RotateOperationalDeltaAsync(LoopHistoryLineage? lineage = null) => RotateAsync(OrchestrationArtifactPaths.OperationalDelta, LoopHistoryKind.OperationalDelta, lineage);

    public Task<(string? Content, string? RelativePath)> ReadLatestHandoffAsync() => ReadLatestAsync(OrchestrationArtifactPaths.LiveHandoff, LoopHistoryKind.Handoff);

    public Task<(string? Content, string? RelativePath)> ReadLatestDecisionsAsync() => ReadLatestAsync(OrchestrationArtifactPaths.Decisions, LoopHistoryKind.Decisions);

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

    public async Task PersistDecisionsAsync(string decisions, LoopHistoryLineage? lineage = null)
    {
        await _historyStore.AppendAsync(LoopHistoryKind.Decisions, decisions, lineage);
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

    private async Task<string?> RotateAsync(string liveRelative, LoopHistoryKind kind, LoopHistoryLineage? lineage = null)
    {
        string? content = await _store.ReadAsync(Resolve(liveRelative));
        if (content is null)
        {
            return null;
        }

        await _historyStore.AppendAsync(kind, content, lineage);
        await _store.DeleteAsync(Resolve(liveRelative));
        return content;
    }

    private async Task<(string? Content, string? RelativePath)> ReadLatestAsync(string liveRelative, LoopHistoryKind kind)
    {
        string? live = await _store.ReadAsync(Resolve(liveRelative));
        if (live is not null)
        {
            return (live, liveRelative);
        }

        LoopHistoryRecord? latest = await _historyStore.ReadLatestAsync(kind);
        return latest is null ? (null, null) : (latest.Content, latest.RelativePath);
    }
}
