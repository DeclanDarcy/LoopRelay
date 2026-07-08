namespace LoopRelay.Cli.Abstractions;

/// <summary>Wall clock, abstracted so telemetry timestamps and day-rotation are deterministic under test.</summary>
internal interface IClock
{
    DateTimeOffset UtcNow { get; }
}
