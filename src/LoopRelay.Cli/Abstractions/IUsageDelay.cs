namespace LoopRelay.Cli.Abstractions;

/// <summary>Waits out a delay. Abstracted so tests assert the requested duration without actually sleeping.</summary>
internal interface IUsageDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}
