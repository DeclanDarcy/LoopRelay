using System.Text.Json;
using LoopRelay.Core.Repositories;
using LoopRelay.Agents.Abstractions;

namespace LoopRelay.Cli;

/// <summary>Reads a Codex quota snapshot, or null when it cannot be determined.</summary>
internal interface ICodexUsageProbe
{
    Task<CodexUsageStatus?> QueryAsync(CancellationToken cancellationToken);
}
