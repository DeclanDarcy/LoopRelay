using System.Globalization;
using System.Text.RegularExpressions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Cli;

/// <summary>Real delay: sleeps via <see cref="Task.Delay(TimeSpan, CancellationToken)"/> (a zero/negative span is a no-op).</summary>
internal sealed class TaskDelayScheduler : IUsageDelay
{
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        duration <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(duration, cancellationToken);
}
