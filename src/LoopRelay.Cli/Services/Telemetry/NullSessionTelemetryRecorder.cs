using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Infrastructure.Models.Diagnostics;

namespace LoopRelay.Cli.Services.Telemetry;

/// <summary>No-op recorder used when session telemetry is disabled (also skips the post-probe cost).</summary>
internal sealed class NullSessionTelemetryRecorder : ISessionTelemetryRecorder
{
    public Task<string?> RecordTurnAsync(
        string repoName, string workingDirectory, SessionIdentity sessionId, SessionRole role,
        DateTimeOffset openedAtUtc, string? cachedLogPath, AgentTurnResult result,
        InputWaitObservation? inputWait,
        CancellationToken cancellationToken) =>
        Task.FromResult(cachedLogPath);
}
