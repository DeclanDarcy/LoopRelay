using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Infrastructure.Models.Diagnostics;

namespace LoopRelay.Cli.Abstractions;

/// <summary>Records one telemetry row per codex turn. Returns the resolved codex rollout path (cached across a
/// session's turns), or null. MUST NOT throw — telemetry never breaks a turn.</summary>
internal interface ISessionTelemetryRecorder
{
    Task<string?> RecordTurnAsync(
        string repoName,
        string workingDirectory,
        SessionIdentity sessionId,
        SessionRole role,
        DateTimeOffset openedAtUtc,
        string? cachedLogPath,
        AgentTurnResult result,
        InputWaitObservation? inputWait,
        CancellationToken cancellationToken);
}
