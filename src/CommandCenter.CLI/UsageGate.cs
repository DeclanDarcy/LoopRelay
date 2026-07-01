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

/// <summary>
/// The first gate of every loop iteration. Reads Codex quota via <see cref="ICodexUsageProbe"/>; if a limit
/// window is exhausted (0% remaining) it prints a message and blocks until that window resets, then lets the
/// iteration proceed. When BOTH windows are exhausted it waits for the later reset (work needs both to have
/// budget). Fails OPEN — an unreadable/unparseable probe result warns and proceeds rather than wedging the loop.
/// </summary>
internal sealed class UsageGate(ICodexUsageProbe probe, IUsageDelay delay, ILoopConsole console)
{
    public async Task WaitForCapacityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CodexUsageStatus? status = await probe.QueryAsync(cancellationToken);
        if (status is null)
        {
            console.Warn("Codex usage could not be read — proceeding without the usage gate.");
            return;
        }

        TimeSpan wait = TimeSpan.Zero;

        if (status.FiveHourRemainingPercent == 0)
        {
            console.Warn($"Codex 5h limit exhausted — resets in {Format(status.FiveHourTimeUntilReset)}.");
            wait = Longer(wait, status.FiveHourTimeUntilReset);
        }

        if (status.WeeklyRemainingPercent == 0)
        {
            console.Warn($"Codex weekly limit exhausted — resets in {Format(status.WeeklyTimeUntilReset)}.");
            wait = Longer(wait, status.WeeklyTimeUntilReset);
        }

        if (wait > TimeSpan.Zero)
        {
            console.Warn($"Waiting {Format(wait)} for Codex usage to reset before continuing.");
            await delay.DelayAsync(wait, cancellationToken);
        }
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
