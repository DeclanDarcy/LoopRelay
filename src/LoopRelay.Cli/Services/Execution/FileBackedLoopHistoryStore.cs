using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Execution;

internal sealed class FileBackedLoopHistoryStore(IArtifactStore _store, Repository _repository) : ILoopHistoryStore
{
    public async Task<LoopHistoryRecord> AppendAsync(
        LoopHistoryKind kind,
        string content,
        LoopHistoryProducerCorrelation? producer = null)
    {
        LoopHistorySpec spec = GetSpec(kind);
        int sequence = await NextSequenceAsync(spec);
        string relativePath = spec.HistoricalPath(sequence);
        string target = Resolve(relativePath);
        if (await _store.ExistsAsync(target))
        {
            throw new IOException($"Historical artifact already exists: {relativePath}");
        }

        await _store.WriteAsync(target, content);
        return new LoopHistoryRecord(kind, sequence, relativePath, content, producer);
    }

    public async Task<LoopHistoryRecord?> ReadLatestAsync(LoopHistoryKind kind)
    {
        LoopHistorySpec spec = GetSpec(kind);
        int sequence = await HighestSequenceAsync(spec);
        if (sequence == 0)
        {
            return null;
        }

        string relativePath = spec.HistoricalPath(sequence);
        string? content = await _store.ReadAsync(Resolve(relativePath));
        return new LoopHistoryRecord(kind, sequence, relativePath, content);
    }

    private string Resolve(string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(_repository, relativePath);

    private async Task<int> NextSequenceAsync(LoopHistorySpec spec) =>
        await HighestSequenceAsync(spec) + 1;

    private async Task<int> HighestSequenceAsync(LoopHistorySpec spec)
    {
        IReadOnlyList<string> files = await _store.ListAsync(Resolve(spec.Directory), spec.SearchPattern);
        int max = 0;
        foreach (string file in files)
        {
            string[] parts = Path.GetFileName(file).Split('.');
            if (parts.Length < 3 || !string.Equals(parts[0], spec.BaseName, StringComparison.Ordinal))
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

    private static LoopHistorySpec GetSpec(LoopHistoryKind kind) => kind switch
    {
        LoopHistoryKind.Decisions => new LoopHistorySpec(
            OrchestrationArtifactPaths.DecisionsDirectory,
            OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
            "decisions",
            OrchestrationArtifactPaths.HistoricalDecision),
        LoopHistoryKind.Handoff => new LoopHistorySpec(
            OrchestrationArtifactPaths.HandoffsDirectory,
            OrchestrationArtifactPaths.HistoricalHandoffSearchPattern,
            "handoff",
            OrchestrationArtifactPaths.HistoricalHandoff),
        LoopHistoryKind.OperationalDelta => new LoopHistorySpec(
            OrchestrationArtifactPaths.DeltasDirectory,
            OrchestrationArtifactPaths.HistoricalDeltaSearchPattern,
            "operational_delta",
            OrchestrationArtifactPaths.HistoricalDelta),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private readonly record struct LoopHistorySpec(
        string Directory,
        string SearchPattern,
        string BaseName,
        Func<int, string> HistoricalPath);
}
