using LoopRelay.Agents.Models;
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
        CancellationToken cancellationToken);
}

/// <summary>
/// Adds one post-turn capacity probe, resolves the codex rollout file once per session, computes effective
/// tokens with the router's cost model, and appends a <see cref="SessionTelemetryRecord"/>. Every step is
/// best-effort: a failure warns and is swallowed so a telemetry fault never breaks a turn. The one exception
/// is a genuine caller cancellation, which is intent (not a telemetry fault) and is propagated.
/// </summary>
internal sealed class SessionTelemetryRecorder(
    ICodexUsageProbe probe,
    ICodexRolloutLocator locator,
    ISessionTelemetrySink sink,
    IDecisionCostModel costModel,
    IClock clock,
    ILoopConsole console) : ISessionTelemetryRecorder
{
    public async Task<string?> RecordTurnAsync(
        string repoName,
        string workingDirectory,
        SessionIdentity sessionId,
        SessionRole role,
        DateTimeOffset openedAtUtc,
        string? cachedLogPath,
        AgentTurnResult result,
        CancellationToken cancellationToken)
    {
        string? path = cachedLogPath;
        try
        {
            CodexUsageStatus? post = await ProbePostAsync(cancellationToken);
            path ??= locator.Resolve(workingDirectory, openedAtUtc);

            AgentTokenUsage usage = result.Usage;
            var record = new SessionTelemetryRecord(
                clock.UtcNow,
                repoName,
                path,
                sessionId.Value.ToString(),
                role.ToString(),
                result.TurnIndex,
                usage.PromptTokens,
                usage.OutputTokens,
                usage.CachedInputTokens,
                costModel.Measure(usage),
                post?.FiveHourRemainingPercent,
                post?.WeeklyRemainingPercent);

            sink.Append(record);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // a genuine caller cancellation is intent, not a telemetry fault — propagate it
        }
        catch (Exception ex)
        {
            console.Warn($"Session telemetry not recorded: {ex.Message}");
        }

        return path;
    }

    // The post probe is best-effort: the token row is worth keeping even when capacity is unknown. Only a
    // genuine caller cancellation escapes (re-thrown so the outer handler propagates it).
    private async Task<CodexUsageStatus?> ProbePostAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await probe.QueryAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>No-op recorder used when session telemetry is disabled (also skips the post-probe cost).</summary>
internal sealed class NullSessionTelemetryRecorder : ISessionTelemetryRecorder
{
    public Task<string?> RecordTurnAsync(
        string repoName, string workingDirectory, SessionIdentity sessionId, SessionRole role,
        DateTimeOffset openedAtUtc, string? cachedLogPath, AgentTurnResult result,
        CancellationToken cancellationToken) =>
        Task.FromResult(cachedLogPath);
}
