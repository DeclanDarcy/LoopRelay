using LoopRelay.Agents.Models;
using LoopRelay.Infrastructure.Diagnostics;
using LoopRelay.Orchestration.Abstractions;

namespace LoopRelay.Cli;

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
