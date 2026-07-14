using LoopRelay.Cli.Abstractions;

namespace LoopRelay.Cli.Tests.Services.Usage;


/// <summary>Records requested delay durations instead of actually sleeping.</summary>
internal sealed class FakeUsageDelay : IUsageDelay
{
    public List<TimeSpan> Delays { get; } = new();

    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(duration);
        return Task.CompletedTask;
    }
}
