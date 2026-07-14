using LoopRelay.Cli.Models;

namespace LoopRelay.Cli.Abstractions;

/// <summary>Reads a Codex quota snapshot, or null when it cannot be determined.</summary>
internal interface ICodexUsageProbe
{
    Task<CodexUsageStatus?> QueryAsync(CancellationToken cancellationToken);
}
