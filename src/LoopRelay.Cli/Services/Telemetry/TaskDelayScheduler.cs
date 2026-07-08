using LoopRelay.Cli.Abstractions;

namespace LoopRelay.Cli.Services.Telemetry;

/// <summary>Real delay: sleeps via <see cref="Task.Delay(TimeSpan, CancellationToken)"/> (a zero/negative span is a no-op).</summary>
internal sealed class TaskDelayScheduler : IUsageDelay
{
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        duration <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(duration, cancellationToken);
}
