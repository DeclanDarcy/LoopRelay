using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Execution;

/// <summary>
/// Explicit legacy numbered-file boundary. Canonical runtime code cannot append through this
/// type; Compatibility Authority reads it and imports facts into the canonical history ledger.
/// </summary>
internal sealed class FileBackedLoopHistoryStore(IArtifactStore _store, Repository _repository)
    : ILegacyLoopHistoryStore
{
    public async Task<LegacyLoopHistoryRecord> AppendLegacyAsync(
        LoopHistoryKind kind,
        string content,
        CancellationToken cancellationToken = default)
    {
        LoopHistorySpec spec = GetSpec(kind);
        IReadOnlyList<LegacyLoopHistoryRecord> existing = await ReadAllLegacyAsync(kind, cancellationToken);
        int sequence = existing.Count == 0 ? 1 : existing.Max(record => record.Sequence) + 1;
        string relativePath = spec.HistoricalPath(sequence);
        string target = Resolve(relativePath);
        if (await _store.ExistsAsync(target))
        {
            throw new IOException($"Historical artifact already exists: {relativePath}");
        }

        await _store.WriteAsync(target, content);
        return new LegacyLoopHistoryRecord(kind, sequence, relativePath, content);
    }

    public async Task<LegacyLoopHistoryRecord?> ReadLatestLegacyAsync(
        LoopHistoryKind kind,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LegacyLoopHistoryRecord> records = await ReadAllLegacyAsync(kind, cancellationToken);
        return records.Count == 0 ? null : records[^1];
    }

    public async Task<IReadOnlyList<LegacyLoopHistoryRecord>> ReadAllLegacyAsync(
        LoopHistoryKind kind,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LoopHistorySpec spec = GetSpec(kind);
        IReadOnlyList<string> files = await _store.ListAsync(Resolve(spec.Directory), spec.SearchPattern);
        var records = new List<LegacyLoopHistoryRecord>();
        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] parts = Path.GetFileName(file).Split('.');
            if (parts.Length < 3 || !string.Equals(parts[0], spec.BaseName, StringComparison.Ordinal) ||
                !int.TryParse(parts[^2], out int sequence) || sequence <= 0)
            {
                continue;
            }

            string relativePath = spec.HistoricalPath(sequence);
            records.Add(new LegacyLoopHistoryRecord(
                kind,
                sequence,
                relativePath,
                await _store.ReadAsync(Resolve(relativePath))));
        }

        return records.OrderBy(record => record.Sequence).ToArray();
    }

    private string Resolve(string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(_repository, relativePath);

    private static LoopHistorySpec GetSpec(LoopHistoryKind kind) => kind switch
    {
        LoopHistoryKind.Decisions => new(
            OrchestrationArtifactPaths.DecisionsDirectory,
            OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
            "decisions",
            OrchestrationArtifactPaths.HistoricalDecision),
        LoopHistoryKind.Handoff => new(
            OrchestrationArtifactPaths.HandoffsDirectory,
            OrchestrationArtifactPaths.HistoricalHandoffSearchPattern,
            "handoff",
            OrchestrationArtifactPaths.HistoricalHandoff),
        LoopHistoryKind.OperationalDelta => new(
            OrchestrationArtifactPaths.DeltasDirectory,
            OrchestrationArtifactPaths.HistoricalDeltaSearchPattern,
            "operational_delta",
            OrchestrationArtifactPaths.HistoricalDelta),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private readonly record struct LoopHistorySpec(
        string Directory,
        string SearchPattern,
        string BaseName,
        Func<int, string> HistoricalPath);
}
