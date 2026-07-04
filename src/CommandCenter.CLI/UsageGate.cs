using System;
using System.Threading;
using System.Threading.Tasks;

namespace CommandCenter.Cli;

/// <summary>Waits out a delay. Abstracted so tests assert the requested duration without actually sleeping.</summary>
internal interface IUsageDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}

/// <summary>Real delay: sleeps via <see cref="Task.Delay(TimeSpan, CancellationToken)"/> (a zero/negative span is a no-op).</summary>
internal sealed class TaskDelayScheduler : IUsageDelay
{
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        duration <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(duration, cancellationToken);
}

/// <summary>Blocks until Codex has capacity to run a turn (or fails open), returning the capacity snapshot the
/// turn will start with (null when unreadable). See <see cref="UsageGate"/>.</summary>
internal interface IUsageGate
{
    Task<CodexUsageStatus?> WaitForCapacityAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Runs before every Codex turn (via <see cref="GatedAgentRuntime"/>). Reads Codex quota via
/// <see cref="ICodexUsageProbe"/>; if a limit window is at or below the exhaustion WATERMARK it prints a
/// message and blocks until that window resets, then lets the turn proceed. When BOTH windows are exhausted
/// it waits for the later reset (work needs both to have budget). Fails OPEN — an unreadable/unparseable
/// probe result warns and proceeds rather than wedging the loop.
/// <para>
/// The watermark is a small buffer above zero (not 0%) because a single turn is a whole Codex run of many
/// internal model calls that can burn a chunk of a window mid-flight, and we cannot interrupt it — starting
/// a turn on the last sliver risks crossing zero and crashing. Stopping at the watermark leaves headroom.
/// </para>
/// </summary>
internal sealed class UsageGate(ICodexUsageProbe probe, IUsageDelay delay, ILoopConsole console) : IUsageGate
{
    private const int ExhaustionWatermarkPercent = 1;

    public async Task<CodexUsageStatus?> WaitForCapacityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CodexUsageStatus? status = await probe.QueryAsync(cancellationToken);
        if (status is null)
        {
            console.Warn("Codex usage could not be read — proceeding without the usage gate.");
            return null;
        }

        TimeSpan wait = TimeSpan.Zero;

        if (status.FiveHourRemainingPercent <= ExhaustionWatermarkPercent)
        {
            console.Warn($"Codex 5h limit spent ({status.FiveHourRemainingPercent}% left, <= {ExhaustionWatermarkPercent}% watermark) — resets in {Format(status.FiveHourTimeUntilReset)}.");
            wait = Longer(wait, status.FiveHourTimeUntilReset);
        }

        if (status.WeeklyRemainingPercent <= ExhaustionWatermarkPercent)
        {
            console.Warn($"Codex weekly limit spent ({status.WeeklyRemainingPercent}% left, <= {ExhaustionWatermarkPercent}% watermark) — resets in {Format(status.WeeklyTimeUntilReset)}.");
            wait = Longer(wait, status.WeeklyTimeUntilReset);
        }

        if (wait > TimeSpan.Zero)
        {
            console.Warn($"Waiting {Format(wait)} for Codex usage to reset before continuing.");
            await delay.DelayAsync(wait, cancellationToken);

            // After the reset the pre-wait reading is stale; re-probe so the returned snapshot reflects the
            // capacity the turn actually starts with. Keep the pre-wait value if the re-probe is unreadable.
            status = await probe.QueryAsync(cancellationToken) ?? status;
        }

        return status;
    }

    private static TimeSpan Longer(TimeSpan a, TimeSpan b) => b > a ? b : a;

    private static string Format(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "0m";
        }

        int days = (int)duration.TotalDays;
        return days > 0
            ? $"{days}d {duration.Hours}h {duration.Minutes}m"
            : $"{duration.Hours}h {duration.Minutes}m";
    }
}

/// <summary>No-op stand-in for <see cref="UsageGate"/>, wired in by <see cref="UsageGateComposition"/> while the
/// watermark gate is disabled. Never probes, never waits — turns proceed immediately with no usage snapshot for
/// telemetry to attach.</summary>
internal sealed class NullUsageGate : IUsageGate
{
    public Task<CodexUsageStatus?> WaitForCapacityAsync(CancellationToken cancellationToken) =>
        Task.FromResult<CodexUsageStatus?>(null);
}
