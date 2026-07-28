using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Orchestration.Abstractions;

namespace LoopRelay.Cli.Services.Telemetry;

/// <summary>
/// Adds one post-turn capacity probe, resolves the codex rollout file once per session, computes effective
/// tokens with the router's cost model, and appends a <see cref="SessionTelemetryRecord"/>. Every step is
/// best-effort: a failure warns and is swallowed so a telemetry fault never breaks a turn. The one exception
/// is a genuine caller cancellation, which is intent (not a telemetry fault) and is propagated.
/// </summary>
internal sealed class SessionTelemetryRecorder(
    ICodexUsageProbe _probe,
    ICodexRolloutLocator _locator,
    ISessionTelemetrySink _sink,
    IDecisionCostModel _costModel,
    IClock _clock,
    ILoopConsole _console,
    ProviderEnvironmentConfiguration? _providerEnvironment = null) : ISessionTelemetryRecorder
{
    public async Task<string?> RecordTurnAsync(
        string repoName,
        string workingDirectory,
        SessionIdentity sessionId,
        SessionRole role,
        DateTimeOffset openedAtUtc,
        string? cachedLogPath,
        AgentTurnResult result,
        InputWaitObservation? inputWait,
        CancellationToken cancellationToken,
        string? providerThreadId = null)
    {
        string? path = StillOnDisk(cachedLogPath);
        try
        {
            CodexUsageStatus? post = await ProbePostAsync(cancellationToken);
            if (path is null && providerThreadId is { Length: > 0 })
            {
                string codexHome = (_providerEnvironment ?? ProviderEnvironmentConfiguration.Resolve()).CodexHome;
                // LocateAsync, not ReadExactAsync: only the path is wanted here, so nothing is read, hashed
                // or materialized. It also returns the newest match where ReadExactAsync would refuse a
                // duplicate thread id outright — a ratified divergence, because a best-effort location beats
                // no location on the fail-open telemetry path while diagnosis must not guess.
                path = await new CodexRolloutRepository().LocateAsync(
                    codexHome, providerThreadId, cancellationToken);
            }
            path ??= providerThreadId is null ? _locator.Resolve(workingDirectory, openedAtUtc) : null;

            AgentTokenUsage usage = result.Usage;
            var record = new SessionTelemetryRecord(
                _clock.UtcNow,
                repoName,
                path,
                sessionId.Value.ToString(),
                role.ToString(),
                result.TurnIndex,
                usage.PromptTokens,
                usage.OutputTokens,
                usage.CachedInputTokens,
                _costModel.Measure(usage),
                post?.FiveHourRemainingPercent,
                post?.WeeklyRemainingPercent,
                inputWait?.Transport,
                inputWait?.Model,
                inputWait?.PromptChars,
                inputWait?.PromptBytes,
                inputWait?.PromptTokensEstimated,
                inputWait?.TokenEstimateSource,
                inputWait?.PromptPreparedAt,
                inputWait?.RequestWriteStartedAt,
                inputWait?.RequestSubmittedAt,
                inputWait?.RequestAcceptedAt,
                inputWait?.FirstProtocolEventAt,
                inputWait?.FirstOutputAt,
                inputWait?.CompletedAt,
                usage.PromptTokens,
                usage.CachedInputTokens,
                usage.OutputTokens,
                inputWait?.Status,
                inputWait?.EstimatorVersion,
                providerThreadId,
                result.ProviderTurnId,
                CertificationInvocationId: Environment.GetEnvironmentVariable(
                    "LOOPRELAY_CERTIFICATION_INVOCATION_ID"),
                InvocationRole: Environment.GetEnvironmentVariable(
                    "LOOPRELAY_CERTIFICATION_INVOCATION_ROLE"));

            _sink.Append(record);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // a genuine caller cancellation is intent, not a telemetry fault — propagate it
        }
        catch (Exception ex)
        {
            _console.Warn($"Session telemetry not recorded: {ex.Message}");
        }

        return path;
    }

    /// <summary>
    /// Guards the one hazard in caching the rollout path for a whole session: the file it names can be
    /// rotated, deleted or replaced mid-session. A cached path is therefore only reused while its file is
    /// still there — otherwise it is dropped, this turn re-resolves, and the session caches whatever came
    /// back. If nothing does, the row carries no path at all, which is honest; stamping a vanished file onto
    /// every remaining row is not. Neither outcome touches the turn, which is what fail-open requires.
    /// </summary>
    private static string? StillOnDisk(string? cachedLogPath) =>
        cachedLogPath is { Length: > 0 } && File.Exists(cachedLogPath) ? cachedLogPath : null;

    // The post probe is best-effort: the token row is worth keeping even when capacity is unknown. Only a
    // genuine caller cancellation escapes (re-thrown so the outer handler propagates it).
    private async Task<CodexUsageStatus?> ProbePostAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _probe.QueryAsync(cancellationToken);
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
